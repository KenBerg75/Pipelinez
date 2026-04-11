# Repository Topics

Repository topics improve GitHub search and make Pipelinez easier to discover for developers looking for .NET pipeline, Kafka, PostgreSQL, retry, dead-letter, and observability libraries.

## Recommended Topics

The canonical topic list is maintained in [`scripts/Set-RepositoryTopics.ps1`](https://github.com/KenBerg75/Pipelinez/blob/main/scripts/Set-RepositoryTopics.ps1):

- `dotnet`
- `csharp`
- `pipeline`
- `dataflow`
- `etl`
- `messaging`
- `kafka`
- `postgresql`
- `dead-letter`
- `retry`
- `observability`
- `stream-processing`
- `tpl-dataflow`
- `background-workers`

## Apply Topics

Install and authenticate GitHub CLI:

```powershell
winget install --id GitHub.cli --exact
gh auth login
```

Then run:

```powershell
./scripts/Set-RepositoryTopics.ps1
```

The script normalizes, validates, applies, and verifies the repository topics.

## Token-Based Usage

For automation or non-interactive maintainer shells, provide a token with repository administration permissions:

```powershell
$env:GH_TOKEN = '<token>'
./scripts/Set-RepositoryTopics.ps1
```

## When To Revisit

Review topics when:

- a new transport package is released
- README/package tags change
- a major feature area becomes stable
- repository positioning changes

Topics should describe real shipped capabilities, not aspirational roadmap items.
