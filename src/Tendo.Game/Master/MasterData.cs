namespace Tendo.Game.Master;

/// <summary>
/// 旧 <c>ea.js</c> 冒頭の <c>const sk = require("./mmo/skill.js")</c> 一式に相当する。
/// 起動時に一度だけ読み込み、以後は不変。全体で 1 インスタンスを共有する。
/// </summary>
public sealed class MasterData
{
    private readonly Dictionary<string, SkillDef> _skillsByName;
    private readonly Dictionary<string, EnemyDef> _enemiesByCode;
    private readonly Dictionary<string, EffectDef> _effectsByName;
    private readonly Dictionary<string, AbilityDef> _abilitiesByName;
    private readonly Dictionary<string, FieldDef> _fieldsByName;
    private readonly Dictionary<string, DifficultyDef> _difficultiesByName;
    private readonly Dictionary<string, AffinityDef> _affinitiesByName;
    private readonly Dictionary<string, ItemInfoDef> _itemInfoById;

    public MasterData(
        IReadOnlyList<EnemyDef> enemies,
        IReadOnlyList<SkillDef> skills,
        IReadOnlyList<EffectDef> effects,
        IReadOnlyList<AbilityDef> abilities,
        IReadOnlyList<FieldDef> fields,
        IReadOnlyList<FieldRequirementDef> fieldRequirements,
        IReadOnlyList<DifficultyDef> difficulties,
        IReadOnlyList<AffinityDef> affinities,
        IReadOnlyList<ShopDef> shops,
        IReadOnlyList<string> tips,
        IReadOnlyList<ItemInfoDef> itemInfo,
        IReadOnlyDictionary<string, string> itemNames,
        FixDef fix)
    {
        Enemies = enemies;
        Skills = skills;
        Effects = effects;
        Abilities = abilities;
        Fields = fields;
        FieldRequirements = fieldRequirements;
        Difficulties = difficulties;
        Affinities = affinities;
        Shops = shops;
        Tips = tips;
        ItemInfo = itemInfo;
        ItemNames = itemNames;
        Fix = fix;

        // 旧実装は毎回 Array.find で線形探索していた。索引に置き換えても
        // 結果は変わらないが、重複キーがあると挙動が変わるので構築時に検出する
        // (find は先頭一致なので、重複があれば先頭が勝つ = 下の FirstOrDefault と同じ)。
        _skillsByName = BuildIndex(skills, s => s.Name);
        _enemiesByCode = BuildIndex(enemies, e => e.Code);
        _effectsByName = BuildIndex(effects, e => e.Name);
        _abilitiesByName = BuildIndex(abilities, a => a.Name);
        _fieldsByName = BuildIndex(fields, f => f.Name);
        _difficultiesByName = BuildIndex(difficulties, d => d.Name);
        _affinitiesByName = BuildIndex(affinities, a => a.Name);
        _itemInfoById = BuildIndex(itemInfo, i => i.Id);

        LearnableSkills = [.. skills.Where(s => s.IsLearnable)];
    }

    public IReadOnlyList<EnemyDef> Enemies { get; }

    public IReadOnlyList<SkillDef> Skills { get; }

    /// <summary>旧 <c>const learnable = sk.filter(s =&gt; s.learn != Infinity)</c>。</summary>
    public IReadOnlyList<SkillDef> LearnableSkills { get; }

    public IReadOnlyList<EffectDef> Effects { get; }

    public IReadOnlyList<AbilityDef> Abilities { get; }

    public IReadOnlyList<FieldDef> Fields { get; }

    public IReadOnlyList<FieldRequirementDef> FieldRequirements { get; }

    public IReadOnlyList<DifficultyDef> Difficulties { get; }

    public IReadOnlyList<AffinityDef> Affinities { get; }

    public IReadOnlyList<ShopDef> Shops { get; }

    /// <summary>旧 <c>mmo/tips.js</c>。<c>/tips</c> が引く。</summary>
    public IReadOnlyList<string> Tips { get; }

    public IReadOnlyList<ItemInfoDef> ItemInfo { get; }

    /// <summary>旧 <c>mmo/item.json</c>。アイテム ID → 表示名。</summary>
    public IReadOnlyDictionary<string, string> ItemNames { get; }

    public FixDef Fix { get; }

    /// <summary>旧 <c>sk.find(s =&gt; s.name == name)</c>。</summary>
    public SkillDef? FindSkill(string name) => _skillsByName.GetValueOrDefault(name);

    /// <summary>旧 <c>en.find(e =&gt; e.code == code)</c>。</summary>
    public EnemyDef? FindEnemy(string code) => _enemiesByCode.GetValueOrDefault(code);

    /// <summary>旧 <c>en.find(e =&gt; e.name == a || e.code == a)</c> (<c>/mod sum</c> 用)。</summary>
    public EnemyDef? FindEnemyByNameOrCode(string nameOrCode)
        => Enemies.FirstOrDefault(e => e.Name == nameOrCode || e.Code == nameOrCode);

    /// <summary>旧 <c>ef.find(f =&gt; f.name == name)</c>。</summary>
    public EffectDef? FindEffect(string name) => _effectsByName.GetValueOrDefault(name);

    /// <summary>旧 <c>ab.find(a =&gt; a.name == name)</c>。</summary>
    public AbilityDef? FindAbility(string name) => _abilitiesByName.GetValueOrDefault(name);

    public FieldDef? FindField(string name) => _fieldsByName.GetValueOrDefault(name);

    /// <summary>
    /// 有効敵Lv。戦闘・報酬の計算はすべてこの値を使う。
    ///
    /// <c>battle.Level</c> は「このチャンネルが積み上げた撃破数」であってフィールドを
    /// またいでも減らない。一方フィールドには <see cref="FieldDef.LevelCap"/> があり、
    /// 上位から下位へ降りると有効敵Lvが下位の上限に張り付く (敵は弱いが報酬も頭打ち)。
    /// 上位へ戻れば <c>battle.Level</c> がそのまま効くので進行は失われない。
    /// </summary>
    public int EffectiveLevel(string field, int level)
        => Math.Min(level, FindField(field)?.LevelCap ?? int.MaxValue);

    /// <summary>旧 <c>df.find(d =&gt; d.name == name)</c>。</summary>
    public DifficultyDef? FindDifficulty(string name) => _difficultiesByName.GetValueOrDefault(name);

    /// <summary>旧 <c>as.find(a =&gt; a.name == zokusei)</c>。</summary>
    public AffinityDef? FindAffinity(string element) => _affinitiesByName.GetValueOrDefault(element);

    public ItemInfoDef? FindItemInfo(string id) => _itemInfoById.GetValueOrDefault(id);

    /// <summary>旧 <c>sh.find(s =&gt; s.field == field)</c>。重複定義があるので先頭一致で引く。</summary>
    public ShopDef? FindShop(string field) => Shops.FirstOrDefault(s => s.Field == field);

    /// <summary>旧 <c>df[0]</c>。難易度不明時のフォールバック。</summary>
    public DifficultyDef DefaultDifficulty => Difficulties[0];

    /// <summary>フィールド不明時のフォールバック。倍率も上限も既定 (等倍・上限なし) になる。</summary>
    public FieldDef DefaultField { get; } = new() { Name = string.Empty };

    /// <summary>旧 <c>ab[0]</c> (「なし」)。アビリティ不明時のフォールバック。</summary>
    public AbilityDef DefaultAbility => Abilities[0];

    private static Dictionary<string, T> BuildIndex<T>(
        IReadOnlyList<T> source,
        Func<T, string> keySelector)
    {
        var index = new Dictionary<string, T>(source.Count, StringComparer.Ordinal);
        foreach (var item in source)
        {
            // 旧 find は先頭一致。後勝ちにすると挙動が変わるので TryAdd を使う。
            index.TryAdd(keySelector(item), item);
        }

        return index;
    }
}
