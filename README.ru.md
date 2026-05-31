# Business Entity Engine

[English README](README.md)

Business Entity Engine - самостоятельно разворачиваемая платформа для построения баз знаний и приложений на бизнес-объектах, где основная модель данных строится вокруг графа бизнес-сущностей.

Текущее приложение - это система на ASP.NET Core / Blazor Server, в которой есть:

- пространства как верхний рабочий контекст;
- папки, документы и рич-документы как бизнес-сущности;
- графовые связи между сущностями;
- редактирование документов и рич-документов;
- аутентификация через Authentik;
- администрирование пользователей, ролей, групп и прав доступа;
- deployment assets для Docker Compose;
- экспериментальное направление mini-app/plugin модели.

## Статус проекта

Business Entity Engine находится на этапе публичной инкубации / preview.

На этом этапе платформа намеренно остается максимально открытой для практической обратной связи. Ее можно ставить, форкать, менять, тестировать, использовать для прототипов, писать плагины и проверять на реальных сценариях.

Важные замечания preview-этапа:

- API, storage layout, скрипты развертывания и механики миграции могут меняться.
- Это еще не отполированный production-дистрибутив.
- Безопасность, backup, storage и модель прав доступа еще укрепляются.
- Перед выставлением инстанса в сеть надо внимательно проверить конфигурацию и secrets.

## Лицензия

Исходный код и документация в этом репозитории распространяются под MIT License. См. [LICENSE](LICENSE).

На этапе публичной инкубации текущая публичная ветка Business Entity Engine распространяется под MIT License. После выхода стабильной версии у проекта могут появиться другие варианты лицензирования, включая более закрытые коммерческие редакции или поставки. При этом последняя версия, опубликованная под MIT License, будет зафиксирована как MIT-версия и останется доступной в этом качестве навсегда.

MIT License относится к коду и документации. Она не передает права на использование названия Business Entity, логотипа, иконок, визуальной айдентики, официального статуса проекта, названий официальных релизов, официальных доменов или официальных каналов распространения таким образом, который может вводить пользователей в заблуждение.

См. [TRADEMARK.md](TRADEMARK.md) для политики по бренду и товарным знакам.

## Граница бренда

Форки и производные проекты приветствуются.

Можно корректно писать:

- "Fork of Business Entity Engine"
- "Powered by Business Entity Engine"
- "Compatible with Business Entity"
- "Unofficial plugin for Business Entity"
- "Built on Business Entity Engine"

Нельзя выдавать форк, сервис, хостинг, пакет, организацию или сайт за официальный Business Entity без письменного разрешения владельца бренда.

## Структура репозитория

```text
BusinessEntity/              Основное ASP.NET Core / Blazor Server приложение
BlazorServerWebLogger/       Сервис web-логирования
BusinessEntity.Resources/    Общие ресурсы
BusinessEntityStorage/       Локальное runtime-хранилище, не часть публичного релиза
Deployment/                  Файлы release bundle и install scripts
Context/                     Архитектурные заметки и политики проекта
Powershell/                  Скрипты разработки и сборки release bundle
docker-compose.yml           Development Docker Compose stack
CHANGELOG.md                 Журнал изменений
LICENSE                      MIT license
TRADEMARK.md                 Политика бренда и trademark
```

## Запуск из исходников

Требования:

- Docker-compatible runtime;
- Docker Compose plugin;
- .NET SDK 6.0, если приложение собирается или запускается вне Docker.

Для локального development stack:

```powershell
docker network create docker-business-entity-common-bridge
docker compose up -d --build
```

Локальные endpoints по умолчанию:

- Business Entity: `http://localhost:7000`
- Authentik: `http://localhost:9000`
- Web Logger: `http://localhost:5080`

Корневой `docker-compose.yml` ориентирован на разработку. Перед публичным развертыванием замените development secrets, проверьте порты, проверьте настройки Authentik и используйте deployment assets из [Deployment](Deployment/README.md).

## Развертывание

Целевая модель развертывания - release bundle для одного Windows или Linux хоста с Docker Compose.

См.:

- [Deployment README](Deployment/README.md)
- [Политика развертывания](Context/Policy/deployment-policy.md)

Целевой operator flow:

```text
скачать release bundle
распаковать
запустить install.ps1 / install.bat / install.sh
поднять Docker Compose stack
загрузить начальные данные
открыть приложение
```

## Чеклист перед публичной публикацией

Перед публикацией репозитория проверьте, что в нем нет:

- реальных production secrets;
- приватных Authentik tokens;
- приватных database dumps;
- локального storage с пользовательскими файлами;
- backups;
- клиентских или персональных данных;
- приватных logs;
- machine-specific путей, которые не должны быть публичными.

Любой secret, который когда-либо был закоммичен, надо ротировать до того, как считать публичный репозиторий безопасным.

## Безопасность

Не публикуйте уязвимости в открытых issues, если сообщение содержит детали эксплуатации.

Пока отдельный `SECURITY.md` не создан, сообщайте security concerns приватно maintainers проекта. По возможности прикладывайте affected version, deployment mode, steps to reproduce, logs и минимальный proof of concept.

Security-sensitive зоны: authentication, authorization, проверка прав доступа, file uploads, обработка HTML в рич-документах, imports, backups, path handling и plugin boundaries.

## Участие

Репозиторий сейчас находится на раннем этапе публичной инкубации. Особенно полезна практическая обратная связь:

- проблемы установки;
- обратная связь по deployment;
- bug reports;
- security reports;
- feedback по storage и migration;
- feedback по permission model;
- сценарии редактирования рич-документов;
- эксперименты с plugins и mini-apps.

Изменения лучше присылать небольшими, с понятным описанием сценария, который они улучшают.

## Официальный статус

Официальными дистрибутивами Business Entity считаются только repositories, releases, packages, websites и services, явно опубликованные maintainers проекта.

Форки и community builds должны явно обозначать себя как unofficial.
