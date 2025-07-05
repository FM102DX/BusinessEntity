# Скрипт удаляет указанные Docker сети, отключая от них контейнеры

param(
    [string[]]$NetworksToRemove = @(
        # Стандартно сеть BusinessEntity больше не существует; оставляем список пустым,
        'docker-networkmall2',
        'shared-bridge-network'
    )
)

foreach ($network in $NetworksToRemove) {
    Write-Host "Processing network '$network'..." -ForegroundColor Cyan

    # Получаем контейнеры, присоединённые к сети
    $containersJson = docker network inspect $network --format '{{json .Containers}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ⚠ Network '$network' not found or inspect failed." -ForegroundColor Yellow
        continue
    }

    $containers = $null
    if ($containersJson -and $containersJson -ne 'null') {
        $containers = $containersJson | ConvertFrom-Json
    }

    # Отключаем контейнеры от сети
    if ($containers) {
        foreach ($prop in $containers.PSObject.Properties) {
            $containerId = $prop.Name
            Write-Host "  Disconnecting container $containerId from $network..." -ForegroundColor Yellow
            docker network disconnect -f $network $containerId | Out-Null
        }
    }

    # Удаляем сеть
    Write-Host "  Removing network $network..." -ForegroundColor Yellow
    docker network rm $network | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Network '$network' removed." -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Failed to remove network '$network'." -ForegroundColor Red
    }
}

Write-Host "Cleanup completed." -ForegroundColor Green 