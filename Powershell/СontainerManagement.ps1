# Управление Docker-контейнерами

. "$PSScriptRoot\DockerNetworkInfo.ps1"
# . "$PSScriptRoot\AutoConnectContainers.ps1"  # Удалено, больше не требуется

# Список контейнеров
$containers = @(
    [PSCustomObject]@{
        Name = "admin-node"
        PortInt = 80
        PortExt = 5020
        ProjectPath = "C:\Develop\BusinessEntity\ControlNode"
        ContextPath = "C:\Develop\BusinessEntity"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "business-entity-container"
        PortInt = 80
        PortExt = 7000
        ProjectPath = "C:\Develop\BusinessEntity\BusinessEntity"
        ContextPath = "C:\Develop\BusinessEntity"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "web_logger-container"
        PortInt = 80
        PortExt = 5080
        ProjectPath = "C:\Develop\BusinessEntity\BlazorServerWebLogger"
        ContextPath = "C:\Develop\BusinessEntity"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "postgres-production-db"
        PortInt = 5432
        PortExt = 5470
        DockerfilePath = "C:\Develop\BusinessEntity\postgres_db\Dockerfile"
        EnvDbName = "main"
        EnvDbUser = "adm01"
        EnvDbPassword = "adm01pwd"
        LogPath = "C:\Develop\Logs\PostgresAssort"
    }
)

function Select-Container {
    Write-Host "Выберите контейнер:" -ForegroundColor Cyan
    $i = 1
    foreach ($container in $containers) {
        Write-Host "$i. $($container.Name) (Port: $($container.PortExt))"
        $i++
    }
    $choice = Read-Host "Введите номер контейнера"
    return $containers[$choice - 1]
}

function GetContainerByName {
    param (
        [string]$containerName
    )

    # Поиск контейнера по имени
    $container = $containers | Where-Object { $_.Name -eq $containerName }

    if ($null -eq $container) {
        Write-Host "Контейнер с именем '$containerName' не найден." -ForegroundColor Red
        return $null
    }

    Write-Host "Контейнер '$containerName' найден." -ForegroundColor Green
    return $container
}

function Action10 {
    $container = Select-Container
    if ($null -eq $container) {
        Write-Host "Неверный выбор" -ForegroundColor Red
        return
    }
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "docker exec -it $($container.Name) sh"
}

function Action20 {
    $container = Select-Container
    if ($null -eq $container) {
        Write-Host "Неверный выбор" -ForegroundColor Red
        return
    }
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "docker exec -it $($container.Name) /bin/bash -c 'apt-get update && apt-get install -y procps curl net-tools mc && echo && ps aux | grep -v /bin/bash && echo && (curl -s http://localhost:80 || echo CURLfailed) && echo && netstat -tuln'"
}

function BuildAndRunBusinessLogicContainers {
    
    BuildAndRunContainer -container (GetContainerByName -containerName 'postgres-production-db')
    BuildAndRunContainer -container (GetContainerByName -containerName 'admin-node')
    BuildAndRunContainer -container (GetContainerByName -containerName 'assort-api-container')
    BuildAndRunContainer -container (GetContainerByName -containerName 'web_logger-container')
}

function SelectContainerAndRun {
    $container = Select-Container
    if ($null -eq $container) {
        Write-Host "Неверный выбор контейнера" -ForegroundColor Red
        return $null
    }
    BuildAndRunContainer -container $container
    AjustNetworks   # Подключаем все контейнеры к общей сети после запуска нового
}
function BuildAndRunPostgresContainer {
    param(
        [string]$Network    = 'docker-business-entity-common-bridge',
        [string]$DataVolume = 'pgdata',
        [string]$ImageTag   = 'latest'
    )

    # найдём наш контейнер
    $c = $containers | Where-Object Name -eq 'postgres-production-db'
    if (-not $c) {
        Write-Error "Конфиг для 'postgres-production-db' не найден."
        return
    }

    # удаляем старый контейнер (том останется нетронутым)
    docker rm -f $c.Name --volumes | Out-Null

    # проверяем, что Dockerfile действительно есть
    if (-not (Test-Path $c.DockerfilePath)) {
        Write-Error "Dockerfile не найден по пути: $($c.DockerfilePath)"
        return
    }

    # контекст сборки — папка, где лежит Dockerfile
    $ctx = Split-Path -Parent $c.DockerfilePath

    # собираем образ
    docker build `
        -t "$($c.Name):$ImageTag" `
        -f $c.DockerfilePath `
        $ctx

    # убеждаемся, что папка для логов есть
    if (-not (Test-Path $c.LogPath)) {
        New-Item -ItemType Directory -Path $c.LogPath -Force | Out-Null
    }

    # запускаем контейнер
    docker run -d `
        --name      $c.Name `
        --network   $Network `
        -p          "$($c.PortExt):$($c.PortInt)" `
        -e          "POSTGRES_DB=$($c.EnvDbName)" `
        -e          "POSTGRES_USER=$($c.EnvDbUser)" `
        -e          "POSTGRES_PASSWORD=$($c.EnvDbPassword)" `
        -v          "$($DataVolume):/var/lib/postgresql/data" `
        -v          "$($c.LogPath):/var/log/postgresql" `
        "$($c.Name):$ImageTag"
}
function BuildAndRunContainer {
    param (
        [PSCustomObject]$container
    )

    # Подготовка основных путей и переменных
    $projectPath    = $container.ProjectPath
    $contextPath    = $container.ContextPath
    $imageName      = ("{0}_image" -f $container.Name).ToLower()
    $dockerfilePath = Join-Path $projectPath 'Dockerfile'
    $portExt        = $container.PortExt
    $portInt        = $container.PortInt

    if (-not (Test-Path $contextPath)) {
        Write-Error "Контекст сборки не найден: $contextPath"
        return
    }

    # Проверка и удаление существующего контейнера, если он есть
    Write-Host "Проверка запущенного контейнера с именем $($container.Name)..."
    $existingContainer = docker ps -a -q --filter "name=$($container.Name)"

    if ($existingContainer) {
        Write-Host "Остановка существующего контейнера $($container.Name)..."
        docker stop $existingContainer

        Write-Host "Удаление существующего контейнера $($container.Name)..."
        docker rm $existingContainer

        # Проверка, что контейнер действительно удалён
        $checkContainer = docker ps -a -q --filter "name=$($container.Name)"
        if ($checkContainer) {
            Write-Host "Ошибка: не удалось удалить контейнер с именем $($container.Name)!" -ForegroundColor Red
            throw "Не удалось удалить контейнер. Скрипт завершён."
        } else {
            Write-Host "Контейнер $($container.Name) успешно удалён." -ForegroundColor Green
        }
    } else {
        Write-Host "Контейнер с именем $($container.Name) не найден."
    }

    Write-Host "Переход в каталог проекта..."
    Set-Location -Path $projectPath

    Write-Host "Сборка проекта..."
    dotnet build

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка сборки проекта. Проверьте код и повторите попытку."
        return
    }

    Write-Host "Создание Docker-образа..."
    docker build -t $imageName -f $dockerfilePath $contextPath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка создания Docker-образа. Проверьте Dockerfile и повторите попытку."
        return
    }

    Write-Host "Запуск нового контейнера..."
    docker run -e 'ASPNETCORE_URLS=http://*:80' --network docker-business-entity-common-bridge -e 'ASPNETCORE_ENVIRONMENT=Development' -e 'IS_DOCKER=true' -d --name $container.Name -p "$(($portExt)):$(($portInt))" $imageName

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка запуска контейнера. Проверьте параметры и повторите попытку."
        return
    }

    # Автоматически подключаем контейнер к общей сети
    Connect-ContainerToSharedNetwork -containerName $container.Name

    Write-Host "Получение IP-адреса контейнера..."
    $containerIP = docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $container.Name
    Write-Host "Контейнер запущен. IP-адрес контейнера: $containerIP"
}

# Функция для автоматического подключения контейнера к общей сети
function Connect-ContainerToSharedNetwork {
    param(
        [string]$containerName
    )
    
    $bridgeNetwork = "docker-business-entity-common-bridge"  # обновлённое имя сети
    
    # Проверяем, существует ли общая сеть
    $existingNetwork = docker network ls --filter "name=$bridgeNetwork" --format "{{.Name}}"
    
    if ($existingNetwork) {
        # Проверяем, не подключен ли уже контейнер
        $networkInfo = docker network inspect $bridgeNetwork | ConvertFrom-Json
        $isConnected = $false
        
        if ($networkInfo[0].Containers) {
            foreach ($container in $networkInfo[0].Containers.PSObject.Properties.Value) {
                if ($container.Name -eq $containerName) {
                    $isConnected = $true
                    break
                }
            }
        }
        
        if (-not $isConnected) {
            Write-Host "Подключаем контейнер '$containerName' к общей сети '$bridgeNetwork'..." -ForegroundColor Yellow
            docker network connect $bridgeNetwork $containerName
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Контейнер подключен к общей сети." -ForegroundColor Green
            } else {
                Write-Host "⚠ Не удалось подключить к общей сети." -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "⚠ Общая сеть не найдена. Запустите функцию AjustNetworks (пункт меню 03) для её создания." -ForegroundColor Yellow
    }
}

# --- Новая функция для создания/настройки общей сети и подключения всех контейнеров ---
function AjustNetworks {
    param(
        [string]$networkName = "docker-business-entity-common-bridge"
    )

    Write-Host "=== Настройка общей сети '$networkName' ===" -ForegroundColor Cyan

    # Создать сеть, если она отсутствует
    $existingNetwork = docker network ls --filter "name=$networkName" --format "{{.Name}}"
    if (-not $existingNetwork) {
        Write-Host "Создаём сеть '$networkName'..." -ForegroundColor Yellow
        docker network create --driver bridge $networkName | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Сеть '$networkName' создана." -ForegroundColor Green
        } else {
            Write-Error "Ошибка создания сети '$networkName'"
            return
        }
    } else {
        Write-Host "Сеть '$networkName' уже существует." -ForegroundColor Green
    }

    # Подключить все существующие контейнеры к сети
    $containers = docker ps -q
    foreach ($containerId in $containers) {
        $networksJson = docker inspect --format "{{json .NetworkSettings.Networks}}" $containerId | ConvertFrom-Json
        if ($networksJson.PSObject.Properties.Name -notcontains $networkName) {
            Write-Host "Подключаю контейнер $containerId к сети '$networkName'..." -ForegroundColor Yellow
            docker network connect $networkName $containerId | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ $containerId подключен." -ForegroundColor Green
            } else {
                Write-Host "⚠ Не удалось подключить $containerId." -ForegroundColor Red
            }
        }
    }

    Write-Host "Настройка сети завершена." -ForegroundColor Cyan
}

# Новая функция для объединения сетей
function Action01 {
    Write-Host "Объединение Docker сетей..." -ForegroundColor Cyan
    $scriptPath = Join-Path $PSScriptRoot "ConnectDockerNetworks.ps1"
    if (Test-Path $scriptPath) {
        & $scriptPath
    } else {
        Write-Host "Скрипт ConnectDockerNetworks.ps1 не найден." -ForegroundColor Red
    }
}

# Новая функция для автоподключения контейнеров
function Action02 {
    Write-Host "Автоподключение контейнеров к общей сети..." -ForegroundColor Cyan
    Auto-ConnectNewContainers
}

# Новая функция для очистки кеша Docker
function Action378 {
    Write-Host "Очистка кеша Docker..." -ForegroundColor Cyan
    Write-Host "Эта операция удалит все неиспользуемые образы, контейнеры, сети и кеш сборки." -ForegroundColor Yellow
    $confirmation = Read-Host "Вы уверены, что хотите продолжить? (y/N)"
    
    if ($confirmation -eq 'y' -or $confirmation -eq 'Y') {
        Write-Host "Выполняется docker system prune -a..." -ForegroundColor Yellow
        docker system prune -a
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Кеш Docker успешно очищен." -ForegroundColor Green
        } else {
            Write-Host "⚠ Произошла ошибка при очистке кеша Docker." -ForegroundColor Red
        }
    } else {
        Write-Host "Операция отменена." -ForegroundColor Yellow
    }
}

function Show-Menu {
    Write-Host "Меню:" -ForegroundColor Green
    Write-Host "00   -- Вывести статус докер-сетей"
    Write-Host "03   -- Настроить общую сеть (AjustNetworks)"
    Write-Host "10   -- Подключиться к выбранному контейнеру"
    Write-Host "20   -- Вывести диагностическую информацию для выбранного контейнера"
    Write-Host "30   -- Сбилдить и запустить контейнер"
    Write-Host "35   -- Сбилдить и запустить production_db контейнер"
    Write-Host "37   -- Запустить сервис Authentik"
    Write-Host "371  -- Сгенерировать .env для Authentik"
    Write-Host "378  -- Очистить кеш докера"
    Write-Host "38   -- Запушить ветку basic_add_elements_and_tree_mechanism как есть"
    Write-Host "40   -- Сбилдить и запустить бизнес-логику"
    Write-Host "80   -- Очистить экран (CLS)"
    Write-Host "99   -- Выход"
}

# Основной цикл работы
do {
    Show-Menu
    $choice = Read-Host "Выберите пункт меню"

    switch ($choice) {
        "00"  { Show-MultipleContainersNetworks }
        "03"  { AjustNetworks }
        "10"  { Action10 }
        "20"  { Action20 }
        "30"  { SelectContainerAndRun }
        "35"  { BuildAndRunPostgresContainer }
        "37"  { Action37 }
        "371" { Action371 }
        "378" { Action378 }
        "38"  { git push --force-with-lease origin basic_add_elements_and_tree_mechanism }
        "40"  { BuildAndRunBusinessLogicContainers }
        "80"  { Clear-Host }
        "99"  { break }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
        }
    }
} while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green
