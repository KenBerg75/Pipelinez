# Release Checklist

Audience: maintainers publishing Pipelinez packages.

## Before Release

- Confirm `CHANGELOG.md` has an entry for the release.
- Run `dotnet build src/Pipelinez.sln --configuration Release`.
- Run `dotnet test src/Pipelinez.sln --configuration Release`.
- Run RabbitMQ integration tests with Docker available when the RabbitMQ package changed.
- Pack all packages locally if package metadata changed.
- Run `scripts/Validate-Packages.ps1` against the local package output.

## Package Set

Pipelinez packages ship with aligned versions:

- `Pipelinez`
- `Pipelinez.Kafka`
- `Pipelinez.AzureServiceBus`
- `Pipelinez.RabbitMQ`
- `Pipelinez.PostgreSql`

Verify each package has:

- the intended package version
- XML documentation
- symbols package
- package README
- SourceLink metadata
- expected dependencies

## Release

- Push a SemVer tag such as `v1.2.3` or `v1.3.0-preview.1`.
- Monitor `.github/workflows/release.yaml`.
- Confirm package validation completed before publish.
- Confirm NuGet Trusted Publishing completed.
- Confirm the GitHub Release was created.

## After Release

- Verify every package appears on NuGet.org.
- Verify `Pipelinez.RabbitMQ` appears when the release includes RabbitMQ.
- Verify generated release notes are accurate enough for users.
- Announce the release if appropriate.
