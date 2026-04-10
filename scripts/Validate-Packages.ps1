param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [string]$PackageVersion
)

$ErrorActionPreference = 'Stop'

function Get-XmlPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$PropertyNames
    )

    if (-not (Test-Path -Path $Path))
    {
        return $null
    }

    [xml]$xml = Get-Content -Path $Path
    $propertyGroups = @($xml.Project.PropertyGroup)

    foreach ($propertyName in $PropertyNames)
    {
        foreach ($group in $propertyGroups)
        {
            $values = @($group.$propertyName)
            foreach ($value in $values)
            {
                if ($null -eq $value)
                {
                    continue
                }

                $textValue = if ($value -is [System.Xml.XmlElement])
                {
                    $value.InnerText
                }
                else
                {
                    $value.ToString()
                }

                if ([string]::IsNullOrWhiteSpace($textValue))
                {
                    continue
                }

                $trimmedValue = $textValue.Trim()
                if ($trimmedValue.Contains('$('))
                {
                    continue
                }

                return $trimmedValue
            }
        }
    }

    return $null
}

function Get-PackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($PackageVersion))
    {
        return $PackageVersion.Trim()
    }

    $projectVersion = Get-XmlPropertyValue `
        -Path $ProjectPath `
        -PropertyNames @('PackageVersion', 'Version', 'VersionPrefix')

    if (-not [string]::IsNullOrWhiteSpace($projectVersion))
    {
        return $projectVersion
    }

    $directoryBuildPropsVersion = Get-XmlPropertyValue `
        -Path (Join-Path $RepositoryRoot 'Directory.Build.props') `
        -PropertyNames @('PackageVersion', 'VersionPrefix', 'Version')

    if (-not [string]::IsNullOrWhiteSpace($directoryBuildPropsVersion))
    {
        return $directoryBuildPropsVersion
    }

    return '1.0.0'
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$WorkingDirectory = (Get-Location).Path
    )

    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($Arguments -join ' ') failed."
    }
}

$resolvedPackageDirectory = (Resolve-Path -Path $PackageDirectory).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$coreProjectPath = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'Pipelinez' 'Pipelinez.csproj')
$kafkaProjectPath = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'Pipelinez.Kafka' 'Pipelinez.Kafka.csproj')
$postgresProjectPath = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'Pipelinez.PostgreSql' 'Pipelinez.PostgreSql.csproj')

$coreVersion = Get-PackageVersion -ProjectPath $coreProjectPath -RepositoryRoot $repoRoot
$kafkaVersion = Get-PackageVersion -ProjectPath $kafkaProjectPath -RepositoryRoot $repoRoot
$postgresVersion = Get-PackageVersion -ProjectPath $postgresProjectPath -RepositoryRoot $repoRoot

if ($coreVersion -ne $kafkaVersion)
{
    throw "Pipelinez and Pipelinez.Kafka package versions must match. Core: $coreVersion Kafka: $kafkaVersion"
}

if ($coreVersion -ne $postgresVersion)
{
    throw "Pipelinez, Pipelinez.Kafka, and Pipelinez.PostgreSql package versions must match. Core: $coreVersion Kafka: $kafkaVersion PostgreSql: $postgresVersion"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pipelinez-package-smoke-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try
{
    $nugetConfigPath = Join-Path $tempRoot 'NuGet.Config'
    $packageSourceValue = $resolvedPackageDirectory.Replace('\', '/')
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$packageSourceValue" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfigPath

    $coreSmokeDirectory = Join-Path $tempRoot 'CoreSmoke'
    Invoke-DotNet -Arguments @('new', 'console', '--framework', 'net8.0', '--output', $coreSmokeDirectory)
    Invoke-DotNet -Arguments @('add', (Join-Path $coreSmokeDirectory 'CoreSmoke.csproj'), 'package', 'Pipelinez', '--version', $coreVersion, '--source', $resolvedPackageDirectory)
    @"
using Pipelinez.Core;
using Pipelinez.Core.Record;

var pipeline = Pipeline<SmokeRecord>.New("smoke")
    .WithInMemorySource(new object())
    .WithInMemoryDestination("config")
    .Build();

Console.WriteLine(pipeline.GetStatus().Status);

public sealed class SmokeRecord : PipelineRecord
{
    public required string Id { get; init; }
}
"@ | Set-Content -Path (Join-Path $coreSmokeDirectory 'Program.cs')
    Invoke-DotNet -Arguments @('build', (Join-Path $coreSmokeDirectory 'CoreSmoke.csproj'), '--configfile', $nugetConfigPath, '--configuration', 'Release')

    $kafkaSmokeDirectory = Join-Path $tempRoot 'KafkaSmoke'
    Invoke-DotNet -Arguments @('new', 'console', '--framework', 'net8.0', '--output', $kafkaSmokeDirectory)
    Invoke-DotNet -Arguments @('add', (Join-Path $kafkaSmokeDirectory 'KafkaSmoke.csproj'), 'package', 'Pipelinez.Kafka', '--version', $kafkaVersion, '--source', $resolvedPackageDirectory)
    @"
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Configuration;

var sourceOptions = new KafkaSourceOptions
{
    BootstrapServers = "localhost:9092",
    TopicName = "orders-in",
    ConsumerGroup = "orders-smoke",
    SecurityProtocol = SecurityProtocol.Plaintext
};

var destinationOptions = new KafkaDestinationOptions
{
    BootstrapServers = "localhost:9092",
    TopicName = "orders-out",
    SecurityProtocol = SecurityProtocol.Plaintext
};

var pipeline = Pipeline<KafkaSmokeRecord>.New("orders")
    .WithKafkaSource(sourceOptions, (string key, string value) => new KafkaSmokeRecord
    {
        Key = key,
        Value = value
    })
    .WithKafkaDestination(destinationOptions, record => new Message<string, string>
    {
        Key = record.Key,
        Value = record.Value
    })
    .Build();

Console.WriteLine(pipeline.GetStatus().Status);

public sealed class KafkaSmokeRecord : PipelineRecord
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
"@ | Set-Content -Path (Join-Path $kafkaSmokeDirectory 'Program.cs')
    Invoke-DotNet -Arguments @('build', (Join-Path $kafkaSmokeDirectory 'KafkaSmoke.csproj'), '--configfile', $nugetConfigPath, '--configuration', 'Release')

    $postgresSmokeDirectory = Join-Path $tempRoot 'PostgreSqlSmoke'
    Invoke-DotNet -Arguments @('new', 'console', '--framework', 'net8.0', '--output', $postgresSmokeDirectory)
    Invoke-DotNet -Arguments @('add', (Join-Path $postgresSmokeDirectory 'PostgreSqlSmoke.csproj'), 'package', 'Pipelinez.PostgreSql', '--version', $postgresVersion, '--source', $resolvedPackageDirectory)
    @"
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.Mapping;

var options = new PostgreSqlDestinationOptions
{
    ConnectionString = "Host=localhost;Database=pipelinez;Username=postgres;Password=postgres"
};

var pipeline = Pipeline<PostgreSqlSmokeRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithPostgreSqlDestination(
        options,
        PostgreSqlTableMap<PostgreSqlSmokeRecord>.ForTable("app", "orders")
            .Map("order_id", record => record.Id)
            .MapJson("payload", record => record))
    .Build();

Console.WriteLine(pipeline.GetStatus().Status);

public sealed class PostgreSqlSmokeRecord : PipelineRecord
{
    public required string Id { get; init; }
}
"@ | Set-Content -Path (Join-Path $postgresSmokeDirectory 'Program.cs')
    Invoke-DotNet -Arguments @('build', (Join-Path $postgresSmokeDirectory 'PostgreSqlSmoke.csproj'), '--configfile', $nugetConfigPath, '--configuration', 'Release')

    Write-Host "Package smoke validation succeeded."
}
finally
{
    if (Test-Path $tempRoot)
    {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
