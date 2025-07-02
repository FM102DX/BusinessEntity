# Скрипт для объединения двух Docker сетей через общую сеть
# Позволяет контейнерам из разных сетей обращаться друг к другу по именам

# Имена сетей
$mainNetwork = "docker-networkmall2"
$authentikNetwork = "authentic_default"
$bridgeNetwork = "shared-bridge-network"

function Create-SharedNetwork {
    Write-Host "=== Создание общей сети для объединения контейнеров ===" -ForegroundColor Green
    
    # Проверяем, существует ли общая сеть
    $existingNetwork = docker network ls --filter "name=$bridgeNetwork" --format "{{.Name}}"
    
    if (-not $existingNetwork) {
        Write-Host "Создаем общую сеть '$bridgeNetwork'..." -ForegroundColor Yellow
        docker network create --driver bridge $bridgeNetwork
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Сеть '$bridgeNetwork' успешно создана." -ForegroundColor Green
        } else {
            Write-Error "Ошибка при создании сети '$bridgeNetwork'"
            return $false
        }
    } else {
        Write-Host "Сеть '$bridgeNetwork' уже существует." -ForegroundColor Blue
    }
    
    return $true
}

function Connect-ContainersToSharedNetwork {
    param(
        [string]$sourceNetwork,
        [string]$targetNetwork
    )
    
    Write-Host "Подключение контейнеров из сети '$sourceNetwork' к общей сети '$targetNetwork'..." -ForegroundColor Yellow
    
    # Получаем информацию о сети
    try {
        $networkInfo = docker network inspect $sourceNetwork | ConvertFrom-Json
        $containers = $networkInfo[0].Containers
        
        if (-not $containers -or $containers.Count -eq 0) {
            Write-Host "В сети '$sourceNetwork' нет контейнеров." -ForegroundColor Blue
            return
        }
        
        # Подключаем каждый контейнер к общей сети
        foreach ($containerInfo in $containers.PSObject.Properties.Value) {
            $containerName = $containerInfo.Name
            
            # Проверяем, не подключен ли уже контейнер к целевой сети
            try {
                $targetNetworkInfo = docker network inspect $targetNetwork | ConvertFrom-Json
                $isAlreadyConnected = $false
                
                if ($targetNetworkInfo[0].Containers) {
                    foreach ($existingContainer in $targetNetworkInfo[0].Containers.PSObject.Properties.Value) {
                        if ($existingContainer.Name -eq $containerName) {
                            $isAlreadyConnected = $true
                            break
                        }
                    }
                }
                
                if (-not $isAlreadyConnected) {
                    Write-Host "  Подключаем контейнер '$containerName' к сети '$targetNetwork'..." -ForegroundColor Cyan
                    docker network connect $targetNetwork $containerName
                    
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "    ✓ Контейнер '$containerName' успешно подключен." -ForegroundColor Green
                    } else {
                        Write-Host "    ✗ Ошибка подключения контейнера '$containerName'." -ForegroundColor Red
                    }
                } else {
                    Write-Host "  Контейнер '$containerName' уже подключен к сети '$targetNetwork'." -ForegroundColor Blue
                }
            } catch {
                Write-Host "  Ошибка при проверке подключения контейнера '$containerName': $_" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "Ошибка при получении информации о сети '$sourceNetwork': $_" -ForegroundColor Red
    }
}

function Show-NetworkConnections {
    Write-Host "`n=== Текущие подключения контейнеров ===" -ForegroundColor Green
    
    $networks = @($mainNetwork, $authentikNetwork, $bridgeNetwork)
    
    foreach ($network in $networks) {
        try {
            $networkInfo = docker network inspect $network 2>$null | ConvertFrom-Json
            if ($networkInfo) {
                $containers = $networkInfo[0].Containers
                Write-Host "`nСеть: $network" -ForegroundColor Yellow
                
                if ($containers -and $containers.Count -gt 0) {
                    foreach ($containerInfo in $containers.PSObject.Properties.Value) {
                        $name = $containerInfo.Name
                        $ip = $containerInfo.IPv4Address.Split('/')[0]
                        Write-Host "  - $name ($ip)" -ForegroundColor Cyan
                    }
                } else {
                    Write-Host "  (нет контейнеров)" -ForegroundColor Gray
                }
            }
        } catch {
            Write-Host "Сеть '$network' не найдена или недоступна." -ForegroundColor Gray
        }
    }
}

function Test-NetworkConnectivity {
    Write-Host "`n=== Тест сетевой связности ===" -ForegroundColor Green
    
    # Список контейнеров для тестирования
    $testContainers = @(
        "postgres-production-db",
        "web_logger-container", 
        "business-entity-container"
    )
    
    $authentikContainers = @(
        "authentic-server-1",
        "authentic-worker-1"
    )
    
    foreach ($container in $testContainers) {
        $containerExists = docker ps --filter "name=$container" --format "{{.Names}}"
        if ($containerExists) {
            Write-Host "`nТестирование связности из контейнера '$container':" -ForegroundColor Yellow
            
            # Тестируем подключение к Authentik контейнерам
            foreach ($target in $authentikContainers) {
                Write-Host "  Пинг $target..." -ForegroundColor Cyan
                docker exec $container ping -c 1 $target 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "    ✓ Доступен" -ForegroundColor Green
                } else {
                    Write-Host "    ✗ Недоступен" -ForegroundColor Red
                }
            }
            
            # Дополнительное тестирование для business-entity-container
            if ($container -eq "business-entity-container") {
                Write-Host "  Расширенное тестирование для business-entity-container:" -ForegroundColor Magenta
                
                # Тестируем подключение к другим контейнерам Mall2
                $mall2Containers = @("postgres-production-db", "web_logger-container")
                foreach ($target in $mall2Containers) {
                    if ($target -ne $container) {
                        Write-Host "    Пинг $target..." -ForegroundColor Cyan
                        docker exec $container ping -c 1 $target 2>&1 | Out-Null
                        if ($LASTEXITCODE -eq 0) {
                            Write-Host "      ✓ Доступен" -ForegroundColor Green
                        } else {
                            Write-Host "      ✗ Недоступен" -ForegroundColor Red
                        }
                    }
                }
                
                # Проверяем DNS-разрешение для Authentik контейнеров
                Write-Host "    Проверка DNS-разрешения:" -ForegroundColor Cyan
                foreach ($target in $authentikContainers) {
                    docker exec $container nslookup $target 2>&1 | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "      ✓ DNS для $target работает" -ForegroundColor Green
                    } else {
                        Write-Host "      ✗ DNS для $target не работает" -ForegroundColor Red
                    }
                }
                
                # Проверяем сетевые интерфейсы в контейнере
                Write-Host "    Сетевые интерфейсы в business-entity-container:" -ForegroundColor Cyan
                $interfaces = docker exec $container ip addr show 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $interfaces | ForEach-Object { 
                        if ($_ -match "inet (\d+\.\d+\.\d+\.\d+)") {
                            Write-Host "      IP: $($matches[1])" -ForegroundColor Green
                        }
                    }
                } else {
                    Write-Host "      ✗ Не удалось получить информацию о сетевых интерфейсах" -ForegroundColor Red
                }
            }
        }
    }
}

# Основная логика
function Main {
    Write-Host "=== Объединение Docker сетей Mall2 и Authentik ===" -ForegroundColor Magenta
    
    # Создаем общую сеть
    if (-not (Create-SharedNetwork)) {
        return
    }
    
    # Подключаем контейнеры из основной сети
    Connect-ContainersToSharedNetwork -sourceNetwork $mainNetwork -targetNetwork $bridgeNetwork
    
    # Подключаем контейнеры из сети Authentik
    Connect-ContainersToSharedNetwork -sourceNetwork $authentikNetwork -targetNetwork $bridgeNetwork
    
    # Показываем результат
    Show-NetworkConnections
    
    # Тестируем связность
    Test-NetworkConnectivity
    
    Write-Host "`n=== Готово! ===" -ForegroundColor Green
    Write-Host "Теперь контейнеры из разных сетей могут обращаться друг к другу по именам." -ForegroundColor Green
}

# Запуск
Main