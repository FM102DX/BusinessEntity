# PostgreSQL Assortment Database Container
FROM postgres:latest

# Set environment variables
ENV POSTGRES_DB=mall
ENV POSTGRES_USER=adm01
ENV POSTGRES_PASSWORD=adm01pwd

# Expose port
EXPOSE 5432

# Initialize database with assortment schema
COPY init-assort-db.sql /docker-entrypoint-initdb.d/