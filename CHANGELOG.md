# Changelog

All notable changes to Pipelinez will be documented in this file.

Pipelinez uses [Semantic Versioning](https://semver.org/). The `Pipelinez`, `Pipelinez.Kafka`, `Pipelinez.AzureServiceBus`, `Pipelinez.RabbitMQ`, `Pipelinez.PostgreSql`, and `Pipelinez.SqlServer` packages ship with aligned versions.

## [Unreleased]

### Added

- Added the `Pipelinez.AzureServiceBus` transport package with queue and topic subscription sources, queue/topic destinations, dead-letter publishing, competing-consumer worker support, docs, examples, and approval/unit test coverage.
- Extended CI, PR, release, docs, ownership, and package smoke validation workflows so `Pipelinez.AzureServiceBus` is validated and published with the existing package set.
- Added the `Pipelinez.RabbitMQ` transport package with queue sources, exchange/default-exchange destinations, RabbitMQ dead-letter publishing, manual ack/nack source settlement, competing-consumer worker support, docs, examples, approval/unit tests, and Testcontainers integration coverage.
- Extended CI, PR, release, docs, and package smoke validation workflows so `Pipelinez.RabbitMQ` is validated and published with the existing package set.
- Added the `Pipelinez.SqlServer` transport package with SQL Server destination writes, dead-letter writes, consumer-owned schema mapping, custom SQL support, docs, examples, approval/unit tests, and Testcontainers integration coverage.
- Extended CI, PR, release, docs, and package smoke validation workflows so `Pipelinez.SqlServer` is validated and published with the existing package set.

## [1.0.0] - 2026-04-07

### Added

- Added a tag-based release workflow and repository setup for public NuGet publishing through NuGet Trusted Publishing.
- Added baseline OSS repository files for issue templates, ownership, security reporting, and release tracking.
- Published the first public `Pipelinez` and `Pipelinez.Kafka` packages to NuGet.org.

### Changed

- Centralized package version defaults so both package projects resolve the same version.

### Migration Notes

- No migration required for the first public release.
