using Dapper;

namespace Tendo.Data.Repositories;

/// <summary>
/// <c>/info</c> 用の件数。旧 <c>ctrl.all("mmo_user").length</c> /
/// <c>ctrl.all("mmo_channel").length</c> は全行を読んでから length を取っていたが、
/// COUNT(*) でも同じ数になる。
/// </summary>
public sealed class MySqlGameStatisticsRepository : IGameStatisticsRepository
{
    private readonly IDbConnectionFactory _connections;

    public MySqlGameStatisticsRepository(IDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<GameStatistics> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);

        using var grid = await connection.QueryMultipleAsync(
            """
            SELECT COUNT(*) FROM players;
            SELECT COUNT(*) FROM battles;
            """);

        var players = await grid.ReadSingleAsync<int>();
        var battles = await grid.ReadSingleAsync<int>();

        return new GameStatistics(players, battles);
    }
}
