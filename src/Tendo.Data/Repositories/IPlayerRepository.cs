using Tendo.Game.State;

namespace Tendo.Data.Repositories;

/// <summary>
/// 旧 <c>ctrl.read(userId)</c> / <c>ctrl.write(userId, r.ots(player))</c> の置き換え。
///
/// 旧実装は「丸ごと読む → メモリ上で書き換える → 丸ごと書き戻す」形なので、
/// 集約単位で読み書きするインターフェースにしてある。
/// </summary>
public interface IPlayerRepository
{
    /// <summary>旧 <c>ctrl.read</c>。存在しなければ null。</summary>
    Task<PlayerState?> FindAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 旧 <c>fn()</c> 冒頭の「データがなければ <c>ctrl.new</c> して処理をやり直す」に相当。
    /// </summary>
    Task<PlayerState> GetOrCreateAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>複数人を一度に読む。旧 <c>ctrl.multi(ids, ctrl.read)</c>。</summary>
    /// <remarks>
    /// 旧実装は 1 件ずつ直列に読んでいたが、結果は同じなので 1 クエリにまとめる。
    /// 戻り値は引数の順序を保ち、存在しないユーザーは含まれない
    /// (旧 <c>source.forEach(ud =&gt; { if(!ud) return; ... })</c> と同じ扱い)。
    /// </remarks>
    Task<IReadOnlyList<PlayerState>> FindManyAsync(
        IReadOnlyCollection<ulong> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>旧 <c>ctrl.write</c>。集約を丸ごと保存する。</summary>
    Task SaveAsync(PlayerState player, CancellationToken cancellationToken = default);

    /// <summary>複数人を 1 トランザクションで保存する。撃破報酬の配布で使う。</summary>
    Task SaveManyAsync(
        IReadOnlyCollection<PlayerState> players,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 旧 <c>ctrl.prank</c>。経験値降順での順位 (1 始まり)。存在しなければ null。
    ///
    /// 旧実装は <c>row.slice(0, 100)</c> を「並べ替える前」に行っていた
    /// (ea.js:266-275)。つまり上位 100 人ではなく、テーブルに入っている順で先頭 100 行を
    /// 取り、その中だけで並べ替えて順位を出していた。101 行目以降に登録された人は
    /// 対象に入らず reject され、<c>/status</c> に順位行が出なかった。
    ///
    /// 登録順で先頭 100 行という切り方は「[順位]」という表示の意味と食い違っており、
    /// しかも新しい MySQL では再現しようがない (SQLite の rowid 順に相当するものがない)。
    /// ここは全件での順位を返す。登録者が 100 人以下の間は旧実装と完全に一致し、
    /// それを超えた場合も表示書式は変わらない。
    /// </summary>
    Task<int?> GetRankAsync(ulong userId, CancellationToken cancellationToken = default);
}
