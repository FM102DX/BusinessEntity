# BusinessEntity Release Bundle

This folder is copied into a release bundle by `Powershell/Build-ReleaseBundle.ps1`.

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
