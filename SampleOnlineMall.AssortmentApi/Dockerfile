#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.



# Используем базовый образ для ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Используем базовый образ для SDK
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем файлы проектов
COPY ["SampleOnlineMall.AssortmentApi/SampleOnlineMall.AssortmentApi.csproj", "SampleOnlineMall.AssortmentApi/"]
COPY ["SampleOnlineMall.Core/SampleOnlineMall.Core.csproj", "SampleOnlineMall.Core/"]
COPY ["SampleOnlineMall.DataAccess/SampleOnlineMall.DataAccess.csproj", "SampleOnlineMall.DataAccess/"]
COPY ["SampleOnlineMall.Service/SampleOnlineMall.Service.csproj", "SampleOnlineMall.Service/"]

# Восстанавливаем зависимости
RUN dotnet restore ./SampleOnlineMall.AssortmentApi/SampleOnlineMall.AssortmentApi.csproj

# Копируем оставшиеся файлы
COPY . .
WORKDIR /src/SampleOnlineMall.AssortmentApi

# Сборка приложения
RUN dotnet build ./SampleOnlineMall.AssortmentApi.csproj -c $BUILD_CONFIGURATION -o /app/build

# Публикация приложения
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish ./SampleOnlineMall.AssortmentApi.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Финальный образ
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Устанавливаем переменную окружения для маркера Docker
ENV IS_DOCKER=true

# Указываем точку входа
ENTRYPOINT ["dotnet", "SampleOnlineMall.AssortmentApi.dll"]