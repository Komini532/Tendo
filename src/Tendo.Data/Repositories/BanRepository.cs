using Dapper;

namespace Tendo.Data.Repositories;

/// <summary>
/// 旧 <c>mmo_others</c> の id=0 に入っていた <c>{ list: [...] }</c> の置き換え。
///
/// 旧実装は起動時に全件をメモリ配列 <c>BANLIST</c> へ読み込み、以降そこだけを見ていた。
/// そのため <c>/mod ban</c> 直後は反映されるが、Bot を複数プロセスで動かすと
/// ずれるし、<c>/mod banlist</c> は再起動するまで DB と食い違った。
/// ここでは毎回 DB を引く (BAN は 1 コマンドにつき 1 回の軽い問い合わせ)。
/// </summary>
public interface IBanRepository
{
    Task<bool> IsBannedAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>旧 <c>/mod banlist</c>。BAN された順。</summary>
    Task<IReadOnlyList<ulong>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ulong userId, CancellationToken cancellationToken = default);

    Task RemoveAsync(ulong userId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class BanRepository : IBanRepository
{
    private readonly IDbConnectionFactory _connections;

    public BanRepository(IDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<bool> IsBannedAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bans WHERE user_id = @userId",
            new { userId }) > 0;
    }

    public async Task<IReadOnlyList<ulong>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<ulong>(
            "SELECT user_id FROM bans ORDER BY created_at ASC, user_id ASC")).ToList();
    }

    public async Task AddAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            "INSERT IGNORE INTO bans (user_id) VALUES (@userId)",
            new { userId });
    }

    public async Task RemoveAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM bans WHERE user_id = @userId", new { userId });
    }
}
