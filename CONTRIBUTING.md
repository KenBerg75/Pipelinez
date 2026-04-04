# Contributing

## Goals

Pipelinez is a reusable library, so contributions should optimize not only for correctness but also for maintainability, API clarity, and consumer confidence.

## Development Basics

- build the solution with `dotnet build src/Pipelinez.sln`
- run the full test suite with `dotnet test src/Pipelinez.sln`
- if you change performance-sensitive behavior, consider running the benchmark project too

## Public API Changes

Public API changes need a slightly higher bar than internal refactors.

When a pull request changes the public surface of `Pipelinez` or `Pipelinez.Kafka`:

- decide whether the API is intended to be stable or preview
- avoid exposing low-level implementation details unless they are deliberate extension points
- update the public API approval baseline intentionally
- update docs and examples when consumer usage changes
- provide migration guidance if an existing API is being replaced

### Stable vs Preview

- stable APIs are part of the compatibility promise for the current major version
- preview APIs are public but intentionally marked as still evolving

Preview APIs should use `ExperimentalAttribute` so consumers get an explicit code-level signal.

### Obsoleting APIs

Do not remove a stable API without a migration path.

Preferred flow:

1. add the replacement
2. mark the old API with `[Obsolete(..., error: false)]`
3. document the migration path
4. remove the old API only in the next major version

## Public API Approval Tests

The repository uses approval tests to guard the public surface of:

- `Pipelinez`
- `Pipelinez.Kafka`

If you intentionally changed the public API, refresh the baselines with:

```powershell
$env:PIPELINEZ_UPDATE_API_BASELINES='1'
dotnet test src/tests/Pipelinez.Tests/Pipelinez.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.Kafka.Tests/Pipelinez.Kafka.Tests.csproj --filter ApiApprovalTests
```

Then run the full suite:

```powershell
dotnet test src/Pipelinez.sln --logger "console;verbosity=minimal"
```

## Documentation

If your change affects how consumers use the library, update the relevant docs in the same pull request.

That usually means some combination of:

- `README.md`
- `docs/Overview.md`
- `docs/ApiStability.md`
- examples under `src/examples`

## Pull Requests

Keep pull requests focused and make public API changes explicit in the description. If your PR changes consumer-facing behavior, call that out directly so reviewers can evaluate compatibility impact separately from implementation details.
