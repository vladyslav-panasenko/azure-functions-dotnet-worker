# Isolated worker test factory code review

Reviewed: 2026-07-25

Diff: `34ce8e6f524a9f9a6a162ac91e2abc37987a7087..5613ef9ecfe191145a5b22d6ed64de2e9b57b435`

## Finding

### [P1] Compare runtime outputs before reporting zero differential

Location: `test/Testing.Conformance/run.ps1:97`

The runtime lane runs several independent test projects and then unconditionally writes:

```text
status: passed
unexplainedDifferences: 0
```

No result from an in-memory invocation is exchanged with a result from Core Tools, and no runtime
normalization or comparison function is called before the report is generated.

The selected tests do not even invoke equivalent functions:

- The in-memory lane runs `BuiltInHttpTests` and
  `Invocation_ExecutesRealPipelineAndReturnsIdValueAndLogs` against `ModernFunctionApp`.
- The real built-in HTTP lane invokes `HelloFromQuery` in `E2EApp`.
- The in-memory ASP.NET Core lane exercises routes in `AspNetCoreFunctionApp`.
- The real ASP.NET Core lane invokes `HttpWithCancellationTokenNotUsed` in `E2EAspNetCoreApp`.
- The queue E2E test has no in-memory counterpart because trigger listening and settlement are explicitly
  host-owned capabilities.

Each test can pass its own hard-coded expectations while the in-memory and real-host behaviors differ.
For example, a header, middleware marker, log category, or body-normalization divergence would not fail
the lane unless one independent test happened to assert that exact behavior. The script would still emit
`unexplainedDifferences: 0`.

This makes `runtime-differential-report.json` a smoke-suite status file presented as a differential
comparison. Because the report is used as compatibility evidence for a new testing package, a semantic
regression can pass the intended release gate and be reported as proven parity.

#### Evidence

- `test/Testing.Conformance/run.ps1:97-109` only invokes separate test projects.
- `test/Testing.Conformance/run.ps1:111-130` constructs the passing report from constants.
- `test/E2ETests/E2ETests/HttpEndToEndTests.cs:51` independently asserts the real
  `HelloFromQuery` result.
- `test/E2ETests/E2ETests/AspNetCore/CancellationEndToEndTests.cs:64` independently asserts a
  different ASP.NET Core function.
- The actual serialized transport lane does have a comparator in
  `test/Testing.Conformance/Transport/TransportDifferentialRunner.cs`; the runtime lane has no equivalent.

#### False-positive checks

- This is not the documented exclusion of host-owned routing, authorization, listeners, settlement,
  retries, or scale. The report explicitly claims comparison of HTTP output, logs, and middleware
  markers, which are within its stated surface.
- Test-process exit codes are checked, but successful independent assertions do not establish equality
  between two implementations.
- The runtime tests are useful smoke/E2E coverage. The defect is the missing comparison and the
  unconditional parity claim, not the existence of those tests.
- The serialized loopback transport comparison cannot cover this gap because both sides run the same
  worker pipeline and it does not execute the real Functions host.

#### Recommendation

Choose one of these honest contracts:

1. Build shared differential fixtures that execute equivalent scenarios in both lanes, serialize a
   fixed normalized result shape, compare the two result sets, and write `passed` with zero differences
   only after that comparator succeeds.
2. Rename the lane and report to runtime smoke/E2E coverage, remove `unexplainedDifferences` and
   `comparedFields`, and stop citing it as differential parity evidence.

The first option matches the approved conformance goal. The second is a safe downgrade if maintaining
shared cross-process fixtures is currently too expensive.

## Review coverage

The review inspected:

- factory startup, cloning, shutdown, host capture, execution-context scoping, and failure caching;
- in-memory protocol state, request correlation, cancellation, message limits, logs, and disposal;
- invocation models, value conversion, retry/trace projection, built-in HTTP, and capability negatives;
- ASP.NET Core TestServer activation, dispatch ordering, routing, request bodies, cancellation,
  redirects, cookies, and client lifetime;
- exact dependency ranges, assembly compatibility checks, package manifests, official build inventory,
  release exclusions, and handoff gates;
- transport conformance, runtime lane orchestration, stress tests, benchmarks, and CI wiring.

No source fix was made as part of this review.

## Verification notes

- `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing.Tests` passed 12 of 12 on net8.0.
- `Microsoft.Azure.Functions.Worker.Testing.Tests` passed 47 of 47 on a repeat no-build run.
- The first core-suite run observed one timing-sensitive failure in
  `Factory_UnavailableSerializedTransportHonorsStartupTimeout`; the isolated rerun and the complete
  repeat run passed. It was not selected over the deterministic runtime-report finding.
- `git diff --check` is required before handoff.
