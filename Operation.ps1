
# Получение пути текущего скрипта
$scriptDirectory = $PSScriptRoot



# Определяем обработчики для каждого пункта меню
function Action1 {
        $targetScriptPath = Join-Path -Path $scriptDirectory -ChildPath "Powershell\BuildAssortAndOpenInDocker.ps1"

        # Запуск целевого скрипта
        Write-Host "Запуск скрипта: $targetScriptPath"
        & $targetScriptPath

        # Проверка результата выполнения
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Ошибка при выполнении скрипта: $targetScriptPath"
            exit $LASTEXITCODE
        }
}


function Action2 {
        $targetScriptPath = Join-Path -Path $scriptDirectory -ChildPath "Powershell\DockerNetworkInfo.ps1"
   
        # Запуск целевого скрипта
        Write-Host "Запуск скрипта: $targetScriptPath"
        & $targetScriptPath

        # Проверка результата выполнения
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Ошибка при выполнении скрипта: $targetScriptPath"
            exit $LASTEXITCODE
        }
}


function Action3 {
    $targetScriptPath = Join-Path -Path $scriptDirectory -ChildPath "Powershell\DockerNetworkInfo.ps1"
   
    # Запуск целевого скрипта
    Write-Host "Запуск скрипта: $targetScriptPath"
    & $targetScriptPath

    # Проверка результата выполнения
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка при выполнении скрипта: $targetScriptPath"
        exit $LASTEXITCODE
    }
}

function ClearScreen {
    Clear-Host
}

# Функция отображения меню
function Show-Menu {
    Write-Host "Меню:"
    Write-Host "1 -- Запстить ассортимент в докере"
    Write-Host "2 -- Показать состав докер сети"
    Write-Host "3 -- Добавить все запущенные контейнры в существующую докер сеть"
    Write-Host "80 -- Очистить экран (CLS)"
    Write-Host "99 -- Выход"
}

# Основной цикл работы
do {
    Show-Menu
    $choice = Read-Host "Выберите пункт меню"

    switch ($choice) {
        "1" { Action1 }
        "2" { Action2 }
        "3" { Action3 }
        "80" { ClearScreen }
        "99" { Write-Host "Выход из программы..." -ForegroundColor Red }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
            continue  # Возвращаемся к началу цикла без ожидания ввода
        }
    }

} while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green
