param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'

function Get-PackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    [xml]$project = Get-Content -Path $ProjectPath
    $propertyGroups = @($project.Project.PropertyGroup)

    foreach ($propertyName in 'PackageVersion', 'Version', 'VersionPrefix')
    {
        foreach ($group in $propertyGroups)
        {
            $value = $group.$propertyName
            if ($null -ne $value -and -not [string]::IsNullOrWhiteSpace($value))
            {
                return $value.Trim()
            }
        }
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

$coreVersion = Get-PackageVersion -ProjectPath $coreProjectPath
$kafkaVersion = Get-PackageVersion -ProjectPath $kafkaProjectPath

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

    Write-Host "Package smoke validation succeeded."
}
finally
{
    if (Test-Path $tempRoot)
    {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
