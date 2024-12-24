
# Проверяем, существует ли сеть с именем docker-networkmall2
$networkName = "docker-networkmall2"
$existingNetwork = docker network ls --filter "name=$networkName" --format "{{.Name}}"

if (-not $existingNetwork) {
    # Если сети не существует, создаем ее
    Write-Output "Сеть '$networkName' не существует. Создаем..."
    docker network create $networkName
} else {
    Write-Output "Сеть '$networkName' уже существует."
}

# Получаем список всех запущенных контейнеров
$runningContainers = docker ps --format "{{.ID}}"

if (-not $runningContainers) {
    Write-Output "Нет запущенных контейнеров."
    return
}

# Добавляем каждый запущенный контейнер в сеть
foreach ($container in $runningContainers) {
    # Проверяем, подключен ли контейнер к сети
    $isConnected = docker network inspect $networkName --format "{{range .Containers}}{{.Name}}{{\n}}{{end}}" | Select-String $container

    if (-not $isConnected) {
        Write-Output "Добавляем контейнер $container в сеть $networkName..."
        docker network connect $networkName $container
    } else {
        Write-Output "Контейнер $container уже подключен к сети $networkName."
    }
}
