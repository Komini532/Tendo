using System.Data.Common;
using Dapper;
using MySqlConnector;
using Tendo.Game.State;

namespace Tendo.Data.Repositories;

/// <inheritdoc />
public sealed class PlayerRepository : IPlayerRepository
{
    private readonly IDbConnectionFactory _connections;
    private readonly GameDefaults _defaults;

    public PlayerRepository(IDbConnectionFactory connections, GameDefaults defaults)
    {
        _connections = connections;
        _defaults = defaults;
    }

    public async Task<PlayerState?> FindAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var found = await FindManyAsync([userId], cancellationToken);
        return found.Count > 0 ? found[0] : null;
    }

    public async Task<PlayerState> GetOrCreateAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = _defaults.NewPlayer(userId);
        await SaveAsync(created, cancellationToken);

        // 別のリクエストが同時に作っていた場合は、そちらの内容が正となる。
        return await FindAsync(userId, cancellationToken) ?? created;
    }

    public async Task<IReadOnlyList<PlayerState>> FindManyAsync(
        IReadOnlyCollection<ulong> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken);
        return await LoadAsync(connection, transaction: null, userIds, cancellationToken);
    }

    public Task SaveAsync(PlayerState player, CancellationToken cancellationToken = default)
        => SaveManyAsync([player], cancellationToken);

    public async Task SaveManyAsync(
        IReadOnlyCollection<PlayerState> players,
        CancellationToken cancellationToken = default)
    {
        if (players.Count == 0)
        {
            return;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var player in players)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO players
                    (user_id, hp, max_hp, mana, max_mana, speed, level, experience, gil,
                     battle_channel_id)
                VALUES
                    (@UserId, @Hp, @MaxHp, @Mana, @MaxMana, @Speed, @Level, @Experience, @Gil,
                     @BattleChannelId)
                ON DUPLICATE KEY UPDATE
                    hp = VALUES(hp), max_hp = VALUES(max_hp),
                    mana = VALUES(mana), max_mana = VALUES(max_mana),
                    speed = VALUES(speed), level = VALUES(level),
                    experience = VALUES(experience), gil = VALUES(gil),
                    battle_channel_id = VALUES(battle_channel_id)
                """,
                new
                {
                    player.UserId,
                    player.Hp,
                    player.MaxHp,
                    player.Mana,
                    player.MaxMana,
                    player.Speed,
                    player.Level,
                    player.Experience,
                    player.Gil,
                    player.BattleChannelId,
                },
                transaction);

            // 子テーブルは「消して入れ直す」。状態異常は毎ターン変わるうえ順序も持つので、
            // 差分更新にするより単純で、行数もたかが知れている。
            await ReplaceSkillsAsync(connection, transaction, player);
            await ReplaceItemsAsync(connection, transaction, player);
            await ReplaceEffectsAsync(connection, transaction, player);
            await ReplacePetAsync(connection, transaction, player);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int?> GetRankAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);

        // 自分より経験値が多い人数 + 1。同値は同順位になる。
        return await connection.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT (SELECT COUNT(*) FROM players p2 WHERE p2.experience > p1.experience) + 1
            FROM players p1
            WHERE p1.user_id = @userId
            """,
            new { userId });
    }

    public async Task<IReadOnlyList<PlayerState>> ListByExperienceDescendingAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);

        var ids = (await connection.QueryAsync<ulong>(
            """
            SELECT user_id FROM players
            ORDER BY experience DESC, user_id ASC
            LIMIT @limit
            """,
            new { limit })).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        // LoadAsync は引数の順序を保つので、並び順はここで決まったものが維持される。
        return await LoadAsync(connection, transaction: null, ids, cancellationToken);
    }

    private static async Task<IReadOnlyList<PlayerState>> LoadAsync(
        MySqlConnection connection,
        DbTransaction? transaction,
        IReadOnlyCollection<ulong> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();

        using var grid = await connection.QueryMultipleAsync(
            """
            SELECT user_id, hp, max_hp, mana, max_mana, speed, level, experience, gil,
                   battle_channel_id
            FROM players WHERE user_id IN @ids;

            -- 習得技は集合として扱う (順序は表示に影響しない) が、
            -- 結果が毎回同じ並びになるよう明示的に整列させる。
            SELECT user_id, skill_name FROM player_skills WHERE user_id IN @ids
            ORDER BY user_id, skill_name;

            SELECT user_id, item_id, quantity FROM player_items WHERE user_id IN @ids;

            SELECT user_id, slot, name, level, turns_left
            FROM player_effects WHERE user_id IN @ids ORDER BY user_id, slot;

            SELECT user_id, name, enemy_code, level, experience, attack_chance, ability
            FROM pets WHERE user_id IN @ids;
            """,
            new { ids },
            transaction);

        var rows = (await grid.ReadAsync<PlayerRow>()).ToDictionary(r => r.user_id);
        var skills = (await grid.ReadAsync<SkillRow>()).ToLookup(r => r.user_id);
        var items = (await grid.ReadAsync<ItemRow>()).ToLookup(r => r.user_id);
        var effects = (await grid.ReadAsync<EffectRow>()).ToLookup(r => r.user_id);
        var pets = (await grid.ReadAsync<PetRow>()).ToDictionary(r => r.user_id);

        cancellationToken.ThrowIfCancellationRequested();

        var result = new List<PlayerState>(rows.Count);

        // 呼び出し側が渡した順序を保つ。存在しないユーザーは飛ばす
        // (旧 source.forEach(ud => { if(!ud) return; ... }) と同じ扱い)。
        foreach (var id in ids)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                continue;
            }

            var player = new PlayerState
            {
                UserId = row.user_id,
                Hp = row.hp,
                MaxHp = row.max_hp,
                Mana = row.mana,
                MaxMana = row.max_mana,
                Speed = row.speed,
                Level = row.level,
                Experience = row.experience,
                Gil = row.gil,
                BattleChannelId = row.battle_channel_id,
            };

            foreach (var skill in skills[id])
            {
                player.Skills.Add(skill.skill_name);
            }

            // 表示順は ItemSlots.Order で決まるので、その順に詰め直す。
            // DB に無いスロットは 0 とし、旧 newdata の形を常に満たすようにする。
            var stored = items[id].ToDictionary(r => r.item_id, r => r.quantity, StringComparer.Ordinal);
            foreach (var slot in ItemSlots.Order)
            {
                player.SetItem(slot, stored.GetValueOrDefault(slot));
            }

            // 上の Order に無いアイテムが将来増えても失われないようにする。
            foreach (var (itemId, quantity) in stored.Where(kv => !player.Items.ContainsKey(kv.Key)))
            {
                player.SetItem(itemId, quantity);
            }

            foreach (var effect in effects[id])
            {
                player.Effects.Add(new ActiveEffect(effect.name, effect.level, effect.turns_left));
            }

            if (pets.TryGetValue(id, out var pet))
            {
                player.Pet = new PetState
                {
                    Name = pet.name,
                    EnemyCode = pet.enemy_code,
                    Level = pet.level,
                    Experience = pet.experience,
                    AttackChance = pet.attack_chance,
                    Ability = pet.ability,
                };
            }

            result.Add(player);
        }

        return result;
    }

    private static async Task ReplaceSkillsAsync(
        MySqlConnection connection,
        DbTransaction transaction,
        PlayerState player)
    {
        await connection.ExecuteAsync(
            "DELETE FROM player_skills WHERE user_id = @UserId",
            new { player.UserId },
            transaction);

        if (player.Skills.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(
            "INSERT INTO player_skills (user_id, skill_name) VALUES (@UserId, @SkillName)",
            player.Skills.Distinct(StringComparer.Ordinal)
                .Select(s => new { player.UserId, SkillName = s }),
            transaction);
    }

    private static async Task ReplaceItemsAsync(
        MySqlConnection connection,
        DbTransaction transaction,
        PlayerState player)
    {
        await connection.ExecuteAsync(
            "DELETE FROM player_items WHERE user_id = @UserId",
            new { player.UserId },
            transaction);

        if (player.Items.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(
            "INSERT INTO player_items (user_id, item_id, quantity) VALUES (@UserId, @ItemId, @Quantity)",
            player.Items.Select(kv => new { player.UserId, ItemId = kv.Key, Quantity = kv.Value }),
            transaction);
    }

    private static async Task ReplaceEffectsAsync(
        MySqlConnection connection,
        DbTransaction transaction,
        PlayerState player)
    {
        await connection.ExecuteAsync(
            "DELETE FROM player_effects WHERE user_id = @UserId",
            new { player.UserId },
            transaction);

        if (player.Effects.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO player_effects (user_id, slot, name, level, turns_left)
            VALUES (@UserId, @Slot, @Name, @Level, @TurnsLeft)
            """,
            player.Effects.Select((e, i) => new
            {
                player.UserId,
                Slot = i,
                e.Name,
                e.Level,
                e.TurnsLeft,
            }),
            transaction);
    }

    private static async Task ReplacePetAsync(
        MySqlConnection connection,
        DbTransaction transaction,
        PlayerState player)
    {
        if (player.Pet is null)
        {
            // 旧 player.p[0] = undefined (逃がした) に相当。
            await connection.ExecuteAsync(
                "DELETE FROM pets WHERE user_id = @UserId",
                new { player.UserId },
                transaction);
            return;
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO pets
                (user_id, name, enemy_code, level, experience, attack_chance, ability)
            VALUES
                (@UserId, @Name, @EnemyCode, @Level, @Experience, @AttackChance, @Ability)
            ON DUPLICATE KEY UPDATE
                name = VALUES(name), enemy_code = VALUES(enemy_code),
                level = VALUES(level), experience = VALUES(experience),
                attack_chance = VALUES(attack_chance), ability = VALUES(ability)
            """,
            new
            {
                player.UserId,
                player.Pet.Name,
                player.Pet.EnemyCode,
                player.Pet.Level,
                player.Pet.Experience,
                player.Pet.AttackChance,
                player.Pet.Ability,
            },
            transaction);
    }

    // Dapper のマッピング用。列名そのままにしてある。
#pragma warning disable IDE1006, SA1300 // 列名に合わせるため小文字
    private sealed record PlayerRow(
        ulong user_id, long hp, long max_hp, long mana, long max_mana, double speed,
        int level, long experience, long gil, ulong? battle_channel_id);

    private sealed record SkillRow(ulong user_id, string skill_name);

    private sealed record ItemRow(ulong user_id, string item_id, int quantity);

    private sealed record EffectRow(ulong user_id, int slot, string name, int level, int turns_left);

    private sealed record PetRow(
        ulong user_id, string name, string enemy_code, int level, long experience,
        int attack_chance, string ability);
#pragma warning restore IDE1006, SA1300
}
