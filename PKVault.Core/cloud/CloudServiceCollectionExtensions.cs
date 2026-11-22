using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.BackupProviders;
using PKVault.Core.cloud.routes;

namespace PKVault.Core.Cloud;

public static class CloudServiceCollectionExtensions
{
    /// <summary>
    /// Registers cloud account (PocketBase login), the PocketBase backup provider and the controller.
    /// Requires IFileSystem, TimeProvider, BackupService and AddBackupProviders().
    /// </summary>
    public static IServiceCollection AddCloud(this IServiceCollection services)
    {
        services.AddSingleton(sp => new CloudStore(sp.GetRequiredService<IFileSystem>()));
        // long-lived realtime stream: per-request timeouts are set inside the client
        services.AddSingleton(_ => new PocketBaseClient(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }));
        services.AddSingleton<CloudAuthService>();
        services.AddSingleton<CloudSyncService>();
        services.AddSingleton<IBackupProvider, PocketBaseBackupProvider>();
        services.AddScoped<CloudController>();

        return services;
    }
}
