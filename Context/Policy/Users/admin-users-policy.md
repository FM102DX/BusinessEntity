# Политика админских пользователей

## 1. Принцип

`Authentik` остается источником identity, паролей и внешних групп.

`UserMiniApp` хранит только локальную материализацию пользователя и application authorization:

- локальные `Users`;
- локальные группы;
- роли;
- назначения ролей.

Пароли, password hash, tokens и client secrets в локальную БД не записываются.

## 2. Стартовые администраторы

При каждом старте приложения `UserMiniApp` выполняет идемпотентный bootstrap:

- проверяет/создает Authentik-пользователя `akadmin`;
- задает `akadmin` пароль `akadmin`, если этот пароль не проходит через Authentik password-flow;
- материализует `akadmin` в локальной таблице `Users`;
- проверяет/создает Authentik-пользователя `admin`;
- задает `admin` пароль `admin`, если этот пароль не проходит через Authentik password-flow;
- создает Authentik-группу `BusinessEntityAdmins`;
- добавляет `admin` в `BusinessEntityAdmins`;
- материализует `admin` в локальной таблице `Users`;
- создает локальную группу `BusinessEntityAdmins`;
- добавляет локального пользователя `admin` в эту группу;
- назначает локальной группе `BusinessEntityAdmins` системную роль `Админ` на `[ВсеПространства]`.

## 3. Маркеры доступа

`akadmin` является техническим emergency-admin и определяется по username.

`admin` получает общий административный runtime-признак через Authentik-группу `BusinessEntityAdmins`.

Контентные/application права `admin` задаются локально через:

```text
Group BusinessEntityAdmins -> Role Админ -> [ВсеПространства]
```

Локальная таблица `Users` не является источником authentication и сама по себе не дает прав.

## 4. Эксплуатационные правила

Bootstrap должен быть безопасен для повторного запуска.

Если Authentik Admin API token не настроен, bootstrap не меняет пользователей и пишет предупреждение в лог.

Имена групп можно переопределять конфигурацией `AuthentikAuth`, но смысл группы `BusinessEntityAdmins` должен оставаться единым: это маркер общего админского доступа приложения.
