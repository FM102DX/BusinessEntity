# Получение пути текущего скрипта
$scriptDirectory = $PSScriptRoot

# Функция для выполнения внешнего скрипта
function ExecExternalScript {
    param (
        [string]$scriptPath
    )

    $targetScriptPath = Join-Path -Path $scriptDirectory -ChildPath $scriptPath

    # Запуск целевого скрипта
    Write-Host "Запуск скрипта: $targetScriptPath"
    & $targetScriptPath

    # Проверка результата выполнения
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка при выполнении скрипта: $targetScriptPath"
        exit $LASTEXITCODE
    }
}

# Определяем обработчики для каждого пункта меню
function Action1 {
    ExecExternalScript -scriptPath "Powershell\BuildAssortAndOpenInDocker.ps1"
}

function Action2 {
    ExecExternalScript -scriptPath "Powershell\DockerNetworkInfo.ps1"
}

function Action3 {
    ExecExternalScript -scriptPath "Powershell\AddContainersToNetwork.ps1"
}

function Action31 {
    ExecExternalScript -scriptPath "Powershell\StartDockerLoggingDbContainer5440.ps1"
}

function Action4 {
            # Определяем путь к каталогу для сохранения логов на хосте
            $destinationPath = "C:\debuglogs"

            # Очищаем каталог на хосте, если он существует
            if (Test-Path $destinationPath) {
                Remove-Item -Recurse -Force $destinationPath
            }

            # Создаем каталог заново
            New-Item -ItemType Directory -Force -Path $destinationPath

            # Имя контейнера
            $containerName = "SampleOnlineMall.AssortmentApi_2"

            # Путь к логам внутри контейнера
            $containerLogsPath = "/app/bin/debug/net6.0/Logs"

            # Копируем содержимое из контейнера на хост
            Write-Host "Копирование логов из контейнера $containerName..."
            $str1 = "$($containerName):$containerLogsPath/."; $str2= "$destinationPath"
            $str1
            docker cp $str1 $str2

            Write-Host "Логи успешно скопированы в $destinationPath"

            # Открытие папки в проводнике Windows
            Start-Process explorer.exe $destinationPath
}

# bin\Debug\net6.0\Logs
# c:\debuglogs\*.*
# --volume c:/debuglogs:/path/in/container
# docker cp <container_name_or_id>:/path/to/log/file /local/path
#docker cp SampleOnlineMall.AssortmentApi_2:/path/to/log/file /local/path

# docker exec -it SampleOnlineMall.AssortmentApi_2 bash
# apt-get update && apt-get install -y mc

# docker exec -it SampleOnlineMall.AssortmentApi_2 sh

function Action5 {
    Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", "docker exec -it SampleOnlineMall.AssortmentApi_2 sh"
}

function Action51 {
    Start-Process  -FilePath "powershell" -ArgumentList "-NoExit", "-Command", "docker exec -it assortmentapi-container sh"
}

#вывести список процессов, запущенных в конейнере assortmentapi-container
function Action52 {

# Проверка наличия Docker на хосте
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker не установлен на хосте. Установите Docker и попробуйте снова."
    exit 1
}

# Проверка, что контейнер существует
$containerName = "assortmentapi-container"
$container = docker ps -q --filter "name=$containerName"
if (-not $container) {
    Write-Error "Контейнер $containerName не запущен. Запустите контейнер и попробуйте снова."
    exit 1
}

# Запуск команды в новом окне
Start-Process powershell -ArgumentList "-NoExit", "-Command", "docker exec -it $containerName /bin/bash -c 'apt-get update && apt-get install -y procps curl net-tools mc  && echo   && ps aux  | grep -v /bin/bash && echo   && (curl -s http://localhost:80 || echo CURLfailed) && echo  && netstat -tuln'"

}
function Action41 {
                # Определяем путь к каталогу для сохранения логов на хосте
                $destinationPath = "C:\debuglogs"

                # Очищаем каталог на хосте, если он существует
                if (Test-Path $destinationPath) {
                    Remove-Item -Recurse -Force $destinationPath
                }
    
                # Создаем каталог заново
                New-Item -ItemType Directory -Force -Path $destinationPath
    
                # Имя контейнера
                $containerName = "assortmentapi-container"
    
                # Путь к логам внутри контейнера
                $containerLogsPath = "/app/Logs"
    
                # Копируем содержимое из контейнера на хост
                Write-Host "Копирование логов из контейнера $containerName..."
                $str1 = "$($containerName):$containerLogsPath/."; $str2= "$destinationPath"
                $str1
                docker cp $str1 $str2
    
                Write-Host "Логи успешно скопированы в $destinationPath"
    
                # Открытие папки в проводнике Windows
                Start-Process explorer.exe $destinationPath
}



function ClearScreen {
    Clear-Host
}

# Функция отображения меню
function Show-Menu {
    Write-Host "Меню:"
    Write-Host "1  -- Запустить ассортимент в докере"
    Write-Host "2  -- Показать состав докер сети"
    Write-Host "3  -- Добавить все запущенные контейнеры в сеть docker-networkmall2"
    Write-Host "31 -- Поднять базу логгинга на порту 5440:5440"
    Write-Host "4  -- Скопировать логи ассортиментного контроллера кот из VS2022"
    Write-Host "41 -- Скопировать логи ассортиментного контроллера кот из под Docker"
    Write-Host "5  -- Подкл к ассорт контейнеру"
    Write-Host "51 -- Подкл к ассорт контейнеру from Docker"
    Write-Host "52 -- Диагностика ассорт контейнера from Docker"
    Write-Host "80 -- Очистить экран (CLS)"
    Write-Host "99 -- Выход"
}

# Основной цикл работы
do {
    Show-Menu
    $choice = Read-Host "Выберите пункт меню"

    switch ($choice) {
        "1"  { Action1 }
        "2"  { Action2 }
        "3"  { Action3 }
        "31"  { Action31 }
        "4"  { Action4 }
        "41" { Action41 }
        "5"  { Action5 }
        "51" { Action51 }
        "52" { Action52 }

        "80" { ClearScreen }
        "99" { Write-Host "Выход из программы..." -ForegroundColor Red }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
            continue  # Возвращаемся к началу цикла без ожидания ввода
        }
    }

} while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green
