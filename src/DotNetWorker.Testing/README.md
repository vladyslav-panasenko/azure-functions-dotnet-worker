# Azure Functions isolated worker testing

`Microsoft.Azure.Functions.Worker.Testing` is a preview integration-test host for executing an isolated worker application in process.

It loads generated function metadata, runs the real worker middleware and converters, and supports synthetic function invocation plus function-targeted built-in HTTP. It does not provide Functions-host URL routing, authorization, trigger listeners, message settlement, retry scheduling, or scale behavior.

See the repository's `docs/testing.md` for the testing pyramid, lifecycle guidance, examples, and the exact package compatibility policy.
