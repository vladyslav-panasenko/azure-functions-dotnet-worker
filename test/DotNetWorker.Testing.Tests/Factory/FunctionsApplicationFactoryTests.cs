// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Testing.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Microsoft.Azure.Functions.Worker.Testing.Tests.Factory;

public class FunctionsApplicationFactoryTests
{
    [Fact]
    public async Task Factory_ModernBuilderStartsRealEntryPointAndLoadsMetadata()
    {
        await using var factory = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"));

        InMemoryFunctionsHost protocol = factory.Services.GetRequiredService<InMemoryFunctionsHost>();

        Assert.Equal(InMemoryFunctionsHostState.Ready, protocol.State);
        Assert.Equal("ModernEcho", Assert.Single(protocol.FunctionMetadata).Name);
    }

    [Fact]
    public async Task Factory_ClassicHostBuilderStartsRealEntryPointAndLoadsMetadata()
    {
        await using var factory = new FunctionsApplicationFactory<ClassicFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ClassicFunctionApp"));

        InMemoryFunctionsHost protocol = factory.Services.GetRequiredService<InMemoryFunctionsHost>();

        Assert.Equal(InMemoryFunctionsHostState.Ready, protocol.State);
        Assert.Equal("ClassicEcho", Assert.Single(protocol.FunctionMetadata).Name);
    }

    [Fact]
    public async Task Factory_SettingsApplyAtHostBuildButNotToPriorImperativeReads()
    {
        await using var factory = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"))
            .WithSetting("FactorySetting", "configured");

        Assert.Null(factory.Services.GetRequiredService<ModernFunctionApp.ImperativeSettingSnapshot>().Value);
        Assert.Equal("configured", factory.Services.GetRequiredService<ModernFunctionApp.ResolvedFactorySetting>().Value);
    }

    [Fact]
    public async Task Factory_WithServicesIsLastWins()
    {
        var replacement = new ModernFunctionApp.ResolvedFactorySetting("replacement");
        await using var factory = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"))
            .WithServices(services => services.Replace(ServiceDescriptor.Singleton(replacement)));

        Assert.Same(replacement, factory.Services.GetRequiredService<ModernFunctionApp.ResolvedFactorySetting>());
    }

    [Fact]
    public async Task Factory_ClonesAreIndependentAndCanStartInParallel()
    {
        var original = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"));
        FunctionsApplicationFactory<ModernFunctionApp.Program> first = original.WithSetting("FactorySetting", "first");
        FunctionsApplicationFactory<ModernFunctionApp.Program> second = original.WithSetting("FactorySetting", "second");

        Task<IServiceProvider> firstStart = Task.Run(() => first.Services);
        Task<IServiceProvider> secondStart = Task.Run(() => second.Services);
        await Task.WhenAll(firstStart, secondStart);

        Assert.Equal("first", firstStart.Result.GetRequiredService<ModernFunctionApp.ResolvedFactorySetting>().Value);
        Assert.Equal("second", secondStart.Result.GetRequiredService<ModernFunctionApp.ResolvedFactorySetting>().Value);

        await first.DisposeAsync();
        await second.DisposeAsync();
        await original.DisposeAsync();
    }

    [Fact]
    public async Task Factory_DifferentApplicationsCanStartInParallel()
    {
        await using var modern = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"));
        await using var classic = new FunctionsApplicationFactory<ClassicFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ClassicFunctionApp"));

        Task<IServiceProvider> modernStart = Task.Run(() => modern.Services);
        Task<IServiceProvider> classicStart = Task.Run(() => classic.Services);
        await Task.WhenAll(modernStart, classicStart);

        Assert.Equal("ModernEcho", modernStart.Result.GetRequiredService<InMemoryFunctionsHost>().FunctionMetadata.Single().Name);
        Assert.Equal("ClassicEcho", classicStart.Result.GetRequiredService<InMemoryFunctionsHost>().FunctionMetadata.Single().Name);
    }

    [Fact]
    public async Task Factory_ConfigurationAfterStartupThrows()
    {
        await using var factory = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"));
        _ = factory.Services;

        Assert.Throws<InvalidOperationException>(() => factory.WithSetting("late", "value"));
        Assert.Throws<InvalidOperationException>(() => factory.WithServices(_ => { }));
        Assert.Throws<InvalidOperationException>(() => factory.WithHostBuilder(_ => { }));
    }

    [Fact]
    public void Factory_MissingFunctionOutputFailsBeforeEntryPointExecutionAndCachesFailure()
    {
        string emptyRoot = Directory.CreateTempSubdirectory("functions-factory-").FullName;
        try
        {
            using var factory = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
                .WithContentRoot(emptyRoot);

            FileNotFoundException first = Assert.Throws<FileNotFoundException>(() => _ = factory.Services);
            FileNotFoundException second = Assert.Throws<FileNotFoundException>(() => _ = factory.Services);

            Assert.Same(first, second);
            Assert.Contains("host.json", first.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyRoot);
        }
    }

    [Fact]
    public async Task Factory_DisposalIsIdempotentAndRejectsFurtherAccess()
    {
        var factory = new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(GetFunctionOutput("ModernFunctionApp"));
        _ = factory.Services;

        await factory.DisposeAsync();
        await factory.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = factory.Services);
    }

    private static string GetFunctionOutput(string projectName)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Resources",
            "Testing",
            projectName,
            "bin",
            "Release",
            "net8.0"));
}
