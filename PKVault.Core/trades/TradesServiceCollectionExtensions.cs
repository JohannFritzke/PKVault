using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.trades.routes;

namespace PKVault.Core.Trades;

public static class TradesServiceCollectionExtensions
{
    /// <summary>Registers trades with friends (PocketBase). Requires AddCloud().</summary>
    public static IServiceCollection AddTrades(this IServiceCollection services)
    {
        services.AddSingleton<TradeService>();
        services.AddScoped<TradeReceivePkmAction>();
        services.AddScoped<TradeController>();

        return services;
    }
}
