FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["SampleOnlineMall.AssortmentApi/SampleOnlineMall.AssortmentApi.csproj", "SampleOnlineMall.AssortmentApi/"]
COPY ["SampleOnlineMall.Core/SampleOnlineMall.Core.csproj", "SampleOnlineMall.Core/"]
COPY ["SampleOnlineMall.DataAccess/SampleOnlineMall.DataAccess.csproj", "SampleOnlineMall.DataAccess/"]
COPY ["SampleOnlineMall.Service/SampleOnlineMall.Service.csproj", "SampleOnlineMall.Service/"]

RUN dotnet restore "SampleOnlineMall.AssortmentApi/SampleOnlineMall.AssortmentApi.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/SampleOnlineMall.AssortmentApi"
RUN dotnet build "SampleOnlineMall.AssortmentApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SampleOnlineMall.AssortmentApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SampleOnlineMall.AssortmentApi.dll"]