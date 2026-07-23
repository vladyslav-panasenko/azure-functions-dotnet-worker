// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing;

internal sealed class FunctionsTestingStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => application =>
        {
            application.UseMiddleware<FunctionsTestingDispatchMiddleware>();
            next(application);
        };
}
