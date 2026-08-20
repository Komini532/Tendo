using System.Text.Json;

namespace Tendo.Game.Master;

/// <summary>マスターデータの読み込み・検証に失敗したときに投げる。</summary>
public sealed class MasterDataException : Exception
{
    public MasterDataException(string message) : base(message)
    {
    }

    public MasterDataException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// <c>data/*.json</c> (<c>tools/dump-master-data.js</c> が生成) を読み込む。
/// </summary>
public static class MasterDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>既定の配置場所。実行ファイルと同じ場所の <c>data/</c>。</summary>
    public static string DefaultDirectory
        => Path.Combine(AppContext.BaseDirectory, "data");

    /// <summary>
    /// <c>data/*.json</c> を読み込んで検証する。
    /// </summary>
    /// <param name="directory">既定は実行ファイル横の <c>data/</c>。</param>
    /// <param name="warnings">挙動に影響しない指摘 (旧データ由来の綴り間違いなど)。</param>
    public static MasterData Load(string? directory, out IReadOnlyList<string> warnings)
    {
        var data = LoadCore(directory);
        warnings = Validate(data);
        return data;
    }

    /// <inheritdoc cref="Load(string?, out IReadOnlyList{string})" />
    public static MasterData Load(string? directory = null)
        => Load(directory, out _);

    private static MasterData LoadCore(string? directory)
    {
        directory ??= DefaultDirectory;

        if (!Directory.Exists(directory))
        {
            throw new MasterDataException(
                $"マスターデータのディレクトリが見つかりません: {directory}{Environment.NewLine}" +
                "`node tools/dump-master-data.js` を実行して data/*.json を生成してください。");
        }

        var data = new MasterData(
            enemies: ReadList<EnemyDef>(directory, "enemies.json"),
            skills: ReadList<SkillDef>(directory, "skills.json"),
            effects: ReadList<EffectDef>(directory, "effects.json"),
            abilities: ReadList<AbilityDef>(directory, "abilities.json"),
            fields: ReadList<FieldDef>(directory, "fields.json"),
            fieldRequirements: ReadList<FieldRequirementDef>(directory, "field-requirements.json"),
            difficulties: ReadList<DifficultyDef>(directory, "difficulties.json"),
            affinities: ReadList<AffinityDef>(directory, "affinities.json"),
            shops: ReadList<ShopDef>(directory, "shops.json"),
            tips: ReadList<string>(directory, "tips.json"),
            itemInfo: ReadList<ItemInfoDef>(directory, "item-info.json"),
            itemNames: Read<Dictionary<string, string>>(directory, "items.json"),
            fix: Read<FixDef>(directory, "fix.json"));

        return data;
    }

    /// <summary>
    /// 参照整合性を起動時に確かめる。
    ///
    /// 旧実装は参照が壊れていても実行時に <c>undefined</c> が伝播して
    /// 妙な表示になるだけだった。C# 側では起動時に落として気付けるようにする。
    ///
    /// ただし旧データに元から入っている「無害な壊れ方」まで落とすと移植できないので、
    /// 挙動に影響しないものは戻り値の警告として返し、起動は止めない。
    /// </summary>
    /// <returns>挙動には影響しないが記録しておくべき指摘。</returns>
    /// <exception cref="MasterDataException">挙動が壊れる参照があるとき。</exception>
    public static IReadOnlyList<string> Validate(MasterData data)
    {
        var problems = new List<string>();
        var warnings = new List<string>();

        foreach (var enemy in data.Enemies)
        {
            // 名前が空の要素は旧データの書きかけ (「双頭龍」の 3 つ目)。
            // per が 0 なので抽選されることはなく、旧実装でも無害だった。エラーにしない。
            foreach (var skill in enemy.Skills
                         .Where(s => !string.IsNullOrEmpty(s.Name))
                         .Where(s => data.FindSkill(s.Name) is null))
            {
                problems.Add($"敵「{enemy.Name}」({enemy.Code}) が未定義の技「{skill.Name}」を参照しています。");
            }

            foreach (var effect in enemy.InitialEffects.Where(e => data.FindEffect(e.Name) is null))
            {
                problems.Add($"敵「{enemy.Name}」({enemy.Code}) が未定義の状態異常「{effect.Name}」を参照しています。");
            }

            foreach (var field in enemy.Fields.Where(f => data.FindField(f) is null))
            {
                problems.Add($"敵「{enemy.Name}」({enemy.Code}) が未定義のフィールド「{field}」を参照しています。");
            }

            if (enemy.Next is { } next && data.FindEnemy(next.Code) is null)
            {
                problems.Add($"敵「{enemy.Name}」の next が未定義の敵「{next.Code}」を指しています。");
            }

            if (enemy.OnlyAbility is { } only && data.FindAbility(only) is null)
            {
                problems.Add($"敵「{enemy.Name}」が未定義の固有アビリティ「{only}」を参照しています。");
            }

            foreach (var drop in enemy.Drops.Where(d => !data.ItemNames.ContainsKey(d.ItemId)))
            {
                problems.Add($"敵「{enemy.Name}」が未定義のアイテム「{drop.ItemId}」をドロップします。");
            }
        }

        foreach (var skill in data.Skills)
        {
            foreach (var name in skill.Effects.Select(e => e.Name)
                         .Concat(skill.SelfEffects.Select(e => e.Name))
                         .Concat(skill.Clears.Select(c => c.Name))
                         .Where(n => data.FindEffect(n) is null))
            {
                problems.Add($"技「{skill.Name}」が未定義の状態異常「{name}」を参照しています。");
            }

            if (skill.MoveField is { Length: > 0 } move && data.FindField(move) is null)
            {
                problems.Add($"技「{skill.Name}」が未定義のフィールド「{move}」へ移動させようとしています。");
            }
        }

        foreach (var effect in data.Effects)
        {
            foreach (var name in effect.Protects.Where(p => data.FindEffect(p) is null))
            {
                // 耐性リストの綴り間違いは無害。旧データには「火傷耐性」が存在しない
                // 「大火傷」を守る指定が実際に入っている。
                // 耐性判定は「これから付与しようとしている状態異常名」との突き合わせなので、
                // 存在しない名前は決して一致せず、指定が無視されるだけ。
                // 将来の打ち間違いに気付けるよう記録はするが、起動は止めない。
                warnings.Add(
                    $"状態異常「{effect.Name}」の protect が未定義の「{name}」を含んでいます " +
                    "(一致しないため無視されます)。");
            }
        }

        foreach (var difficulty in data.Difficulties)
        {
            foreach (var name in difficulty.Effects.Select(e => e.Name)
                         .Where(n => data.FindEffect(n) is null))
            {
                problems.Add($"難易度「{difficulty.Name}」が未定義の状態異常「{name}」を参照しています。");
            }
        }

        foreach (var shop in data.Shops)
        {
            if (data.FindField(shop.Field) is null)
            {
                problems.Add($"ショップが未定義のフィールド「{shop.Field}」に置かれています。");
            }

            foreach (var item in shop.Items.Where(i => !data.ItemNames.ContainsKey(i.ItemId)))
            {
                problems.Add($"「{shop.Field}」のショップが未定義のアイテム「{item.ItemId}」を売っています。");
            }
        }

        foreach (var requirement in data.FieldRequirements)
        {
            foreach (var name in requirement.ConnectedFrom.Append(requirement.Name)
                         .Where(n => data.FindField(n) is null))
            {
                problems.Add($"移動条件「{requirement.Name}」が未定義のフィールド「{name}」を参照しています。");
            }
        }

        foreach (var info in data.ItemInfo.Where(i => !data.ItemNames.ContainsKey(i.Id)))
        {
            problems.Add($"アイテム情報「{info.Name}」の ID「{info.Id}」が items.json にありません。");
        }

        if (data.Difficulties.Count == 0)
        {
            problems.Add("難易度が 1 件も定義されていません。");
        }

        if (data.Abilities.Count == 0)
        {
            problems.Add("アビリティが 1 件も定義されていません。");
        }

        if (problems.Count > 0)
        {
            throw new MasterDataException(
                $"マスターデータの参照が壊れています ({problems.Count} 件):{Environment.NewLine}" +
                string.Join(Environment.NewLine, problems.Select(p => "  - " + p)));
        }

        return warnings;
    }

    private static IReadOnlyList<T> ReadList<T>(string directory, string fileName)
        => Read<List<T>>(directory, fileName);

    private static T Read<T>(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);

        if (!File.Exists(path))
        {
            throw new MasterDataException(
                $"マスターデータが見つかりません: {path}{Environment.NewLine}" +
                "`node tools/dump-master-data.js` を実行して生成してください。");
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options)
                   ?? throw new MasterDataException($"{fileName} の中身が null です。");
        }
        catch (JsonException ex)
        {
            throw new MasterDataException($"{fileName} の読み込みに失敗しました: {ex.Message}", ex);
        }
    }
}
