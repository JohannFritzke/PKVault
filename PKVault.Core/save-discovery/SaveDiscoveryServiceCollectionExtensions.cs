using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.savediscovery.routes;

namespace PKVault.Core.SaveDiscovery;

public static class SaveDiscoveryServiceCollectionExtensions
{
    /// <summary>
    /// Registers save discovery services, detectors and controller.
    /// Requires IFileSystem, IFileIOService, ISettingsService, ISavesLoadersService, ISessionService, DataService.
    /// </summary>
    public static IServiceCollection AddSaveDiscovery(this IServiceCollection services)
    {
        services.AddSingleton<IDiscoveryEnvironment, DiscoveryEnvironment>();
        services.AddSingleton<IMgbaLibraryReader, MgbaLibraryReader>();

        // add new emulators here
        services.AddSingleton<IEmulatorDetector, MgbaDetector>();
        services.AddSingleton<IEmulatorDetector, MelonDsDetector>();
        services.AddSingleton<IEmulatorDetector, DesmumeDetector>();
        services.AddSingleton<IEmulatorDetector, CitraDetector>();
        services.AddSingleton<IEmulatorDetector, RetroArchDetector>();

        services.AddSingleton<SaveDiscoveryService>();
        services.AddScoped<SaveDiscoveryController>();

        return services;
    }
}
