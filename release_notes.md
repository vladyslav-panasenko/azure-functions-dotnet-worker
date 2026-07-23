## What's Changed

<!-- Please add your release notes in the following format:
- My change description (#PR/#issue)
-->

### Microsoft.Azure.Functions.Worker (metapackage) <version>

- <entry>

### Microsoft.Azure.Functions.Worker.Core <version>

- <entry>

### Microsoft.Azure.Functions.Worker.Grpc <version>

- <entry>
## Draft: isolated worker integration testing preview

- Adds the local/CI-only `Microsoft.Azure.Functions.Worker.Testing` preview package for real in-process worker startup, synthetic invocation, and function-targeted built-in HTTP.
- Adds the local/CI-only `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing` preview companion for worker-owned ASP.NET Core routes through `TestServer`.
- Records exact closed dependency ranges, per-target TestHost versions, Source Link, package validation, capability boundaries, conformance reports, benchmarks, runnable samples, and full-host escalation guidance.
- No package is published and no external issue is changed by this draft. Publication, package ownership, issue updates, and support commitments require a separate owner-authorized release.
