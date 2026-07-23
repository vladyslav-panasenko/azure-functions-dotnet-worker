// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore;
using Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.AspNetMiddleware;
using Microsoft.Azure.Functions.Worker.Testing;

namespace Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing;

internal sealed class FunctionsTestingDispatchMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FunctionsEndpointDataSource _dataSource;
    private readonly IFunctionsTestInvocationDispatcher _dispatcher;

    public FunctionsTestingDispatchMiddleware(
        RequestDelegate next,
        FunctionsEndpointDataSource dataSource,
        IFunctionsTestInvocationDispatcher dispatcher,
        IHttpCoordinator coordinator)
    {
        _next = next;
        _dataSource = dataSource;
        _dispatcher = dispatcher;
        _ = coordinator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        (RouteEndpoint? endpoint, RouteValueDictionary? routeValues, bool pathMatched) = MatchEndpoint(context);
        if (endpoint is null)
        {
            context.Response.StatusCode = pathMatched
                ? StatusCodes.Status405MethodNotAllowed
                : StatusCodes.Status404NotFound;
            return;
        }

        context.SetEndpoint(endpoint);
        context.Request.RouteValues = routeValues!;

        string functionName = endpoint.DisplayName
            ?? throw new InvalidOperationException("The matched function endpoint has no function name.");
        string invocationId = Guid.NewGuid().ToString("N");
        context.Request.Headers[Constants.CorrelationHeader] = invocationId;
        FunctionsTestHttpRequest request = await CreateRequestAsync(context.Request, context.RequestAborted);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task<FunctionInvocationResult> invocation = _dispatcher.InvokeHttpAsync(
            functionName,
            request,
            invocationId,
            cancellation.Token);

        try
        {
            await _next(context);
            FunctionInvocationResult result = await invocation;
            if (result.Status == FunctionInvocationStatus.Failed)
            {
                throw new InvalidOperationException(
                    $"ASP.NET Core function '{functionName}' failed: "
                    + (result.Exception is null
                        ? "The worker returned no exception details."
                        : $"{result.Exception.Message}{Environment.NewLine}{result.Exception.StackTrace}"));
            }
        }
        catch
        {
            cancellation.Cancel();
            try
            {
                await invocation;
            }
            catch
            {
            }

            throw;
        }
    }

    private (RouteEndpoint? Endpoint, RouteValueDictionary? Values, bool PathMatched) MatchEndpoint(
        HttpContext context)
    {
        bool pathMatched = false;
        foreach (RouteEndpoint endpoint in _dataSource.Endpoints
                     .OfType<RouteEndpoint>()
                     .OrderBy(item => item.Order))
        {
            string rawPattern = endpoint.RoutePattern.RawText ?? string.Empty;
            var matcher = new TemplateMatcher(
                TemplateParser.Parse(rawPattern),
                new RouteValueDictionary(endpoint.RoutePattern.Defaults));
            var values = new RouteValueDictionary();
            if (!matcher.TryMatch(context.Request.Path, values))
            {
                continue;
            }

            pathMatched = true;
            HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
            if (methods is not null
                && methods.HttpMethods.Count > 0
                && !methods.HttpMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            return (endpoint, values, true);
        }

        return (null, null, pathMatched);
    }

    private static async Task<FunctionsTestHttpRequest> CreateRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, cancellationToken);
        request.Body.Position = 0;
        if (request.ContentLength is null && body.Length > 0)
        {
            request.ContentLength = body.Length;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, Microsoft.Extensions.Primitives.StringValues values) in request.Headers)
        {
            headers[name] = values.ToString();
        }

        var url = new Uri(
            $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}");
        return new FunctionsTestHttpRequest(
            request.Method,
            url,
            headers,
            body.ToArray());
    }
}
