# Security Automation

Pipelinez uses GitHub-native automation to keep dependency drift visible, block risky dependency changes, and publish security signals for maintainers.

## What Runs

| Automation | Trigger | Purpose |
| --- | --- | --- |
| Dependabot | Weekly | Opens NuGet, .NET tool, and GitHub Actions update pull requests. |
| Dependency Review | Pull requests | Reviews dependency changes and fails high-severity runtime vulnerability introductions. |
| CodeQL | Pull requests, `main`, weekly, manual | Performs C# static analysis and publishes code scanning results. |
| OpenSSF Scorecard | Weekly, branch protection changes, manual | Evaluates repository supply-chain posture and publishes SARIF results. |
| Release SBOM | Release validation | Generates an SPDX JSON SBOM from packed NuGet artifacts. |

## Dependabot

Dependabot is configured in `.github/dependabot.yml`.

The configuration covers:

- the repository root for `.NET` tool updates such as DocFX
- the `src` tree for project and test package references
- GitHub Actions workflow dependencies

Dependabot pull requests should be reviewed like any other pull request. Grouped dependency updates are preferred for routine maintenance, but security updates may be handled independently when a vulnerable package needs a faster merge.

## Dependency Review

Dependency Review runs on pull requests and uses `.github/dependency-review-config.yml`.

The current policy is intentionally focused:

- fail on `high` or `critical` runtime vulnerabilities
- keep license checking disabled until the project adopts a formal license policy
- show OpenSSF package scorecard context where GitHub can provide it
- show patched versions when vulnerability data includes them

If a failure is not exploitable in Pipelinez, document the reasoning in the pull request before adding any exception.

## CodeQL

CodeQL runs with a manual .NET build so analysis sees the same compile path as CI.

The workflow restores and builds `src/Pipelinez.sln` in `Release` before analysis. This avoids relying on CodeQL autobuild heuristics and makes failures easier to correlate with normal CI failures.

Review CodeQL alerts before releases. A new alert should be triaged as one of:

- true positive requiring a fix
- false positive with a short explanation
- accepted risk with clear release-note and maintainer context

## OpenSSF Scorecard

OpenSSF Scorecard runs weekly and after branch protection changes.

The workflow publishes results through the OpenSSF API and uploads SARIF to GitHub code scanning. The score should be treated as a maintenance signal rather than an absolute release gate; score drops should trigger review, especially around branch protection, pinned actions, security policy, and token permissions.

## Release SBOM

The release workflow generates `pipelinez-packages.spdx.json` from the packed NuGet artifacts after package validation succeeds.

The SBOM is included in the `pipelinez-packages` workflow artifact and is attached to tag-based GitHub Releases alongside the NuGet packages and symbol packages.

## Manual Repository Settings

Source control cannot enable every GitHub security feature. Maintainers should confirm these repository settings are enabled:

- Dependabot alerts
- Dependabot security updates
- dependency graph
- secret scanning
- push protection, when available
- code scanning
- private vulnerability reporting

Branch protection or repository rules should require the normal PR build, dependency review, and CodeQL checks once the workflows have completed at least one successful run.
