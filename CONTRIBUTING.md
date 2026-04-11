# Contributing

## Goals

Pipelinez is a reusable library, so contributions should optimize not only for correctness but also for maintainability, API clarity, and consumer confidence.

## Development Basics

- install the .NET 8 SDK
- keep Docker running when you want to run Kafka integration tests locally
- build the solution with `dotnet build src/Pipelinez.sln`
- run the full test suite with `dotnet test src/Pipelinez.sln`
- if you change performance-sensitive behavior, consider running the benchmark project too
- if you change package metadata or release behavior, validate local packages with `./scripts/Validate-Packages.ps1 -PackageDirectory artifacts/packages`
- if you change public API documentation or docs-site configuration, build generated docs with `dotnet docfx docs-site/docfx.json`

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

## Public API XML Documentation

All public packages enforce XML documentation coverage for public APIs during normal builds.

That means:

- every new public type needs a meaningful XML `<summary>`
- public constructors, methods, properties, and events need XML docs or intentional `<inheritdoc />`
- option/configuration properties should document important defaults and behavior interactions
- event docs should call out important ordering or lifecycle guarantees when they matter

The compiler enforces this with `CS1591` as an error for `Pipelinez`, `Pipelinez.Kafka`, and `Pipelinez.PostgreSql`.

If you add or change a public API, make the XML documentation update in the same pull request.

Generated API documentation is built with DocFX:

```powershell
dotnet tool restore
dotnet docfx docs-site/docfx.json
```

The generated site is deployed from `main` through the `Docs` GitHub Actions workflow.

## Documentation

If your change affects how consumers use the library, update the relevant docs in the same pull request.

That usually means some combination of:

- `README.md`
- `docs/Overview.md`
- `docs/ApiStability.md`
- examples under `src/examples`

## Pull Requests

Keep pull requests focused and make public API changes explicit in the description. If your PR changes consumer-facing behavior, call that out directly so reviewers can evaluate compatibility impact separately from implementation details.

## Release Impact

Every non-trivial pull request should be easy to classify for release notes.

Use this vocabulary in PR descriptions when it applies:

- `patch`
  bug fixes, documentation fixes, safe diagnostics improvements, or packaging fixes
- `minor`
  additive APIs, new features, new integrations, or preview APIs
- `major`
  breaking changes to stable APIs or runtime behavior that requires migration

The `Pipelinez` and `Pipelinez.Kafka` packages are versioned together. A release should never intentionally publish different versions for the two packages.

## Release Workflow

Public releases are tag-based and use NuGet Trusted Publishing rather than a long-lived NuGet API key.

Maintainer release flow:

1. merge release-ready work into `main`
2. confirm CI is green
3. update `CHANGELOG.md`
4. create a tag like `v1.2.3` or `v1.3.0-preview.1`
5. push the tag
6. let the release workflow validate, pack, publish to NuGet.org, and create a GitHub Release

Manual release workflow dispatch is intended for package validation and does not publish to NuGet.org.

## Agent-Assisted Maintenance

Small, well-scoped repository maintenance work can be assigned to GitHub Copilot coding agent when the issue includes clear acceptance criteria.

Good agent-assisted tasks include:

- documentation refreshes
- release-note draft preparation
- issue-template updates
- small regression-test additions
- package smoke-test improvements

Keep these tasks human-controlled:

- approving release versions
- creating release tags
- approving publish environments
- publishing packages
- deciding breaking-change impact
- security vulnerability disclosure

Agent-created PRs must still pass normal validation and receive human review before merge.
