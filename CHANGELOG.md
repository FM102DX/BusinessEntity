# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-04-21 15:15:35 +03:00

### MAJOR-FEATURES

#### Архитектура mini-app и хранение данных
- В систему введена mini-app архитектура с коннекторами, message bus-взаимодействием и явной инициализацией mini-app при старте приложения.
- Добавлен `DataProviderMiniApp` как единая точка CRUD-работы с хранилищем бизнес-сущностей, их данных и отношений между ними.
- Введена DTO-модель хранения `BusinessEntityDto`, `BusinessEntityDataDto`, `BusinessEntityRelationDto`, включая сериализованное бинарное JSON-хранение payload в `BusinessEntityDataDto`.
- Реализована полная обработка запросов на сущности, данные и relations через входящие bus-сообщения, connector-слой и typed-репозитории.

#### Пользователи и авторизация
- Добавлен `UserMiniApp` для получения текущего пользователя через mini-app контракт и bus-roundtrip.
- Существенно переработана Authentik-интеграция: настройки вынесены в окружение и конфиг, добавлены группы, улучшены login/logout и обновление сессии.
- Добавлены и доработаны механизмы первичной инсталляции и внешнего запуска окружения, связанные с Authentik и docker-развёртыванием.

#### Главное дерево, пространства и документы
- В систему возвращено и развёрнуто главное визуальное дерево с пространствами, папками и документами.
- Добавлены и стабилизированы ключевые операции дерева: выбор пространства, создание папок и документов, drag-and-drop, multiselect, удаление, rename и контекстные меню.
- Реализовано и доработано отображение страниц документа, простое редактирование документа и корректное поведение при перезагрузке страниц и пространств.

#### Архитектура решения
- Проект переведён в одно основное приложение `BusinessEntity`: код из прежних проектов схлопнут в единый проект, а старая многопроектная раскладка убрана.

### MINOR-FEATURES

#### DataProviderMiniApp и инфраструктура хранения
- Репозиторный слой приведён к одному `IAsyncRepository<T>` внутри mini-app без дублирующих контрактов и legacy-фабрик.
- Для всех трёх DTO-репозиториев добавлены in-memory реализации; EF/Postgres-регистрации временно отключены в DI.
- Bus-подписки и сообщения внутри `DataProviderMiniApp` разложены по группам объектов для более понятной поддержки.
- `Program.cs` структурирован по блокам зависимостей и снабжён поясняющими комментариями.

#### Производительность, стабильность и сидирование
- Исправлены проблемы медленной загрузки приложения и ошибки загрузки дерева.
- Восстановлен и стабилизирован механизм seed-инициализации данных.
- Исправлены отдельные проблемы логина и сценарии обновления состояния приложения.

#### Логирование, UI и сопровождение
- Логирование вынесено и улучшено через web-logger, включая доработки интерфейса логгера.
- Добавлены комментарии и сопровождающая документация по mini-app, DataProvider, UserMiniApp и связанным участкам кода.
- Выполнены множественные чистки, рефакторинг и вынос логики из UI/контроллеров в сервисы и helper-слои.

## [0.1.0] - 2025-06-07

### Added
- OAuth external link integration established in the system
- Cookie-based test authorization mechanism implemented
- Basic system infrastructure and authentication framework

### Notes
- This is the initial release version with foundational authentication components
- OAuth connectivity configured and operational
- Test authorization through cookies enabled for development and testing purposes

---

*For more information about this project, see the [README.md](README.md) file.*
