// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.Worker.Testing.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[JsonExporterAttribute.Full]
[MarkdownExporterAttribute.GitHub]
public class FactoryStartupBenchmarks
{
    [Benchmark]
    [InvocationCount(1)]
    public async Task StartAndDispose()
    {
        await using var factory = BenchmarkFactory.Create();
        _ = factory.Services;
    }
}

[MemoryDiagnoser]
[InProcess]
[JsonExporterAttribute.Full]
[MarkdownExporterAttribute.GitHub]
public class WarmInvocationBenchmarks
{
    private FunctionsApplicationFactory<ModernFunctionApp.Program> _factory = null!;
    private FunctionInvocationRequest _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        _factory = BenchmarkFactory.Create();
        _ = _factory.Services;
        _request = FunctionInvocationRequest.Create()
            .WithInput("request", FunctionTestValue.String("benchmark"))
            .WithInvocationId("benchmark-warm");
    }

    [GlobalCleanup]
    public void Cleanup()
        => _factory.Dispose();

    [Benchmark]
    public Task<FunctionInvocationResult> Invoke()
        => _factory.InvokeAsync("GenericEcho", _request);
}

internal static class BenchmarkFactory
{
    internal static FunctionsApplicationFactory<ModernFunctionApp.Program> Create()
        => new FunctionsApplicationFactory<ModernFunctionApp.Program>()
            .WithContentRoot(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Resources",
                "Testing",
                "ModernFunctionApp",
                "bin",
                "Release",
                "net8.0")));
}
