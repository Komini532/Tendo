using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tendo.Data.Repositories;

namespace Tendo.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>MySQL 永続化層を登録する。</summary>
    public static IServiceCollection AddTendoData(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new MySqlConnectionFactory(options.ConnectionString);
        });

        services.AddSingleton<MigrationRunner>();

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IBattleRepository, BattleRepository>();
        services.AddScoped<IBanRepository, BanRepository>();
        services.AddScoped<IGameStatisticsRepository, MySqlGameStatisticsRepository>();

        return services;
    }
}
