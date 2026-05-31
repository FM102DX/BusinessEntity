# Business Entity Engine

[Russian README](README.ru.md)

Business Entity Engine is a self-deployable platform for building knowledge bases and business-object applications, where the core data model is built around a graph of business entities.

The current application is an ASP.NET Core / Blazor Server system that includes:

- spaces as the top-level work context;
- folders, documents and rich documents as business entities;
- graph relations between entities;
- document and rich-document editing;
- authentication through Authentik;
- administration of users, roles, groups and access rights;
- deployment assets for Docker Compose;
- an experimental mini-app/plugin model direction.

## Project Status

Business Entity Engine is at the public incubation / preview stage.

At this stage, the platform intentionally remains as open as possible for practical feedback. You can install it, fork it, modify it, test it, use it for prototypes, write plugins and validate it in real scenarios.

Important preview-stage notes:

- APIs, storage layout, deployment scripts and migration mechanics may change.
- This is not yet a polished production distribution.
- Security, backup, storage and the access-rights model are still being hardened.
- Before exposing an instance to the network, carefully review configuration and secrets.

## License

The source code and documentation in this repository are distributed under the MIT License. See [LICENSE](LICENSE).

At the public incubation stage, the current public branch of Business Entity Engine is distributed under the MIT License. After a stable version is released, the project may introduce other licensing options, including more closed commercial editions or distributions. At the same time, the last version published under the MIT License will be fixed as an MIT version and will remain available in that capacity forever.

The MIT License applies to code and documentation. It does not transfer rights to use the Business Entity name, logo, icons, visual identity, official project status, names of official releases, official domains or official distribution channels in a way that may mislead users.

See [TRADEMARK.md](TRADEMARK.md) for the brand and trademark policy.

## Brand Boundary

Forks and derived projects are welcome.

You may correctly write:

- "Fork of Business Entity Engine"
- "Powered by Business Entity Engine"
- "Compatible with Business Entity"
- "Unofficial plugin for Business Entity"
- "Built on Business Entity Engine"

You must not present a fork, service, hosting offering, package, organization or website as the official Business Entity without written permission from the brand owner.

## Repository Layout

```text
BusinessEntity/              Main ASP.NET Core / Blazor Server application
BlazorServerWebLogger/       Web logging service
BusinessEntity.Resources/    Shared resources
BusinessEntityStorage/       Local runtime storage, not part of the public release
Deployment/                  Release bundle files and install scripts
Context/                     Architecture notes and project policies
Powershell/                  Scripts for development and release bundle builds
docker-compose.yml           Development Docker Compose stack
CHANGELOG.md                 Changelog
LICENSE                      MIT license
TRADEMARK.md                 Brand and trademark policy
```

## Running From Source

Requirements:

- Docker-compatible runtime;
- Docker Compose plugin;
- .NET SDK 6.0 if the application is built or run outside Docker.

For a local development stack:

```powershell
docker network create docker-business-entity-common-bridge
docker compose up -d --build
```

Default local endpoints:

- Business Entity: `http://localhost:7000`
- Authentik: `http://localhost:9000`
- Web Logger: `http://localhost:5080`

The root `docker-compose.yml` is development-oriented. Before public deployment, replace development secrets, review ports, review Authentik settings and use deployment assets from [Deployment](Deployment/README.md).

## Deployment

The target deployment model is a release bundle for a single Windows or Linux host with Docker Compose.

See:

- [Deployment README](Deployment/README.md)
- [Deployment policy](Context/Policy/deployment-policy.md)

The target operator flow:

```text
download release bundle
unpack
run install.ps1 / install.bat / install.sh
start Docker Compose stack
load initial data
open the application
```

## Checklist Before Public Publishing

Before publishing the repository, verify that it does not contain:

- real production secrets;
- private Authentik tokens;
- private database dumps;
- local storage with user files;
- backups;
- customer or personal data;
- private logs;
- machine-specific paths that should not be public.

Any secret that has ever been committed must be rotated before the public repository can be considered safe.

## Security

Do not publish vulnerabilities in open issues if the report contains exploitation details.

Until a separate `SECURITY.md` is created, report security concerns privately to the project maintainers. Where possible, include affected version, deployment mode, steps to reproduce, logs and a minimal proof of concept.

Security-sensitive areas: authentication, authorization, access-right checks, file uploads, HTML handling in rich documents, imports, backups, path handling and plugin boundaries.

## Contributing

The repository is currently at an early public incubation stage. Practical feedback is especially useful:

- installation problems;
- deployment feedback;
- bug reports;
- security reports;
- feedback on storage and migration;
- feedback on the permission model;
- rich-document editing scenarios;
- experiments with plugins and mini-apps.

It is better to submit small changes with a clear description of the scenario they improve.

## Official Status

Only repositories, releases, packages, websites and services explicitly published by the Business Entity maintainers are considered official Business Entity distributions.

Forks and community builds must clearly identify themselves as unofficial.
