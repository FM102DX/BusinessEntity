# PostgreSQL Logger Database Container
FROM postgres:latest

# Set environment variables
ENV POSTGRES_DB=logger_db
ENV POSTGRES_USER=adm01
ENV POSTGRES_PASSWORD=adm01pws

# Expose port
EXPOSE 5432

# Initialize database with logging schema
COPY init-logger-db.sql /docker-entrypoint-initdb.d/