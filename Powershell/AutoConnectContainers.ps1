# Автоматическое подключение новых контейнеров к общей сети
# Этот скрипт можно запускать после создания новых контейнеров

$bridgeNetwork = "shared-bridge-network"

function Auto-ConnectNewContainers {
    Write-Host "=== Автоматическое подключение контейнеров к общей сети ===" -ForegroundColor Green
    
    # Проверяем, существует ли общая сеть
    $existingNetwork = docker network ls --filter "name=$bridgeNetwork" --format "{{.Name}}"
    
    if (-not $existingNetwork) {
        Write-Host "Общая сеть '$bridgeNetwork' не найдена. Сначала запустите ConnectDockerNetworks.ps1" -ForegroundColor Red
        return
    }
    
    # Получаем все запущенные контейнеры
    $allContainers = docker ps --format "{{.Names}}"
    
    if (-not $allContainers) {
        Write-Host "Нет запущенных контейнеров." -ForegroundColor Blue
        return
    }
    
    # Получаем контейнеры, уже подключенные к общей сети
    $bridgeNetworkInfo = docker network inspect $bridgeNetwork | ConvertFrom-Json
    $connectedContainers = @()
    
    if ($bridgeNetworkInfo[0].Containers) {
        foreach ($container in $bridgeNetworkInfo[0].Containers.PSObject.Properties.Value) {
            $connectedContainers += $container.Name
        }
    }
    
    # Подключаем неподключенные контейнеры
    $newConnections = 0
    foreach ($container in $allContainers) {
        if ($container -notin $connectedContainers) {
            Write-Host "Подключаем новый контейнер '$container' к общей сети..." -ForegroundColor Yellow
            docker network connect $bridgeNetwork $container
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✓ Контейнер '$container' успешно подключен." -ForegroundColor Green
                $newConnections++
            } else {
                Write-Host "  ✗ Ошибка подключения контейнера '$container'." -ForegroundColor Red
            }
        }
    }
    
    if ($newConnections -eq 0) {
        Write-Host "Все контейнеры уже подключены к общей сети." -ForegroundColor Blue
    } else {
        Write-Host "Подключено новых контейнеров: $newConnections" -ForegroundColor Green
    }
}

# Запуск
Auto-ConnectNewContainers