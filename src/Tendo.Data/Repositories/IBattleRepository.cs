using Tendo.Game.State;

namespace Tendo.Data.Repositories;

/// <summary>
/// 旧 <c>ctrl.read(channelId)</c> / <c>ctrl.write(channelId, r.ots(enemy))</c> の置き換え。
/// </summary>
public interface IBattleRepository
{
    Task<BattleState?> FindAsync(ulong channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 存在しなければ初期値で作る。旧 <c>fn()</c> の
    /// 「<c>!ids[0]</c> なら <c>ctrl.new</c> して <c>fn</c> をやり直す」に相当。
    /// </summary>
    /// <remarks>
    /// 敵の中身 (code / HP / 状態異常) は空のまま返る。旧実装も生成直後に
    /// 草原の敵を抽選して埋めていた。その抽選は Phase 4 の出現処理が行う。
    /// </remarks>
    Task<BattleState> GetOrCreateAsync(ulong channelId, CancellationToken cancellationToken = default);

    Task SaveAsync(BattleState battle, CancellationToken cancellationToken = default);

    /// <summary>
    /// 旧 <c>/ranking</c> と <c>/mod clist</c>。敵レベル降順。
    /// 旧実装は全戦場を読んで JS 側で並べ替えていたが、SQL で並べても結果は同じ。
    /// </summary>
    Task<IReadOnlyList<BattleState>> ListByLevelDescendingAsync(
        int limit,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>戦場の総数。<c>/mod clist</c> のページ表示 <c>[i / 総数]</c> に使う。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
