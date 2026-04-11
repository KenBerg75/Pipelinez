# API Stability

Pipelinez now treats its public API as an intentional contract rather than an incidental byproduct of the implementation.

## Stability Levels

### Stable

Stable APIs are safe for consumers to depend on within the current major version.

Examples of stable surface area include:

- `Pipeline<T>.New(...)`
- `PipelineBuilder<T>`
- `IPipeline<T>`
- `PipelineRecord`
- source, segment, and destination abstractions
- user-facing options, status, event, performance, flow-control, retry, dead-letter, distributed, and operational models
- Kafka builder extensions and Kafka configuration models

Stable APIs should only change in additive, source-compatible ways until the next major release.

### Preview

Preview APIs are public but intentionally marked as still evolving. When Pipelinez introduces a public API that is not yet covered by the stable compatibility promise, it should be marked with `ExperimentalAttribute` and called out in release communication.

Example shape:

```csharp
using System.Diagnostics.CodeAnalysis;

[Experimental("PIPELINEZ001")]
public sealed class FutureTransportOptions
{
    public bool EnableSomethingStillEvolving { get; init; }
}
```

Pipelinez does not currently ship any public APIs marked as preview.

### Internal

Types that are not intended as consumer extension points should remain internal. This includes low-level transport plumbing and helper types that exist only to support the runtime implementation.

## Compatibility Rules

Pipelinez uses SemVer-style versioning for public releases. `Pipelinez` and `Pipelinez.Kafka` should be published with the same version number in each release.

### Patch Releases

Patch releases may include:

- bug fixes
- performance improvements
- non-breaking diagnostics improvements
- documentation clarifications

Patch releases should not remove public APIs or make existing supported usage stop compiling.

### Minor Releases

Minor releases may include:

- new additive APIs
- new events
- new options with safe defaults
- new transports or integrations
- preview APIs

Minor releases should preserve compatibility for existing stable APIs.

### Major Releases

Major releases may include:

- API removals
- renamed members
- signature changes
- required migration work for stable consumer code

## Obsoletion Policy

When a stable API needs to be replaced:

1. a supported replacement should exist first
2. the old API should be marked `[Obsolete(..., error: false)]`
3. migration guidance should be added to release communication and docs
4. the obsolete API should remain available until the next major release unless explicitly documented otherwise

## Public API Enforcement

The repository now includes public API approval tests for:

- `Pipelinez`
- `Pipelinez.Kafka`

These tests compare the compiled public surface against checked-in approved baselines. Any unintended public API change causes the test suite to fail, which means the existing PR and CI workflows also catch it automatically.

The public package projects also enforce XML documentation coverage for public APIs. Missing XML docs on public members fail the build through `CS1591`, which keeps IntelliSense quality aligned with the approved public surface.

Approved baselines live in:

- `src/tests/Pipelinez.Tests/ApprovedApi/Pipelinez.publicapi.txt`
- `src/tests/Pipelinez.Kafka.Tests/ApprovedApi/Pipelinez.Kafka.publicapi.txt`

To intentionally refresh the baselines after an approved public API change:

```powershell
$env:PIPELINEZ_UPDATE_API_BASELINES='1'
dotnet test src/tests/Pipelinez.Tests/Pipelinez.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.Kafka.Tests/Pipelinez.Kafka.Tests.csproj --filter ApiApprovalTests
```

Then run the full test suite normally before merging.

## Contributor Expectations

If a change affects a public API:

- decide whether the API is stable or preview
- update the approved API baseline intentionally
- update or add XML documentation for the changed public members
- update consumer-facing docs when usage changes
- add migration guidance when replacing an existing API

For broader contributor guidance, see [CONTRIBUTING.md](https://github.com/KenBerg75/Pipelinez/blob/main/CONTRIBUTING.md).

## Release Communication

Release notes should make compatibility impact explicit:

- patch releases should call out fixes and safe behavior changes
- minor releases should call out additive APIs and new features
- major releases should include migration guidance
- preview releases should be marked clearly as preview

Release notes are tracked in the repository changelog and mirrored into GitHub Releases.
