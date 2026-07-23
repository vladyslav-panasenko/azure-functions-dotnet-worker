using Microsoft.Azure.Functions.Worker.Testing;

namespace TestingPackages;

public sealed class PackageCompileSmoke
{
    public static FunctionsApplicationFactory<SmokeEntryPoint> CreateBuiltInFactory()
        => new FunctionsApplicationFactory<SmokeEntryPoint>()
            .WithSetting("Sample:Mode", "Package");

    public static FunctionsApplicationFactory<SmokeEntryPoint> CreateAspNetCoreFactory()
        => CreateBuiltInFactory().WithAspNetCore();

    public sealed class SmokeEntryPoint;
}
