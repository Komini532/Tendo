using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tendo.Game.Master;

/// <summary>
/// 状態異常の付与指定。旧データの <c>[状態異常名, レベル, 持続ターン, 確率]</c>。
/// <c>skill.effect</c> (敵に付与) / <c>skill.self</c> (自分に付与) /
/// <c>difficulty.eff</c> / <c>ability.eff</c> で使われる。
/// </summary>
[JsonConverter(typeof(StatusApplicationConverter))]
public sealed record StatusApplication(string Name, int Level, int Turns, int Percent)
{
    /// <summary>
    /// 旧 <c>e.slice(0, 3)</c>。確率判定に当たった後、状態として積むときの形。
    /// </summary>
    public StatusState ToState() => new(Name, Level, Turns);
}

/// <summary>
/// 実際に付いている状態異常。旧データ/旧 DB の <c>[状態異常名, レベル, 残りターン]</c>。
/// <c>enemy.effect</c> (出現時の初期状態) と、プレイヤー・敵の <c>eff</c> 配列がこの形。
/// </summary>
[JsonConverter(typeof(StatusStateConverter))]
public sealed record StatusState(string Name, int Level, int TurnsLeft);

/// <summary>
/// 状態異常の解除指定。旧 <c>skill.pair</c> の <c>[状態異常名, レベル, 確率]</c>。
/// 対象が持つ状態のレベルが <see cref="Level"/> 以下なら解除できる。
/// </summary>
[JsonConverter(typeof(EffectClearConverter))]
public sealed record EffectClear(string Name, int Level, int Percent);

/// <summary>
/// ドロップ品。旧 <c>enemy.i</c> の <c>[アイテムID, 個数, 確率]</c>。
/// </summary>
[JsonConverter(typeof(ItemDropConverter))]
public sealed record ItemDrop(string ItemId, int Count, int Percent);

/// <summary>
/// 旧データは全て「先頭が文字列、以降が数値」の配列。
/// System.Text.Json の既定では record にマップできないので専用に読む。
/// </summary>
internal abstract class TupleConverter<T> : JsonConverter<T>
{
    protected abstract int MinLength { get; }

    protected abstract T Create(string name, IReadOnlyList<int> numbers);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException(
                $"{typeof(T).Name} は JSON 配列である必要があります (実際: {reader.TokenType})。");
        }

        reader.Read();
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"{typeof(T).Name} の 1 要素目は文字列である必要があります。");
        }

        var name = reader.GetString() ?? string.Empty;
        var numbers = new List<int>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            // 旧データには 1/32 のような小数は現れないが、念のため丸めて受ける。
            numbers.Add(reader.TokenType switch
            {
                JsonTokenType.Number => (int)Math.Floor(reader.GetDouble() + 0.5),
                _ => throw new JsonException(
                    $"{typeof(T).Name} の 2 要素目以降は数値である必要があります (実際: {reader.TokenType})。"),
            });
        }

        if (numbers.Count < MinLength)
        {
            throw new JsonException(
                $"{typeof(T).Name} \"{name}\" の要素数が足りません " +
                $"(数値 {numbers.Count} 個、必要 {MinLength} 個)。");
        }

        return Create(name, numbers);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => throw new NotSupportedException("マスターデータは読み込み専用です。");
}

internal sealed class StatusApplicationConverter : TupleConverter<StatusApplication>
{
    protected override int MinLength => 3;

    protected override StatusApplication Create(string name, IReadOnlyList<int> n)
        => new(name, n[0], n[1], n[2]);
}

internal sealed class StatusStateConverter : TupleConverter<StatusState>
{
    protected override int MinLength => 2;

    protected override StatusState Create(string name, IReadOnlyList<int> n)
        => new(name, n[0], n[1]);
}

internal sealed class EffectClearConverter : TupleConverter<EffectClear>
{
    protected override int MinLength => 2;

    protected override EffectClear Create(string name, IReadOnlyList<int> n)
        => new(name, n[0], n[1]);
}

internal sealed class ItemDropConverter : TupleConverter<ItemDrop>
{
    protected override int MinLength => 2;

    protected override ItemDrop Create(string name, IReadOnlyList<int> n)
        => new(name, n[0], n[1]);
}
