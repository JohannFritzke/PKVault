using Microsoft.Extensions.DependencyInjection;
using PKVault.Core;
using PKVault.Core.SaveDiscovery;
using PKVault.Core.savediscovery.routes;

public class SaveDiscoveryRegistrationTests
{
    public SaveDiscoveryRegistrationTests()
    {
        Program.Initialize();
    }

    [Fact]
    public void AddSaveDiscovery_ResolvesControllerAndAllDetectors()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSaveDiscovery();
        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<SaveDiscoveryController>());
        Assert.Equal(
            ["citra", "desmume", "melonds", "mgba", "retroarch"],
            sp.GetServices<IEmulatorDetector>().Select(d => d.Id).Order()
        );
    }

    [Fact]
    public void Controller_HasExpectedRoutes()
    {
        var type = typeof(SaveDiscoveryController);
        Assert.Equal("api/[controller]", ((RouteAttribute)Attribute.GetCustomAttribute(type, typeof(RouteAttribute))!).Template);
        Assert.Equal("save-discovery", SlugifyTransformer.TransformOutbound(type.Name.Replace("Controller", "")));

        (string Method, string Template)[] routes = [.. type.GetMethods()
            .Select(m => (HttpAttribute?)Attribute.GetCustomAttribute(m, typeof(HttpAttribute)))
            .OfType<HttpAttribute>()
            .Select(h => (h.Method, h.Template))
            .Order()];

        Assert.Equal([("GET", ""), ("POST", "add"), ("POST", "scan")], routes);
    }
}
