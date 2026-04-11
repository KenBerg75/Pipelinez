# Security Policy

## Supported Versions

Pipelinez is preparing for public package releases. Until the first public release is published, security fixes are handled on the `main` branch.

After public releases begin, the project intends to support the latest stable minor release line for security fixes unless release notes state otherwise.

## Reporting A Vulnerability

Please do not open a public GitHub issue for security vulnerabilities.

Use GitHub private vulnerability reporting:

https://github.com/KenBerg75/Pipelinez/security/advisories/new

If private vulnerability reporting is unavailable, contact the repository maintainer through GitHub and avoid sharing exploit details publicly.

## What To Include

Please include:

- affected package and version, if known
- affected transport or runtime area
- reproduction steps or proof-of-concept details
- expected impact
- any known mitigations

## Disclosure

Maintainers will review the report, coordinate a fix, and publish release notes or an advisory when appropriate. Vulnerability disclosure timing should remain human-controlled and should not be delegated to automated or agentic workflows.

## Automated Security Controls

The repository uses source-controlled automation to reduce supply-chain and code security risk:

- Dependabot monitors NuGet packages, .NET tools, and GitHub Actions.
- Dependency Review evaluates dependency changes on pull requests and blocks high-severity runtime vulnerabilities.
- CodeQL analyzes the C# source on pull requests, pushes to `main`, scheduled runs, and manual dispatch.
- OpenSSF Scorecard runs on a schedule and uploads SARIF results to GitHub code scanning.
- Release validation generates an SPDX JSON SBOM from the packed NuGet artifacts and publishes it with release artifacts.

Repository-level GitHub security settings still need to remain enabled by maintainers, including Dependabot alerts, Dependabot security updates, secret scanning, push protection when available, code scanning, and private vulnerability reporting.
