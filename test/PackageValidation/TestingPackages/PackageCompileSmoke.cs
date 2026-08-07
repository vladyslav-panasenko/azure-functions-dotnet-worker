using System;
using Microsoft.Azure.Functions.Worker.Testing;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus.Testing;
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs.Testing;

namespace TestingPackages;

public sealed class PackageCompileSmoke
{
    public static FunctionsApplicationFactory<SmokeEntryPoint> CreateBuiltInFactory()
        => new FunctionsApplicationFactory<SmokeEntryPoint>()
            .WithSetting("Sample:Mode", "Package");

    public static FunctionsApplicationFactory<SmokeEntryPoint> CreateAspNetCoreFactory()
        => CreateBuiltInFactory().WithAspNetCore();

    public static Type[] TriggerCompanions { get; } =
    [
        typeof(ServiceBusTriggerTestData),
        typeof(BlobTriggerTestData)
    ];

    public sealed class SmokeEntryPoint;
}
