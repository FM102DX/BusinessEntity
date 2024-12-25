# Установите необходимые переменные
$projectPath = "C:\Develop\Mall2\SampleOnlineMall.AssortmentApi"
$buildContextPath="C:\Develop\Mall2"
$imageName = "assortmentapi"
$containerName = "assortmentapi-container"
$dockerfilePath = "$projectPath\Dockerfile"
$portExt = 5000
$portInt = 5000 

# Проверка наличия Docker
if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
    Write-Error "Docker не установлен. Установите Docker Desktop и убедитесь, что он настроен для работы с Hyper-V."
    exit 1
}

# Переход в каталог проекта
Write-Host "Переход в каталог проекта..."
Set-Location -Path $projectPath

# Сборка проекта
Write-Host "Сборка проекта..."
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Error "Ошибка сборки проекта. Проверьте код и повторите попытку."
    exit 1
}

# Создание Docker-образа
Write-Host "Создание Docker-образа..."
docker build -t $imageName -f $dockerfilePath $buildContextPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Ошибка создания Docker-образа. Проверьте Dockerfile и повторите попытку."
    exit 1
}

# Проверка, есть ли уже запущенный контейнер с этим именем
$existingContainer = docker ps -a --filter "name=$containerName" --format "{{.ID}}"

if ($existingContainer) {
    Write-Host "Остановка и удаление существующего контейнера..."
    docker stop $existingContainer
    docker rm $existingContainer
}

# Запуск контейнера
Write-Host "Запуск контейнера..."
docker run -e 'ASPNETCORE_URLS=http://*:80' -d --name $containerName -p "$($portExt):$($portInt)" $imageName

if ($LASTEXITCODE -ne 0) {
    Write-Error "Ошибка запуска контейнера. Проверьте параметры и повторите попытку."
    exit 1
}

# Вывод IP-адреса контейнера
Write-Host "Получение IP-адреса контейнера..."
$containerIP = docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $containerName
Write-Host "Контейнер запущен. IP-адрес контейнера: $containerIP"
Write-Host "Приложение доступно по адресу: http://$($containerIP):$($port)"

# Открытие в браузере Opera
$address = "http://localhost:$($portExt)"
Write-Host "Открытие в браузере Opera: $address"
Start-Process "opera" $address
