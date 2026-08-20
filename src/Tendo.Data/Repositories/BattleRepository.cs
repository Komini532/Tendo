using System.Data.Common;
using Dapper;
using MySqlConnector;
using Tendo.Game.State;

namespace Tendo.Data.Repositories;

/// <inheritdoc />
public sealed class BattleRepository : IBattleRepository
{
    private readonly IDbConnectionFactory _connections;
    private readonly GameDefaults _defaults;

    public BattleRepository(IDbConnectionFactory connections, GameDefaults defaults)
    {
        _connections = connections;
        _defaults = defaults;
    }

    public async Task<BattleState?> FindAsync(
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var found = await LoadAsync(connection, transaction: null, [channelId], cancellationToken);
        return found.Count > 0 ? found[0] : null;
    }

    public async Task<BattleState> GetOrCreateAsync(
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(channelId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = _defaults.NewBattle(channelId);
        await SaveAsync(created, cancellationToken);

        return await FindAsync(channelId, cancellationToken) ?? created;
    }

    public async Task SaveAsync(BattleState battle, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
            INSERT INTO battles
                (channel_id, field, difficulty, enemy_code, level, hp, max_hp, mana, max_mana,
                 turn, checksum_channel_id)
            VALUES
                (@ChannelId, @Field, @Difficulty, @EnemyCode, @Level, @Hp, @MaxHp, @Mana, @MaxMana,
                 @Turn, @ChecksumChannelId)
            ON DUPLICATE KEY UPDATE
                field = VALUES(field), difficulty = VALUES(difficulty),
                enemy_code = VALUES(enemy_code), level = VALUES(level),
                hp = VALUES(hp), max_hp = VALUES(max_hp),
                mana = VALUES(mana), max_mana = VALUES(max_mana),
                turn = VALUES(turn), checksum_channel_id = VALUES(checksum_channel_id)
            """,
            new
            {
                battle.ChannelId,
                battle.Field,
                battle.Difficulty,
                battle.EnemyCode,
                battle.Level,
                battle.Hp,
                battle.MaxHp,
                battle.Mana,
                battle.MaxMana,
                battle.Turn,
                battle.ChecksumChannelId,
            },
            transaction);

        await ReplaceEffectsAsync(connection, transaction, battle);
        await ReplaceIdListAsync(
            connection, transaction, "battle_participants", battle.ChannelId, battle.Participants);
        await ReplaceIdListAsync(
            connection, transaction, "battle_chocolate_feeders", battle.ChannelId, battle.ChocolateFeeders);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BattleState>> ListByLevelDescendingAsync(
        int limit,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);

        var ids = (await connection.QueryAsync<ulong>(
            """
            SELECT channel_id FROM battles
            ORDER BY level DESC, channel_id ASC
            LIMIT @limit OFFSET @offset
            """,
            new { limit, offset })).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return await LoadAsync(connection, transaction: null, ids, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM battles");
    }

    private static async Task<IReadOnlyList<BattleState>> LoadAsync(
        MySqlConnection connection,
        DbTransaction? transaction,
        IReadOnlyCollection<ulong> channelIds,
        CancellationToken cancellationToken)
    {
        var ids = channelIds.Distinct().ToArray();

        using var grid = await connection.QueryMultipleAsync(
            """
            SELECT channel_id, field, difficulty, enemy_code, level, hp, max_hp, mana, max_mana,
                   turn, checksum_channel_id
            FROM battles WHERE channel_id IN @ids;

            SELECT channel_id, slot, name, level, turns_left
            FROM battle_effects WHERE channel_id IN @ids ORDER BY channel_id, slot;

            SELECT channel_id, user_id FROM battle_participants WHERE channel_id IN @ids;

            SELECT channel_id, user_id FROM battle_chocolate_feeders WHERE channel_id IN @ids;
            """,
            new { ids },
            transaction);

        var rows = (await grid.ReadAsync<BattleRow>()).ToDictionary(r => r.channel_id);
        var effects = (await grid.ReadAsync<EffectRow>()).ToLookup(r => r.channel_id);
        var participants = (await grid.ReadAsync<MemberRow>()).ToLookup(r => r.channel_id);
        var feeders = (await grid.ReadAsync<MemberRow>()).ToLookup(r => r.channel_id);

        cancellationToken.ThrowIfCancellationRequested();

        var result = new List<BattleState>(rows.Count);

        foreach (var id in ids)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                continue;
            }

            var battle = new BattleState
            {
                ChannelId = row.channel_id,
                Field = row.field,
                Difficulty = row.difficulty,
                EnemyCode = row.enemy_code,
                Level = row.level,
                Hp = row.hp,
                MaxHp = row.max_hp,
                Mana = row.mana,
                MaxMana = row.max_mana,
                Turn = row.turn,
                ChecksumChannelId = row.checksum_channel_id,
            };

            foreach (var effect in effects[id])
            {
                battle.Effects.Add(new ActiveEffect(effect.name, effect.level, effect.turns_left));
            }

            battle.Participants.AddRange(participants[id].Select(p => p.user_id));
            battle.ChocolateFeeders.AddRange(feeders[id].Select(p => p.user_id));

            result.Add(battle);
        }

        return result;
    }

    private static async Task ReplaceEffectsAsync(
        MySqlConnection connection,
        DbTransaction transaction,
        BattleState battle)
    {
        await connection.ExecuteAsync(
            "DELETE FROM battle_effects WHERE channel_id = @ChannelId",
            new { battle.ChannelId },
            transaction);

        if (battle.Effects.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO battle_effects (channel_id, slot, name, level, turns_left)
            VALUES (@ChannelId, @Slot, @Name, @Level, @TurnsLeft)
            """,
            battle.Effects.Select((e, i) => new
            {
                battle.ChannelId,
                Slot = i,
                e.Name,
                e.Level,
                e.TurnsLeft,
            }),
            transaction);
    }

    private static async Task ReplaceIdListAsync(
        MySqlConnection connection,
        DbTransaction transaction,
        string table,
        ulong channelId,
        IReadOnlyCollection<ulong> userIds)
    {
        await connection.ExecuteAsync(
            $"DELETE FROM {table} WHERE channel_id = @channelId",
            new { channelId },
            transaction);

        var distinct = userIds.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(
            $"INSERT INTO {table} (channel_id, user_id) VALUES (@channelId, @userId)",
            distinct.Select(userId => new { channelId, userId }),
            transaction);
    }

#pragma warning disable IDE1006, SA1300 // 列名に合わせるため小文字
    private sealed record BattleRow(
        ulong channel_id, string field, string difficulty, string enemy_code, int level,
        long hp, long max_hp, long mana, long max_mana, int turn, ulong? checksum_channel_id);

    private sealed record EffectRow(ulong channel_id, int slot, string name, int level, int turns_left);

    private sealed record MemberRow(ulong channel_id, ulong user_id);
#pragma warning restore IDE1006, SA1300
}
