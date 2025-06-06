function Show-MultipleContainersNetworks {
    <#
    .SYNOPSIS
        Выводит только те Docker-сети, в которых есть хотя бы один контейнер.

    .DESCRIPTION
        Функция проверяет все существующие Docker-сети, инспектирует каждую из них и выводит в консоль
        таблицу с информацией о контейнерах (имя, IPv4-адрес, MAC-адрес) для тех сетей, где есть запущенные контейнеры.
        Если ни в одной сети нет контейнеров, функция выводит соответствующее сообщение.

    .EXAMPLE
        PS> Show-MultipleContainersNetworks
        ===Сети Докер, содержащие хотябы 1 контейнер===
        Сеть: docker-networkmall2 (Количество контейнеров: 3)
        ------------------------------------------------------------------------------------------------------
        Контейнер                                                    | IPv4-адрес         | MAC-адрес         
        -------------------------------------------------------------|--------------------|-------------------
        postgres-production-db                                        | 172.18.0.2         | 02:42:ac:12:00:02 
        web_logger-container                                          | 172.18.0.3         | 02:42:ac:12:00:03 
        business-entity-container                                     | 172.18.0.4         | 02:42:ac:12:00:04 
        ------------------------------------------------------------------------------------------------------

    .NOTES
        Скрипт должен запускаться в PowerShell 5.1 или выше (Windows PowerShell) либо в PowerShell 7+.
        Не требует передавать какие-либо параметры.
    #>

    Write-Host "=== Сети Докер, содержащие хотя бы 1 контейнер ===" -ForegroundColor Yellow

    # Получаем список всех имён Docker-сетей
    $networkList = docker network ls --format "{{.Name}}"

    # Коллекция для сетей, в которых есть контейнеры
    $matchingNetworks = @()

    foreach ($netName in $networkList) {
        # Информация по сети в формате JSON
        $netInspect = docker network inspect $netName | ConvertFrom-Json
        $netObject  = $netInspect[0]

        # Словарь Containers: ключ = ID контейнера, значение = объект с информацией
        $containers = $netObject.Containers
        $containerCount = $containers.Count

        # Если в сети есть хотя бы один контейнер, добавляем объект сети в коллекцию
        if ($containerCount -gt 0) {
            $matchingNetworks += $netObject
        }
    }

    # Если коллекция пустая, уведомляем пользователя
    if ($matchingNetworks.Count -eq 0) {
        Write-Host "Нет сетей, в которых есть запущенные Docker-контейнеры." -ForegroundColor Red
        Write-Host "******************"
        return
    }

    # Для каждой найденной сети выводим таблицу контейнеров
    foreach ($netObject in $matchingNetworks) {
        $netName = $netObject.Name
        $containers = $netObject.Containers

        # Преобразуем объекты контейнеров в простой массив
        $containerArray = @()
        foreach ($key in $containers.PSObject.Properties.Name) {
            $containerArray += $containers.$key
        }

        # Если контейнеров нет (на всякий случай), пропускаем
        if ($containerArray.Count -eq 0) {
            continue
        }

        # Заголовок таблицы
        Write-Host "`nСеть: $netName (Количество контейнеров: $($containerArray.Count))" -ForegroundColor Yellow
        Write-Host "------------------------------------------------------------------------------------------------------"
        Write-Host "Контейнер                                                    | IPv4-адрес         | MAC-адрес         "
        Write-Host "-------------------------------------------------------------|--------------------|-------------------"

        # Перебор контейнеров внутри сети
        foreach ($container in $containerArray) {
            if ($null -eq $container) {
                continue  # Пропускаем пустые записи
            }

            # Извлечение данных контейнера
            $name = $container.Name
            $ipv4 = $container.IPv4Address
            $macAddress = $container.MacAddress

            # Убираем маску из IPv4 (оставляем только адрес)
            if ($null -ne $ipv4) {
                $ipv4 = $ipv4.Split('/')[0]
            } else {
                $ipv4 = "Нет IP-адреса"
            }

            # Форматированный вывод строки
            $formattedLine = "{0,-60} | {1,-18} | {2,-17}" -f $name, $ipv4, $macAddress
            Write-Host -Object $formattedLine
        }

        Write-Host "------------------------------------------------------------------------------------------------------"
    }
}
