#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SampleOnlineMall.AssortmentApi/SampleOnlineMall.AssortmentApi.csproj", "SampleOnlineMall.AssortmentApi/"]
COPY ["SampleOnlineMall.Core/SampleOnlineMall.Core.csproj", "SampleOnlineMall.Core/"]
COPY ["SampleOnlineMall.DataAccess/SampleOnlineMall.DataAccess.csproj", "SampleOnlineMall.DataAccess/"]
COPY ["SampleOnlineMall.Service/SampleOnlineMall.Service.csproj", "SampleOnlineMall.Service/"]
RUN dotnet restore "./SampleOnlineMall.AssortmentApi/SampleOnlineMall.AssortmentApi.csproj"
COPY . .
WORKDIR "/src/SampleOnlineMall.AssortmentApi"
RUN dotnet build "./SampleOnlineMall.AssortmentApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SampleOnlineMall.AssortmentApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SampleOnlineMall.AssortmentApi.dll"]