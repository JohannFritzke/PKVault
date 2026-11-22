using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.SingleLocationRoutes;

namespace PKVault.Core;

public static class SingleLocationServiceCollectionExtensions
{
    /// <summary>Fork rule "a pokémon is in a save or in the storage". Requires AddCloud().</summary>
    public static IServiceCollection AddSingleLocation(this IServiceCollection services)
    {
        services.AddSingleton<SingleLocationService>();
        services.AddScoped<ResolveSingleLocationAction>();
        services.AddScoped<SingleLocationController>();

        return services;
    }
}
