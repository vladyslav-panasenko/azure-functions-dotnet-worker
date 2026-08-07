# Pull request title

Add integration testing support for .NET isolated worker

# Pull request description

Resolves #281.

## Why

.NET isolated Functions apps do not have a simple first-party way to run the real worker in-process in tests.
Today, teams usually choose between mocks and a full Functions host. There is a useful middle layer missing.

This gap matters even more with AI-driven development. AI can generate function code quickly, but startup,
middleware, binding conversion, cancellation, and HTTP behavior are too important to trust without tests that
exercise the real worker pipeline.

## What this adds

- `Microsoft.Azure.Functions.Worker.Testing` starts the application entry point in-process and invokes functions
  through the real worker pipeline with an in-memory transport.
- `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing` runs ASP.NET Core Functions endpoints with
  `TestServer`.
- Small Service Bus and Blob helpers convert SDK messages and blobs into synthetic trigger inputs.
- Tests, samples, documentation, package checks, CI coverage, and transport conformance checks.

## What this does not replace

This is worker integration testing. It is not a replacement for the Azure Functions host.

Host-owned behavior such as trigger listeners, authorization, message settlement, retry scheduling, scaling, and
the complete binding-extension lifecycle still needs Core Tools, emulator, or deployed end-to-end tests.

## Verification

- Core factory and invocation tests.
- ASP.NET Core tests on supported target frameworks.
- Service Bus and Blob trigger-input tests.
- In-memory and loopback-gRPC transport conformance tests.
- Package installation and compatibility checks.
- Runtime smoke tests for the behavior that requires the real Functions host.

## AI disclosure

This is an AI-implemented slop-fest, submitted and owned by Vladyslav Panasenko.

That wording is intentional. A lot of the implementation and tests were produced with AI assistance. Please do
not treat that as proof that the design or code is correct. Review it critically, run the tests, and point out
anything that is unnecessary, unsafe, or overcomplicated.

The main goal of this PR is to highlight a real testing gap. Azure Functions needs this kind of testing support,
especially as more code is produced with AI. The worker startup and invocation pipeline is too critical for this
layer to be missing.

Suggested reviewers: @fabiocav, @brettsam, and @satvu.

Please keep feedback focused on the code and design, and follow the
[Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).

## Contributor notes

- No package is published by this PR.
- This does not need to be backported.
- Documentation and release notes are included.
- If the CLA bot asks, I will complete the Microsoft Contributor License Agreement.
