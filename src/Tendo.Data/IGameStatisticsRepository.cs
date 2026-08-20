namespace Tendo.Data;

/// <summary>
/// 旧 <c>ctrl.all("mmo_user")</c> / <c>ctrl.all("mmo_channel")</c> の件数取得。
/// <c>/info</c> が「参加しているユーザー」「戦場の数」を出すために使う。
/// </summary>
public interface IGameStatisticsRepository
{
    Task<GameStatistics> GetAsync(CancellationToken cancellationToken = default);
}

/// <param name="PlayerCount">旧 <c>mmo_user</c> の行数。</param>
/// <param name="BattleCount">旧 <c>mmo_channel</c> の行数。</param>
public readonly record struct GameStatistics(int PlayerCount, int BattleCount);

/// <summary>
/// Phase 1 用の暫定実装。実データを返す MySQL 実装は Phase 3 で入る。
/// </summary>
public sealed class UnavailableGameStatisticsRepository : IGameStatisticsRepository
{
    public Task<GameStatistics> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new GameStatistics(0, 0));
}
