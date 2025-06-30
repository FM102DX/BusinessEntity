# Используем официальный образ PostgreSQL
FROM postgres:latest

# (Необязательно) Задаём переменные окружения по умолчанию.
# Здесь вы можете поменять имя БД, пользователя и пароль на свои.
ENV POSTGRES_DB=production_db_db
ENV POSTGRES_USER=postgres
ENV POSTGRES_PASSWORD=yourpassword

# Указываем, что контейнер слушает 5432 порт
EXPOSE 5432

# По умолчанию образ уже запускает PostgreSQL, так что ENTRYPOINT/CMD не меняем
