# Isolated worker test factory implementation status

Open question:
- could i use it without commits in base project? 

Last reconciled: 2026-07-23T16:35:30Z

## Status summary

The approved implementation is complete. Tasks T01 through T07 are `Done`, and the execution state in
`docs/specs/2026-07-23-isolated-worker-test-factory-v3.html` is `Done`.

- Branch: `feature/isolated-worker-test-factory`
- Current commit: `8199157a1` (`Complete isolated worker testing infrastructure`)
- Upstream branch commit at reconciliation: `8199157a1`
- Approved specification: `docs/specs/2026-07-23-isolated-worker-test-factory-v3.html`
- Approved immutable-body SHA-256: `4e18c1a9bbeaf7f6ea3de4044a1150342ad0d8ddc24713b6115d93f0b67168b6`
- Repository state before this report was added: clean
- Current documentation changes: this new report and the spec's append-only T07 audit note are uncommitted
- Delivery posture: source, tests, documentation, CI configuration, and local preview packages are complete; no package publication or external issue mutation was performed

The earlier `docs/specs/2026-07-22-isolated-worker-test-factory-checkpoint.md` is a historical checkpoint. It
stops during T06 and must not be used as the current status.

## What was implemented

### T01 — Application identity

- Added execution-context-local application assembly identity with nesting, restoration, and parallel isolation.
- Added the host-builder assembly overload and kept the service-collection assembly overload internal to preserve
  source compatibility for existing `AddFunctionsWorkerCore(null)` calls.
- Added eager-builder, extension-startup, idempotency, failure-cache, and compatibility coverage.

### T02 — In-memory worker protocol

- Added the multi-target `Microsoft.Azure.Functions.Worker.Testing` package.
- Implemented a channel-backed in-memory worker client over production worker seams.
- Covered initialization, metadata/load, invocation, cancellation, termination, timeouts, size limits, duplicate
  identifiers, disposal, and worker-log capture.
- Replaced process-global application-directory mutation with execution-context-local state.

### T03 — Factory lifecycle

- Added `FunctionsApplicationFactory<TEntryPoint>` with lazy cached startup, immutable customization clones, real
  entry-point execution, in-memory transport replacement, and owned shutdown.
- Adapted the generic-host capture helpers with repository-owned provenance.
- Fixed concurrent host-capture ownership, cached startup failure recovery, and shadow-copy content-root handling.

### T04 — Invocation and built-in HTTP

- Added protocol-neutral invocation inputs, trigger metadata, trace/retry data, immutable results, diagnostics,
  logs, and structured exceptions.
- Added real worker-pipeline invocation, binding conversion, cancellation, concurrent invocation, output handling,
  validation, and function-targeted synthetic built-in HTTP.
- Added `RetryContext.PreviousException` and immutable `FunctionRetryException` projection without exposing protobuf
  types or breaking custom retry contexts.

### T05 — ASP.NET Core TestServer companion

- Added the signed `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing` companion package.
- Added explicit `WithAspNetCore()` activation and an in-process `TestServer` path with no Kestrel fallback.
- Preserved framework endpoint routing, route constraints, 404/405 behavior, middleware, body binding,
  `IActionResult`/`IResult`, cancellation, redirects, cookies, client options, and provider ownership.

### T06 — Conformance and performance evidence

- Added 1,000-invocation correlation stress and repeated factory lifecycle coverage.
- Added a production loopback-gRPC transport comparison with strict normalization.
- Added explicit negative capabilities for host-owned routing, authorization, listeners, settlement, and retry
  scheduling.
- Added live Core Tools/Azurite differential coverage for built-in HTTP, queue, and ASP.NET Core HTTP.
- Added timeout, reclamation, cross-target CI, and BenchmarkDotNet smoke artifacts.

### T07 — Packaging, samples, documentation, and handoff

- Added both testing projects and validation suites to the repository solutions and official artifact inventory.
- Added strong-named preview packages with net8.0, net9.0, and net10.0 assets where supported.
- Added exact closed dependency ranges, generated compatibility manifests, runtime assembly-version diagnostics,
  package validation, Source Link metadata, and package readmes.
- Added built-in HTTP and ASP.NET Core sample test projects.
- Added `docs/testing.md`, `docs/testing-upstream-handoff.md`, release notes, and automated fresh-install,
  version-skew, and no-publication gates.
- Restored setup-only sample and E2E dependency edits after live verification; they are not part of the delivered
  product change.

## Verification evidence

| Verification area | Result |
| --- | --- |
| Core testing project | 47 passed, 0 failed |
| ASP.NET Core companion | 12 passed on net8.0 and 12 passed on net10.0 |
| Serialized transport conformance | 19 of 19 matched on net8.0 and net10.0; 0 unexplained differences |
| In-memory runtime factory comparison | 7 of 7 passed |
| Companion runtime comparison | 12 of 12 passed on net8.0 and net10.0 |
| Real built-in HTTP plus queue | 2 of 2 passed |
| Real ASP.NET Core HTTP | 1 of 1 passed |
| Runtime differential report | Passed; 0 unexplained differences |
| Source package samples | 1 of 1 passed for each sample |
| Fresh local-feed install | Passed |
| Intentional Worker-version skew | Rejected as expected |
| No-publication gate | Passed |
| Official targeted artifact set and package gate | Passed |

Machine-readable conformance evidence is retained in:

- `test/Testing.Conformance/TestResults/transport-report.json`
- `test/Testing.Conformance/TestResults/runtime-differential-report.json`

The BenchmarkDotNet artifacts are smoke measurements, not release thresholds. The recorded one-iteration run measured
approximately 4.114 ms and 690.69 KB allocated for factory start/dispose, and 395.9 microseconds and 863.56 KB allocated
for warm invocation. Use repeated, environment-controlled runs before making regression or capacity claims.

## Current repository and artifact state

The implementation commits after the original base are:

```text
8199157a1 Complete isolated worker testing infrastructure
1f498a05b Add capability boundaries and startup timeout normalization
5ff8aab5c Add transport conformance coverage and scope function app directory
2c72dcb4a Add serialized gRPC transport support for conformance tests
c3f356fa0 Record T06 durability gate progress
550a0feb8 Complete ASP.NET Core TestServer companion
fae676990 Complete T03 lifecycle and update testing checkpoint
a53726398 WIP
```

Local preview packages were generated under `artifacts/testing-preview`. They include the aligned Worker, Core, Grpc,
HTTP, ASP.NET Core integration, core testing, and ASP.NET Core testing packages. These artifacts are local verification
outputs and are not a publication record.

At the end of live verification, Azurite, Functions hosts, and build servers were stopped, and ports 7071 and
10000–10002 were clear.

## Known boundaries and exceptions

- The in-process factory intentionally does not reproduce host-owned routing, authorization, trigger listeners,
  settlement, retry scheduling, scale, or full binding-extension behavior. Use Core Tools or an equivalent full-host
  lane for those assertions.
- Built-in HTTP is function-targeted synthetic invocation. Only the ASP.NET Core companion exercises worker-owned
  endpoint routing through `TestServer`.
- Exact dependency alignment is deliberate because the testing packages use signed friend access to worker internals.
  Version mismatch fails early with actionable diagnostics.
- The complete existing ASP.NET Core extension suite still has three unrelated Windows line-ending-sensitive code-fix
  failures; the non-analyzer regression set passed 27 of 27.
- A broad `dotnet pack DotNetWorker.sln` creates the new testing package and then fails on the unchanged
  `Azure.Functions.Sdk.Resolver` missing-output behavior. The official targeted artifact set and the package gate pass.
  This is a repository baseline exception, not an incomplete testing-package task.

## Remaining work

There is no remaining implementation task in the approved T01–T07 scope.

`Q01` remains intentionally non-blocking: Azure Functions product and release owners must separately authorize the
Microsoft package identifiers, signing/release posture, publication, any external issue update, and any support
commitment. Until that happens, keep the packages local or CI-only and do not claim Microsoft support.

Normal upstream review may request changes. Any request that changes the approved contracts, scope, acceptance
criteria, or compatibility posture requires a new specification version; ordinary implementation corrections can be
verified against the existing baseline.

## Resume and audit commands

```powershell
git status --short
git branch --show-current
git diff --check

dotnet test test\DotNetWorker.Testing.Tests\DotNetWorker.Testing.Tests.csproj -c Release
dotnet test test\extensions\Worker.Extensions.Http.AspNetCore.Testing.Tests\Worker.Extensions.Http.AspNetCore.Testing.Tests.csproj -c Release -f net8.0
dotnet test test\extensions\Worker.Extensions.Http.AspNetCore.Testing.Tests\Worker.Extensions.Http.AspNetCore.Testing.Tests.csproj -c Release -f net10.0

pwsh eng\verify-testing-packages.ps1
pwsh eng\verify-testing-handoff.ps1
```

The live Core Tools/Azurite lane requires its documented external tooling and port ownership; consult
`docs/testing-upstream-handoff.md` before rerunning it.
