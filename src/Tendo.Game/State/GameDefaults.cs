using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tendo.Game.State;

/// <summary>
/// 旧 <c>mmo/newdata.js</c> の移植。新規プレイヤー / 新規戦場 / 新規ペットの初期値。
/// 値は <c>data/defaults.json</c> (dump スクリプトが生成) から読む。
/// </summary>
public sealed class GameDefaults
{
    private readonly DefaultsDocument _document;

    private GameDefaults(DefaultsDocument document)
    {
        _document = document;
    }

    /// <summary>旧 <c>newdata("user")</c>。</summary>
    public PlayerState NewPlayer(ulong userId)
    {
        var d = _document.User;
        var player = new PlayerState
        {
            UserId = userId,
            Hp = d.Hp,
            MaxHp = d.MaxHp,
            Mana = d.Mana,
            MaxMana = d.MaxMana,
            Speed = d.Speed,
            Level = d.Level,
            Experience = d.Experience,
            Gil = d.Gil,
            // 旧 n:"0" は「非戦闘中」。
            BattleChannelId = null,
        };

        // 表示順が出るので、defaults.json のキー順ではなく
        // 明示した ItemSlots.Order の順で詰める (両者が一致することはテストで固定)。
        foreach (var slot in ItemSlots.Order)
        {
            player.SetItem(slot, d.Items.GetValueOrDefault(slot));
        }

        return player;
    }

    /// <summary>
    /// 旧 <c>newdata("channel")</c>。
    ///
    /// 敵の中身 (code / HP / 状態異常) はここでは決まらない。旧 <c>fn()</c> も
    /// 生成直後に草原のレア度 2 以外の敵から抽選して埋めていた。その抽選は
    /// Phase 4 の出現処理が行う。
    /// </summary>
    public BattleState NewBattle(ulong channelId)
    {
        var d = _document.Channel;
        return new BattleState
        {
            ChannelId = channelId,
            Field = d.Field,
            Difficulty = d.Difficulty,
            EnemyCode = d.EnemyCode,
            Level = d.Level,
            Hp = d.Hp,
            MaxHp = d.MaxHp,
            Mana = d.Mana,
            MaxMana = d.MaxMana,
            ChecksumChannelId = channelId,
        };
    }

    /// <summary>旧 <c>newdata("pet")</c>。</summary>
    public PetState NewPet()
    {
        var d = _document.Pet;
        return new PetState
        {
            Name = d.Name,
            EnemyCode = d.EnemyCode,
            Level = d.Level,
            Experience = d.Experience,
            AttackChance = d.AttackChance,
            Ability = d.Ability,
        };
    }

    /// <summary>defaults.json に書かれているアイテム欄のキー順。テストでの照合用。</summary>
    public IReadOnlyList<string> ItemSlotOrderFromData => _document.User.Items.Keys.ToList();

    public static GameDefaults Load(string? directory = null)
    {
        directory ??= Path.Combine(AppContext.BaseDirectory, "data");
        var path = Path.Combine(directory, "defaults.json");

        if (!File.Exists(path))
        {
            throw new Master.MasterDataException(
                $"初期値データが見つかりません: {path}{Environment.NewLine}" +
                "`node tools/dump-master-data.js` を実行して生成してください。");
        }

        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize<DefaultsDocument>(stream)
                       ?? throw new Master.MasterDataException("defaults.json の中身が null です。");

        return new GameDefaults(document);
    }

    private sealed record DefaultsDocument
    {
        [JsonPropertyName("channel")]
        public ChannelDefaults Channel { get; init; } = new();

        [JsonPropertyName("user")]
        public UserDefaults User { get; init; } = new();

        [JsonPropertyName("pet")]
        public PetDefaults Pet { get; init; } = new();
    }

    private sealed record ChannelDefaults
    {
        [JsonPropertyName("f")]
        public string Field { get; init; } = "草原";

        [JsonPropertyName("d")]
        public string Difficulty { get; init; } = "NORMAL";

        [JsonPropertyName("c")]
        public string EnemyCode { get; init; } = string.Empty;

        [JsonPropertyName("lv")]
        public int Level { get; init; } = 1;

        [JsonPropertyName("hp")]
        public long Hp { get; init; }

        [JsonPropertyName("mhp")]
        public long MaxHp { get; init; }

        [JsonPropertyName("mp")]
        public long Mana { get; init; }

        [JsonPropertyName("mmp")]
        public long MaxMana { get; init; }
    }

    private sealed record UserDefaults
    {
        [JsonPropertyName("hp")]
        public long Hp { get; init; }

        [JsonPropertyName("mhp")]
        public long MaxHp { get; init; }

        [JsonPropertyName("mp")]
        public long Mana { get; init; }

        [JsonPropertyName("mmp")]
        public long MaxMana { get; init; }

        [JsonPropertyName("spd")]
        public double Speed { get; init; } = 1;

        [JsonPropertyName("lv")]
        public int Level { get; init; } = 1;

        [JsonPropertyName("xp")]
        public long Experience { get; init; }

        [JsonPropertyName("g")]
        public long Gil { get; init; }

        [JsonPropertyName("i")]
        public Dictionary<string, int> Items { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed record PetDefaults
    {
        [JsonPropertyName("n")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("c")]
        public string EnemyCode { get; init; } = string.Empty;

        [JsonPropertyName("lv")]
        public int Level { get; init; } = 1;

        [JsonPropertyName("xp")]
        public long Experience { get; init; }

        [JsonPropertyName("p")]
        public int AttackChance { get; init; }

        [JsonPropertyName("a")]
        public string Ability { get; init; } = string.Empty;
    }
}
