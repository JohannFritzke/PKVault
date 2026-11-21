using Microsoft.Extensions.DependencyInjection;

namespace PKVault.Core.BackupProviders;

public static class BackupProvidersServiceCollectionExtensions
{
    /// <summary>
    /// Registers the backup providers dispatcher.
    /// Providers themselves are registered as <c>services.AddSingleton&lt;IBackupProvider, MyProvider&gt;()</c>.
    /// Requires TimeProvider.
    /// </summary>
    public static IServiceCollection AddBackupProviders(this IServiceCollection services)
    {
        services.AddSingleton<BackupProviderDispatcher>();

        // add new providers here

        return services;
    }
}
