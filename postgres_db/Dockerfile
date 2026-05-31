# Базовый образ PostgreSQL
FROM postgres:17-alpine

# Аргументы для версии и окружения (можно переопределить при сборке)
ARG POSTGRES_DB=main
ARG POSTGRES_USER=adm01
ARG POSTGRES_PASSWORD=adm01pwd

# Задаём переменные окружения для контейнера
ENV POSTGRES_DB=${POSTGRES_DB} \
    POSTGRES_USER=${POSTGRES_USER} \
    POSTGRES_PASSWORD=${POSTGRES_PASSWORD}

# Копируем скрипты инициализации, если они есть
# Все .sql и .sh из папки initdb будут выполнены при первом старте
COPY initdb/ /docker-entrypoint-initdb.d/

# Указываем, что каталог с данными будет храниться во внешнем томе
VOLUME ["/var/lib/postgresql/data"]

# Прокидываем порт
EXPOSE 5432

# Точка входа: стандартный entrypoint от образа postgres
# CMD можно не указывать, так как он унаследован, но можно переопределить:
CMD ["postgres"]
