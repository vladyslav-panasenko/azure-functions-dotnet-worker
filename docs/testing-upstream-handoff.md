# Preview testing packages: upstream handoff

Status: local and CI artifacts only. This work does not publish packages, mutate an external issue, use release credentials, or make a Microsoft support commitment.

## Proposed package IDs

- `Microsoft.Azure.Functions.Worker.Testing`
- `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing`

Both use an independent `1.0.0-preview.n` line. Product and release owners must authorize any later publication through a separate release workflow and approved plan.

## Reviewer checklist

- [ ] API review covers factory cloning/startup, invocation/value/result models, built-in HTTP naming, capability flags, ASP.NET activation, and additive retry exception projection.
- [ ] Exact dependency matrix is read from each package nuspec and packaged `buildTransitive` compatibility manifest.
- [ ] Package validation, signing, Source Link, XML documentation, readmes, and net8/net9/net10 assets pass.
- [ ] Transport report has zero unexplained differences on net8 and net10.
- [ ] Runtime differential report has zero unexplained differences for built-in HTTP, ASP.NET HTTP, and Azurite queue output.
- [ ] Startup and warm-invocation benchmark JSON and Markdown artifacts are attached separately.
- [ ] Security and legal review confirms no credential flow, network listener in the in-process factory, external mutation, or new licensing obligation.
- [ ] `docs/testing.md` accurately distinguishes worker integration from host and end-to-end tests.
- [ ] Built-in and ASP.NET sample fixtures restore and pass against only the local package feed.
- [ ] Intentional Worker, Worker.Grpc, ASP.NET extension, core testing, and TestHost version skew fails at restore or startup.

## Proposed issue update for an authorized owner

An upstream owner may later summarize that a preview, worker-owned integration-testing design is available for review. The text must describe its fidelity boundary, link the exact dependency matrix and conformance evidence, and avoid implying that synthetic triggers reproduce Functions-host behavior. This repository change does not post that text.

## Recommended preview gates

1. No unexplained transport or enumerated runtime-differential mismatch.
2. Green unit, integration, stress, leak, package sample, and multi-target jobs.
3. Signed local packages with Source Link and exact closed dependency ranges.
4. API, security, legal, documentation, and release-engineering approval.
5. Explicit product-owner authorization for package IDs, publication, issue update, and support posture.

## Recommended GA gates

1. Preview feedback resolves API and compatibility issues without broadening fidelity claims.
2. A documented compatibility release process ships aligned testing packages with every supported worker line.
3. Benchmark history establishes reviewed regression thresholds from CI distributions, not local smoke values.
4. Maintainers own the host-capture and ASP.NET bridge synchronization strategy.
5. Product owners define support and servicing commitments.
