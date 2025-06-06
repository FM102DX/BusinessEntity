# Управление Docker-контейнерами

. "$PSScriptRoot\DockerNetworkInfo.ps1"


# Список контейнеров
$containers = @(
    [PSCustomObject]@{
        Name = "admin-node"
        PortInt = 80
        PortExt = 5020
        ProjectPath = "C:\Develop\Mall2\ControlNode"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "assort-api-container"
        PortInt = 80
        PortExt = 5010
        ProjectPath = "C:\Develop\Mall2\SampleOnlineMall.AssortmentApi"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "business-entity-container"
        PortInt = 80
        PortExt = 7000
        ProjectPath = "C:\Develop\Mall2\BusinessEntity"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "web_logger-container"
        PortInt = 80
        PortExt = 5080
        ProjectPath = "C:\Develop\Mall2\BlazorServerWebLogger"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "postgres-production-db"
        PortInt = 5432
        PortExt = 5470
        DockerfilePath = "C:\Develop\Mall2\postgres_db\Dockerfile"
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
}

function BuildAndRunPostgresContainer {
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$container
    )

    # Извлекаем параметры из объекта
    $name           = $container.Name
    $portInt        = $container.PortInt
    $portExt        = $container.PortExt
    $dockerfilePath = $container.DockerfilePath
    $envDbName      = $container.EnvDbName
    $envDbUser      = $container.EnvDbUser
    $envDbPassword  = $container.EnvDbPassword
    $logPath        = $container.LogPath
    $networkName    = "docker-networkmall2"

    # Контекст сборки — папка, где лежит Dockerfile
    $contextPath = Split-Path $dockerfilePath -Parent

    # Убедимся, что директория для логов существует
    if (-not (Test-Path $logPath)) {
        New-Item -ItemType Directory -Path $logPath | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $buildLog  = Join-Path $logPath "$name-build-$timestamp.log"
    $runLog    = Join-Path $logPath "$name-run-$timestamp.log"

    try {
        Write-Host "[$timestamp] Сборка Docker-образа '$name' из Dockerfile '$dockerfilePath'..."
        & docker build `
            --file $dockerfilePath `
            --tag $name `
            $contextPath 2>&1 `
            | Tee-Object -FilePath $buildLog

        if ($LASTEXITCODE -ne 0) {
            throw "Ошибка при сборке образа. См. лог: $buildLog"
        }

        # Проверяем, есть ли уже контейнер с таким именем, и если есть — останавливаем и удаляем
        $existing = docker ps -a --filter "name=^/$name$" --format "{{.Names}}"
        if ($existing -eq $name) {
            Write-Host "Контейнер '$name' уже существует. Останавливаю и удаляю..."
            docker stop $name 2>&1 | Tee-Object -FilePath $runLog -Append
            docker rm   $name 2>&1 | Tee-Object -FilePath $runLog -Append
        }

        Write-Host "Запуск контейнера '$name' в сети '$networkName' с портом хоста $portExt на порт контейнера $portInt..."
        & docker run -d `
            --name $name `
            --network $networkName `
            -e "POSTGRES_DB=$envDbName" `
            -e "POSTGRES_USER=$envDbUser" `
            -e "POSTGRES_PASSWORD=$envDbPassword" `
            -p "$portExt`:$portInt" `
            $name 2>&1 `
            | Tee-Object -FilePath $runLog

        if ($LASTEXITCODE -ne 0) {
            throw "Ошибка при запуске контейнера. См. лог: $runLog"
        }

        Write-Host "Контейнер '$name' успешно запущен в сети '$networkName'."
        Write-Host "Логи сохранены в:"
        Write-Host "  Build: $buildLog"
        Write-Host "  Run:   $runLog"
    }
    catch {
        Write-Error $_
    }
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
    docker run -e 'ASPNETCORE_URLS=http://*:80' --network docker-networkmall2 -e 'ASPNETCORE_ENVIRONMENT=Development' -d --name $container.Name -p "$($portExt):$($portInt)" $imageName

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка запуска контейнера. Проверьте параметры и повторите попытку."
        return
    }

    Write-Host "Получение IP-адреса контейнера..."
    $containerIP = docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $container.Name
    Write-Host "Контейнер запущен. IP-адрес контейнера: $containerIP"
}

function BuildAndRunPostgresAssortDb {
    Write-Host "Сборка и запуск PostgreSQL Assort DB..." -ForegroundColor Cyan
    $container = GetContainerByName -containerName 'postgres-production-db'
    if ($null -eq $container) {
        Write-Host "Контейнер postgres-assort-db не найден" -ForegroundColor Red
        return
    }
    BuildAndRunPostgresContainer -container $container
}

function Action37 {
    Write-Host "Запуск сервиса Authentik..." -ForegroundColor Cyan
    $authPath = "C:\Develop\Mall2\Authentic"
    if (-not (Test-Path $authPath)) {
        Write-Error "Путь '$authPath' не найден."
        return
    }

    Push-Location $authPath
    try {
        Write-Host "Выполняется 'docker-compose pull' и 'docker-compose up -d' в '$authPath'..."
        docker-compose pull
        docker-compose up -d

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Не удалось запустить Authentik. Проверьте docker-compose.yml."
        } else {
            Write-Host "Сервис Authentik успешно запущен." -ForegroundColor Green
        }
    }
    catch {
        Write-Error $_
    }
    finally {
        Pop-Location
    }
}

function Action371 {
    Write-Host "Генерация файла .env для Authentik..." -ForegroundColor Cyan

    $authPath = "C:\Develop\Mall2\Authentic"
    if (-not (Test-Path $authPath)) {
        Write-Error "Путь '$authPath' не найден."
        return
    }

    Push-Location $authPath
    try {
        # Генерируем 36 случайных байт и кодируем Base64 для PG_PASS
        $bytesPG = New-Object byte[] 36
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytesPG)
        $PG_PASS = [Convert]::ToBase64String($bytesPG)

        # Генерируем 60 случайных байт и кодируем Base64 для AUTHENTIK_SECRET_KEY
        $bytesAK = New-Object byte[] 60
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytesAK)
        $AUTHENTIK_SECRET_KEY = [Convert]::ToBase64String($bytesAK)

        # Записываем в .env (перезаписываем, если уже есть)
        "PG_PASS=$PG_PASS" | Out-File -Encoding utf8 .env
        "AUTHENTIK_SECRET_KEY=$AUTHENTIK_SECRET_KEY" | Add-Content -Encoding utf8 .env

        Write-Host "Файл .env успешно создан и сохранён в '$authPath'." -ForegroundColor Green
        Write-Host "Содержимое .env:"
        Get-Content .env | ForEach-Object { Write-Host "  $_" }
    }
    catch {
        Write-Error $_
    }
    finally {
        Pop-Location
    }
}

function Show-Menu {
    Write-Host "Меню:" -ForegroundColor Green
    Write-Host "00   -- Вывести статус докер-сетей"
    Write-Host "10   -- Подключиться к выбранному контейнеру"
    Write-Host "20   -- Вывести диагностическую информацию для выбранного контейнера"
    Write-Host "30   -- Сбилдить и запустить контейнер"
    Write-Host "35   -- Сбилдить и запустить production_db контейнер"
    Write-Host "37   -- Запустить сервис Authentik"
    Write-Host "371  -- Сгенерировать .env для Authentik"
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
        "10"  { Action10 }
        "20"  { Action20 }
        "30"  { SelectContainerAndRun }
        "35"  { BuildAndRunPostgresAssortDb }
        "37"  { Action37 }
        "371" { Action371 }
        "40"  { BuildAndRunBusinessLogicContainers }
        "80"  { Clear-Host }
        "99"  { break }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
        }
    }
} while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green
