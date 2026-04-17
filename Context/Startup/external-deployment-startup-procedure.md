# External Deployment Startup Procedure

## Назначение

Этот текст описывает, что нужно сделать при первом разворачивании `BusinessEntity` на внешнем хостинге.

## 1. Поднять инфраструктуру

Нужно развернуть и проверить:
- PostgreSQL для `BusinessEntity`
- PostgreSQL для `Authentik`
- `Authentik server`
- `Authentik worker`
- `BusinessEntity`
- при необходимости `WebLogger`

Нужно заранее подготовить:
- доменное имя приложения
- доменное имя Authentik
- TLS/HTTPS
- секреты и пароли

## 2. Настроить BusinessEntity

Нужно задать:
- `AUTHENTIK_BASE_URL`
- `AUTHENTIK_BASE_URL_FOR_BROWSER`
- `ClientId`
- `ClientSecret`
- `RedirectUri`
- `Scope`
- connection string к PostgreSQL

Нужно убедиться, что redirect URI указывает на внешний адрес приложения:
- `https://<app-host>/auth/callback`

## 3. Базовая настройка Authentik

В Authentik нужно:
- создать admin-аккаунт платформы
- сохранить его отдельно как технический доступ
- создать OAuth2/OIDC provider для `BusinessEntity`
- создать application и привязать provider к application
- зарегистрировать корректные redirect URI
- зарегистрировать logout URI при необходимости

## 4. Scope mappings в Authentik

Для приложения должны отдаваться claims:
- `preferred_username`
- `email`
- `name`
- `groups`

Минимально нужно настроить mapping-и так, чтобы приложение получало:
- имя пользователя
- email
- список групп

Особенно важно:
- `groups` должен приходить в токене
- `preferred_username` должен приходить в токене

## 5. Группы в Authentik

Нужно создать как минимум:
- `BusinessEntityAdmins`
- базовую пользовательскую группу приложения

Например:
- `BusinessEntityUsers`
- `BusinessEntityEditors`
- другие прикладные группы по бизнес-сценарию

Важно:
- `IsGeneralAdmin` в приложении определяется по membership в группе `BusinessEntityAdmins`
- `IsAkadmin` определяется только по username `akadmin`

## 6. Пользователи

Нужно создать:
- технического администратора Authentik `akadmin`
- минимум одного общего администратора приложения
- минимум одного обычного пользователя приложения

Рекомендуется:
- `akadmin` использовать только как технического администратора платформы
- обычных прикладных администраторов включать в группу `BusinessEntityAdmins`
- обычных пользователей включать в базовую группу приложения

## 7. Права в Authentik

Если прикладной администратор должен управлять пользователями через Authentik Admin UI, ему нужно выдать:
- доступ в admin interface
- права на просмотр/создание/изменение/удаление пользователей

Лучше делать это через отдельную роль или отдельную admin-group в Authentik.

## 8. Проверка после запуска

После первого старта нужно проверить:
- логин через Authentik работает
- callback в приложение работает
- в приложении отображается нормальный `preferred_username`, а не `sub`
- в claims приходит `groups`
- пользователь из `BusinessEntityAdmins` получает `IsGeneralAdmin = true`
- пользователь `akadmin` получает `IsAkadmin = true`
- обычный пользователь не получает admin-флаги

## 9. Что сохранить отдельно

После настройки нужно сохранить в защищённом месте:
- URL Authentik
- URL приложения
- `ClientId`
- `ClientSecret`
- admin credentials
- список созданных групп
- список базовых пользователей
- перечень scope mappings и их назначение

## 10. Короткая итоговая схема

```text
Infrastructure
  -> PostgreSQL
  -> Authentik
  -> BusinessEntity

Authentik
  -> OIDC Provider
  -> Application
  -> Redirect URI
  -> Scope mappings: preferred_username, email, name, groups
  -> Groups: BusinessEntityAdmins + app groups
  -> Users: akadmin + app admins + regular users

BusinessEntity
  -> login через Authentik
  -> получает claims
  -> строит BusinessEntityUser
  -> определяет IsGeneralAdmin и IsAkadmin
```
