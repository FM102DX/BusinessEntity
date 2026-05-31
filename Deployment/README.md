# BusinessEntity Release Bundle

This folder contains the installation template for a packaged Business Entity release.

In simple terms: the application itself lives in the main project folders, but this
folder describes how to deliver it to another machine. The release builder
`Powershell/Build-ReleaseBundle.ps1` copies these files into a downloadable archive.
After that, a user can unpack the archive, run `install.ps1` or `install.bat`, and
get a local Docker-based Business Entity instance without manually assembling the
Compose stack.

This folder is useful for deployment and first installation. It is not required for
day-to-day local development from source.

The folder contains:

- `docker-compose.yml` - the Docker Compose stack used by the release bundle;
- `.env.example` - a template for local deployment settings and secrets;
- `install.ps1` / `install.bat` - first-run installer scripts;
- `deploy.ps1` - helper commands for status, logs, restart and stop;
- `scripts/bootstrap-initial-data.ps1` - initial data/bootstrap helper;
- `README.md` - this deployment note.

## Install

Windows:

```powershell
.\install.ps1
```

or:

```cmd
install.bat
```

The installer:

- creates `.env` from `.env.example`;
- generates missing secrets;
- creates local runtime folders;
- loads offline Docker images from `images/*.tar`, if present;
- creates the shared Docker network;
- runs `docker compose up -d`;
- waits for basic HTTP health checks.

Docker Desktop, Docker Engine, or another Docker-compatible runtime must already be installed.

## Operations

```powershell
.\deploy.ps1 status
.\deploy.ps1 logs -Service business-entity
.\deploy.ps1 restart
.\deploy.ps1 stop
```

Initial application data is currently created by application startup/bootstrap. Authentik OIDC bootstrap is still a separate deployment task until application bootstrap is implemented as a first-class service.
