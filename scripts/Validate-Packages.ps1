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
$azureServiceBusProjectPath = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'Pipelinez.AzureServiceBus' 'Pipelinez.AzureServiceBus.csproj')
$rabbitMqProjectPath = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'Pipelinez.RabbitMQ' 'Pipelinez.RabbitMQ.csproj')
$postgresProjectPath = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'Pipelinez.PostgreSql' 'Pipelinez.PostgreSql.csproj')

$coreVersion = Get-PackageVersion -ProjectPath $coreProjectPath -RepositoryRoot $repoRoot
$kafkaVersion = Get-PackageVersion -ProjectPath $kafkaProjectPath -RepositoryRoot $repoRoot
$azureServiceBusVersion = Get-PackageVersion -ProjectPath $azureServiceBusProjectPath -RepositoryRoot $repoRoot
$rabbitMqVersion = Get-PackageVersion -ProjectPath $rabbitMqProjectPath -RepositoryRoot $repoRoot
$postgresVersion = Get-PackageVersion -ProjectPath $postgresProjectPath -RepositoryRoot $repoRoot

$packageVersions = @(
    [pscustomobject]@{ Name = 'Pipelinez'; Version = $coreVersion },
    [pscustomobject]@{ Name = 'Pipelinez.Kafka'; Version = $kafkaVersion },
    [pscustomobject]@{ Name = 'Pipelinez.AzureServiceBus'; Version = $azureServiceBusVersion },
    [pscustomobject]@{ Name = 'Pipelinez.RabbitMQ'; Version = $rabbitMqVersion },
    [pscustomobject]@{ Name = 'Pipelinez.PostgreSql'; Version = $postgresVersion }
)

$referenceVersion = $packageVersions[0].Version
$mismatchedPackages = @($packageVersions | Where-Object { $_.Version -ne $referenceVersion })

if ($mismatchedPackages.Count -gt 0)
{
    $versionSummary = ($packageVersions | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Version }) -join ' '
    throw "Pipelinez package versions must match. $versionSummary"
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

    $azureServiceBusSmokeDirectory = Join-Path $tempRoot 'AzureServiceBusSmoke'
    Invoke-DotNet -Arguments @('new', 'console', '--framework', 'net8.0', '--output', $azureServiceBusSmokeDirectory)
    Invoke-DotNet -Arguments @('add', (Join-Path $azureServiceBusSmokeDirectory 'AzureServiceBusSmoke.csproj'), 'package', 'Pipelinez.AzureServiceBus', '--version', $azureServiceBusVersion, '--source', $resolvedPackageDirectory)
    @"
using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;
using Pipelinez.Core.Record;

var connection = new AzureServiceBusConnectionOptions
{
    ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
};

var pipeline = Pipeline<AzureServiceBusSmokeRecord>.New("orders")
    .WithAzureServiceBusSource(
        new AzureServiceBusSourceOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-in")
        },
        message => new AzureServiceBusSmokeRecord
        {
            Id = message.MessageId,
            Value = message.Body.ToString()
        })
    .WithAzureServiceBusDestination(
        new AzureServiceBusDestinationOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-out")
        },
        record => new ServiceBusMessage(BinaryData.FromString(record.Value))
        {
            MessageId = record.Id
        })
    .Build();

Console.WriteLine(pipeline.GetStatus().Status);

public sealed class AzureServiceBusSmokeRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required string Value { get; init; }
}
"@ | Set-Content -Path (Join-Path $azureServiceBusSmokeDirectory 'Program.cs')
    Invoke-DotNet -Arguments @('build', (Join-Path $azureServiceBusSmokeDirectory 'AzureServiceBusSmoke.csproj'), '--configfile', $nugetConfigPath, '--configuration', 'Release')

    $rabbitMqSmokeDirectory = Join-Path $tempRoot 'RabbitMqSmoke'
    Invoke-DotNet -Arguments @('new', 'console', '--framework', 'net8.0', '--output', $rabbitMqSmokeDirectory)
    Invoke-DotNet -Arguments @('add', (Join-Path $rabbitMqSmokeDirectory 'RabbitMqSmoke.csproj'), 'package', 'Pipelinez.RabbitMQ', '--version', $rabbitMqVersion, '--source', $resolvedPackageDirectory)
    @"
using System.Text;
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;

var connection = new RabbitMqConnectionOptions
{
    Uri = new Uri("amqp://guest:guest@localhost:5672/")
};

var pipeline = Pipeline<RabbitMqSmokeRecord>.New("orders")
    .WithRabbitMqSource(
        new RabbitMqSourceOptions
        {
            Connection = connection,
            Queue = RabbitMqQueueOptions.Named("orders-in")
        },
        delivery => new RabbitMqSmokeRecord
        {
            Id = delivery.Properties?.MessageId ?? delivery.DeliveryTag.ToString(),
            Value = Encoding.UTF8.GetString(delivery.Body.Span)
        })
    .WithRabbitMqDestination(
        new RabbitMqDestinationOptions
        {
            Connection = connection,
            Exchange = "orders",
            RoutingKey = "processed"
        },
        record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Value)))
    .Build();

Console.WriteLine(pipeline.GetStatus().Status);

public sealed class RabbitMqSmokeRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required string Value { get; init; }
}
"@ | Set-Content -Path (Join-Path $rabbitMqSmokeDirectory 'Program.cs')
    Invoke-DotNet -Arguments @('build', (Join-Path $rabbitMqSmokeDirectory 'RabbitMqSmoke.csproj'), '--configfile', $nugetConfigPath, '--configuration', 'Release')

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
