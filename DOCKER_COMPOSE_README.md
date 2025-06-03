# SampleOnlineMall Docker Compose Setup

This repository now contains a complete Docker Compose setup that allows you to run the entire SampleOnlineMall ecosystem with a single command.

## Services

The docker-compose setup includes the following services:

1. **PostgreSQL Database** (`postgres`) - Database server with two databases:
   - `assortment` - for the AssortmentApi service
   - `weblogger` - for the WebLogger service

2. **WebLogger Service** (`weblogger`) - Logging service
   - Port: 7000
   - URL: http://localhost:7000

3. **Assortment API Service** (`assortmentapi`) - Main API service
   - Port: 8000
   - URL: http://localhost:8000

4. **Blazor Frontend** (`frontend`) - Web frontend using nginx
   - Port: 3000
   - URL: http://localhost:3000

## Quick Start

To start the entire ecosystem:

```bash
# Start all services
docker compose up -d

# Check status
docker compose ps

# View logs
docker compose logs -f

# Stop all services
docker compose down

# Stop and remove volumes (WARNING: This will delete all data)
docker compose down -v
```

## Individual Service Management

You can also start services individually:

```bash
# Start only PostgreSQL
docker compose up postgres -d

# Start PostgreSQL and WebLogger
docker compose up postgres weblogger -d

# Start everything except frontend
docker compose up postgres weblogger assortmentapi -d
```

## Development

For development, you can rebuild services after code changes:

```bash
# Rebuild a specific service
docker compose build weblogger
docker compose build assortmentapi
docker compose build frontend

# Rebuild and restart
docker compose up --build weblogger -d
```

## Configuration

The services are configured to work together automatically:

- **Database connections**: Services connect to the PostgreSQL container using the hostname `postgres`
- **Service communication**: AssortmentApi connects to WebLogger using the hostname `weblogger`
- **Frontend configuration**: The Blazor frontend is configured to connect to the APIs at `localhost:7000` and `localhost:8000`

## Volumes

The setup creates persistent volumes for:

- `postgres_data`: PostgreSQL database files
- `weblogger_logs`: WebLogger application logs
- `assortmentapi_logs`: AssortmentApi application logs

## Network

All services communicate through a custom bridge network called `sampleonlinemall-network`.

## Ports

- 3000: Blazor Frontend
- 5432: PostgreSQL
- 7000: WebLogger
- 8000: AssortmentApi

## Migration from Individual Dockerfiles

The old individual Dockerfiles have been updated with correct project paths and are now used by the docker-compose setup. The previous deployment scripts in `SampleOnlineMall.PowershellManagement` are no longer needed for local development.