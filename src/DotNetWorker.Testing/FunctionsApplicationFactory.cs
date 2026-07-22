// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Grpc;
using Microsoft.Azure.Functions.Worker.Testing.Hosting;
using Microsoft.Azure.Functions.Worker.Testing.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.Functions.Worker.Testing;

/// <summary>
/// Boots an Azure Functions .NET isolated worker application through its real entry point for integration testing.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the executable function application assembly.</typeparam>
public class FunctionsApplicationFactory<TEntryPoint> : IDisposable, IAsyncDisposable
    where TEntryPoint : class
{
    private readonly IReadOnlyList<Action<IHostBuilder>> _hostConfigurations;
    private readonly IReadOnlyList<Action<IServiceCollection>> _serviceConfigurations;
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly FunctionsApplicationFactoryOptions _options;
    private readonly string? _contentRoot;
    private readonly Lazy<Task<FactoryState>> _startup;
    private int _disposed;

    /// <summary>Initializes an unstarted factory using default options.</summary>
    public FunctionsApplicationFactory()
        : this(
            Array.Empty<Action<IHostBuilder>>(),
            Array.Empty<Action<IServiceCollection>>(),
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new FunctionsApplicationFactoryOptions(),
            contentRoot: null)
    {
    }

    private FunctionsApplicationFactory(
        IReadOnlyList<Action<IHostBuilder>> hostConfigurations,
        IReadOnlyList<Action<IServiceCollection>> serviceConfigurations,
        IReadOnlyDictionary<string, string?> settings,
        FunctionsApplicationFactoryOptions options,
        string? contentRoot)
    {
        _hostConfigurations = hostConfigurations;
        _serviceConfigurations = serviceConfigurations;
        _settings = settings;
        _options = options;
        _contentRoot = contentRoot;
        _startup = new Lazy<Task<FactoryState>>(StartAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Gets the started application's service provider.</summary>
    public IServiceProvider Services => GetState().Services;

    /// <summary>Returns an independent unstarted factory with an additional host-builder callback.</summary>
    public FunctionsApplicationFactory<TEntryPoint> WithHostBuilder(Action<IHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureCanConfigure();
        return Clone(hostConfigurations: Append(_hostConfigurations, configure));
    }

    /// <summary>Returns an independent unstarted factory with a last-wins service callback.</summary>
    public FunctionsApplicationFactory<TEntryPoint> WithServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureCanConfigure();
        return Clone(serviceConfigurations: Append(_serviceConfigurations, configure));
    }

    /// <summary>Returns an independent unstarted factory with an in-memory configuration setting.</summary>
    public FunctionsApplicationFactory<TEntryPoint> WithSetting(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A non-empty configuration key is required.", nameof(key));
        }

        EnsureCanConfigure();
        var settings = new Dictionary<string, string?>(_settings, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        };
        return Clone(settings: settings);
    }

    /// <summary>Returns an independent unstarted factory using an explicit function output directory.</summary>
    public FunctionsApplicationFactory<TEntryPoint> WithContentRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A content-root path is required.", nameof(path));
        }

        EnsureCanConfigure();
        string fullPath = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(fullPath) || !Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The function content root '{fullPath}' does not exist.");
        }

        return Clone(contentRoot: fullPath, replaceContentRoot: true);
    }

    /// <summary>Returns an independent unstarted factory with modified validated options.</summary>
    public FunctionsApplicationFactory<TEntryPoint> WithOptions(Action<FunctionsApplicationFactoryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureCanConfigure();
        FunctionsApplicationFactoryOptions options = _options.Clone();
        configure(options);
        options.Validate();
        return Clone(options: options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!_startup.IsValueCreated)
        {
            return;
        }

        FactoryState state;
        try
        {
            state = await _startup.Value;
        }
        catch
        {
            return;
        }

        await state.Protocol.DisposeAsync();

        using var shutdown = new CancellationTokenSource(_options.ShutdownTimeout);
        try
        {
            await state.Host.StopAsync(shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }

        if (state.Host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            state.Host.Dispose();
        }
    }

    private FactoryState GetState()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _startup.Value.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task<FactoryState> StartAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _options.Validate();

        Assembly applicationAssembly = typeof(TEntryPoint).Assembly;
        if (applicationAssembly.EntryPoint is null)
        {
            throw new InvalidOperationException(
                $"Assembly '{applicationAssembly.FullName}' has no executable entry point. TEntryPoint must come from the function application executable.");
        }

        string contentRoot = ResolveContentRoot(applicationAssembly);
        ValidateFunctionOutput(contentRoot);

        var protocol = new InMemoryFunctionsHost(_options.ShutdownTimeout, _options.MaxMessageLength);
        var builder = new DeferredFunctionsHostBuilder();
        builder.UseEnvironment(_options.EnvironmentName);
        builder.UseContentRoot(contentRoot);
        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostDefaults.ApplicationKey] = applicationAssembly.GetName().Name ?? string.Empty
            }));

        if (_settings.Count > 0)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));
        }

        foreach (Action<IHostBuilder> configure in _hostConfigurations)
        {
            configure(builder);
        }

        foreach (Action<IServiceCollection> configure in _serviceConfigurations)
        {
            builder.ConfigureServices((_, services) => configure(services));
        }

        builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(protocol);
            services.AddSingleton<InMemoryWorkerClientFactory>();
            services.Replace(ServiceDescriptor.Singleton<IWorkerClientFactory>(provider =>
                provider.GetRequiredService<InMemoryWorkerClientFactory>()));
        });

        Func<string[], object>? hostFactory = HostFactoryResolver.ResolveHostFactory(
            applicationAssembly,
            _options.StartupTimeout,
            builder.ConfigureHostBuilder,
            builder.EntryPointCompleted);
        if (hostFactory is null)
        {
            throw new InvalidOperationException(
                $"Assembly '{applicationAssembly.FullName}' does not expose an executable entry point that builds an IHost.");
        }

        builder.SetHostFactory(hostFactory);
        IHost host = builder.Build();

        using var startup = new CancellationTokenSource(_options.StartupTimeout);
        try
        {
            await host.StartAsync(startup.Token);
            await protocol.InitializeAsync(contentRoot, _options.StartupTimeout, startup.Token);
            return new FactoryState(host, protocol);
        }
        catch
        {
            await protocol.DisposeAsync();
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                host.Dispose();
            }

            throw;
        }
    }

    private string ResolveContentRoot(Assembly applicationAssembly)
    {
        if (_contentRoot is not null)
        {
            return _contentRoot;
        }

        string? location = Path.GetDirectoryName(applicationAssembly.Location);
        if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
        {
            throw new DirectoryNotFoundException(
                $"Could not resolve a content root beside '{applicationAssembly.Location}'. Use WithContentRoot with the function build output directory.");
        }

        return Path.GetFullPath(location);
    }

    private static void ValidateFunctionOutput(string contentRoot)
    {
        foreach (string requiredFile in new[] { "host.json", "functions.metadata" })
        {
            string path = Path.Combine(contentRoot, requiredFile);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"The function build output '{contentRoot}' does not contain required file '{requiredFile}'. Use WithContentRoot for shadow-copy test layouts.",
                    path);
            }
        }
    }

    private void EnsureCanConfigure()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_startup.IsValueCreated)
        {
            throw new InvalidOperationException("Factory configuration cannot be changed after startup has begun.");
        }
    }

    private FunctionsApplicationFactory<TEntryPoint> Clone(
        IReadOnlyList<Action<IHostBuilder>>? hostConfigurations = null,
        IReadOnlyList<Action<IServiceCollection>>? serviceConfigurations = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        FunctionsApplicationFactoryOptions? options = null,
        string? contentRoot = null,
        bool replaceContentRoot = false)
        => new(
            hostConfigurations ?? _hostConfigurations,
            serviceConfigurations ?? _serviceConfigurations,
            settings ?? _settings,
            options ?? _options.Clone(),
            replaceContentRoot ? contentRoot : _contentRoot);

    private static IReadOnlyList<T> Append<T>(IReadOnlyList<T> source, T item)
    {
        var result = new T[source.Count + 1];
        for (int index = 0; index < source.Count; index++)
        {
            result[index] = source[index];
        }

        result[^1] = item;
        return result;
    }

    private sealed record FactoryState(IHost Host, InMemoryFunctionsHost Protocol)
    {
        internal IServiceProvider Services => Host.Services;
    }
}
