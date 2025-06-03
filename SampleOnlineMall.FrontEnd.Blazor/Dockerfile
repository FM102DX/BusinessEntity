FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["SampleOnlineMall.FrontEnd.Blazor/SampleOnlineMall.FrontEnd.Blazor.csproj", "SampleOnlineMall.FrontEnd.Blazor/"]
COPY ["SampleOnlineMall.Core/SampleOnlineMall.Core.csproj", "SampleOnlineMall.Core/"]

RUN dotnet restore "SampleOnlineMall.FrontEnd.Blazor/SampleOnlineMall.FrontEnd.Blazor.csproj"

# Copy source code
COPY . .

# Build and publish the Blazor WebAssembly app
WORKDIR "/src/SampleOnlineMall.FrontEnd.Blazor"
RUN dotnet publish "SampleOnlineMall.FrontEnd.Blazor.csproj" -c Release -o /app/publish

# Use nginx to serve the static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Copy the published Blazor WebAssembly app
COPY --from=build /app/publish/wwwroot .

# Copy custom nginx configuration if needed
COPY --from=build /src/SampleOnlineMall.FrontEnd.Blazor/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80