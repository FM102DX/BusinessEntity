# Deployment policy context

Эта папка хранит контекст по установке, облачному deploy, обновлению и раскатке `BusinessEntity` на Windows/Linux хостах.

Основной документ:

- `deployment-policy.md` - политика release bundle, install/update lifecycle, production bootstrap, backup/rollback и deploy scripts.

Связанные рабочие артефакты в репозитории:

- `Deployment/docker-compose.yml`
- `Deployment/.env.example`
- `Deployment/install.ps1`
- `Deployment/install.bat`
- `Deployment/deploy.ps1`
- `Deployment/scripts/bootstrap-initial-data.ps1`
- `Powershell/Build-ReleaseBundle.ps1`

Эти артефакты остаются в своих production/build директориях. В эту папку переносится только policy-контекст и навигация по связанным файлам.
