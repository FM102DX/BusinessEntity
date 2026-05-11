# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.12.0] - 2026-05-10 18:49:14 +03:00

### MAJOR-FEATURES

#### Rich-text document backup export
- Человекочитаемый export rich-doc изменен с набора Markdown-файлов на один HTML-файл `{entityName}--human-readable.html`.
- Для HTML-export добавлена папка `attachments/`, куда копируются изображения и будущие вложения в обычных файловых форматах; HTML ссылается на локальные файлы и открывается из файловой системы без приложения.

### MINOR-FEATURES

#### Rich-text document image storage
- Внутреннее файловое хранилище embedded images больше не пишет новые изображения как `*.bin`: variant сохраняется в родном расширении по имени файла или MIME-типу.
- В `metadata.json` embedded-файла добавлен `storedFileName`; чтение сохраняет fallback на legacy `*.bin` для уже существующих картинок.

#### Backup policies
- Политики backup и file-object storage обновлены под HTML-export rich-doc, папку `attachments` и запрет новых `original.bin`.

## [0.11.0] - 2026-05-10 18:33:28 +03:00

### MAJOR-FEATURES

#### Rich-text document images
- Изображения rich-doc переведены в inline-модель: картинка хранится и редактируется как inline atom внутри текста, может стоять в одной строке с другой картинкой и текстом между ними.
- Канонический HTML inline-картинки изменен на `span.rich-text-inline-image` с document-local image id; Tiptap, HTML import, chunk serializer и read-side cache сохраняют этот marker и серверно восстанавливают `img src`.
- Сохранение edited chunks теперь сохраняет embedded-файлы, появившиеся при HTML-конвертации inline-картинок, до записи chunk-ссылок.

#### Space backup
- Добавлен background backup пространств: `SpaceBackupService` обходит пространства, пишет только dirty business entities, обновляет relations и публикует `manifest.json` в entity-folder layout.
- В администрировании пространств добавлены настройки backup: включение, папка, период, ручной запуск и очистка backup для конкретного пространства.
- Добавлен generic backup handler, который выгружает entity/data/properties/chunks/files в JSON и формирует human-readable экспорт для обычных документов и rich-doc, включая вложенные изображения.

### MINOR-FEATURES

#### Rich-text document image storage
- Файловое хранилище embedded images сохраняет расширение по имени файла или content type, пишет `StoredFileName` в metadata и сохраняет совместимость с legacy `*.bin`.
- Стили просмотра и редактирования rich-doc обновлены для inline-картинок: `inline-block`, вертикальное выравнивание по строке и сохранение кликабельности.

#### Backup configuration and policies
- В конфигурацию добавлены `SpaceBackup` настройки и host-path mapping для отображения backup/storage путей из Docker.
- Добавлена политика `space-backup-policy-entity-folder-layout.md`; политики rich-doc и file-object storage обновлены под inline image marker.
- `.gitignore` уточнен так, чтобы папка `BusinessEntity/Services/BackupRestore/` не попадала под общий ignore-паттерн `Backup*/`.

## [0.10.0] - 2026-05-09 18:32:53 +03:00

### MAJOR-FEATURES

#### Rich-text document images
- В rich-doc добавлена вставка изображений из clipboard: редактор загружает картинку через API, сохраняет ее как embedded-файл документа и вставляет в content image-блок.
- Для изображений rich-doc добавлено сохранение display-size в самом документе: ширина/высота проходят через block model, HTML serializer и HTML import/conversion pipeline.
- В режиме редактирования добавлено контекстное меню изображения с быстрыми размерами `100 / 200 / 300 / 500`, режимом `[orig]` и custom-width вводом.
- В режиме просмотра и редактирования добавлен in-page просмотрщик полного изображения по левому клику с закрытием через `ESC`, фон или кнопку закрытия.

#### File object storage
- Embedded-файлы rich-doc вынесены из disposable filesystem контейнера во внешний storage root, задаваемый через `Storage:RootPath`.
- Docker compose монтирует host-каталог `BusinessEntityStorage` в `/app/storage`, чтобы загруженные изображения переживали пересборку и пересоздание контейнера.
- Физическая структура файлового storage приведена к схеме `business-entities/{businessEntityId}/images/{imageId}/{variant}.bin`.

### MINOR-FEATURES

#### Rich-text document UX
- Для изображений rich-doc добавлен pointer-cursor при hover, чтобы пользователь видел кликабельность.
- Сериализация image HTML теперь добавляет `data-rich-image-id`, `data-display-variant`, `loading="lazy"` и размерные атрибуты.
- Импорт HTML распознает уже существующие rich-doc embedded images и не пытается переимпортировать их как внешние картинки.
- Форматирование чисел во вкладке статистики rich-doc переведено на пробелы как разделители групп.

#### Shell UI
- Пользовательское меню в шапке переведено с native `details` на управляемое Blazor-состояние и автоматически закрывается через 5 секунд после ухода мыши.

#### Policies and storage configuration
- Добавлена политика хранения файловых объектов, вложений, архивов и прочих загружаемых данных во внешнем storage.
- `BusinessEntityStorage/` добавлен в `.gitignore`, а локальный fallback storage оставлен только как non-Docker default через `App_Data/RichDocumentData`.

## [0.9.0] - 2026-05-09 11:04:03 +03:00

### MAJOR-FEATURES

#### Rich-text document UI
- В правую панель rich-doc добавлена вкладка `Стат.` со статистикой по chunks текущей просматриваемой версии: общее количество, среднее, минимальное и максимальное число символов в chunk.
- Выбор количества уровней оглавления заменен с combobox на radio-переключатели `1 / 2 / 3`; значение по умолчанию при открытии документа теперь равно `1`.

#### UserMiniApp
- Для rich-doc добавлено пользовательское свойство `RichDocDisplayedLevelProperty`, которое хранит `DocumentId` и выбранную глубину отображения оглавления в `UserProperties`.
- API `UserMiniApp` и `UserConnector` расширены методами чтения и сохранения глубины оглавления для текущего пользователя.

### MINOR-FEATURES

#### Rich-text document statistics
- Статистика chunks собирается асинхронно и батчами через `RichTextDocumentHelper`, с учетом выбранной версии документа и без сборки полного snapshot.
- Вкладка статистики защищена от повторного запуска одинакового расчета при rerender и поддерживает немедленное обновление через icon-only кнопку.

#### Rich-text document outline
- Read/edit представления rich-doc синхронизируют видимые уровни оглавления с пользовательским свойством и сохраняют изменение сразу после переключения radio.
- Стили оглавления обновлены под компактные radio-сегменты без изменения поведения переходов по заголовкам.

## [0.8.0] - 2026-05-08 20:55:00 +03:00

### MAJOR-FEATURES

#### Rich-text document
- Добавлен новый тип бизнес-объекта `RichTextDocument` с chunked-хранением текста, отдельной страницей просмотра/редактирования, импортом `.txt`, `.md`, `.html` и встроенным хранением файлов документа.
- Реализованы чтение и редактирование больших rich-text документов через viewport/chunk-window: загрузка порциями, автоскролл к позициям, переходы по содержанию и сохранение измененных чанков.
- Добавлены оглавление rich-doc, поиск по тексту, пользовательские закладки и правый tab-control инструментов документа.

#### Версионирование BusinessEntityData
- В базовую модель `BusinessEntityData` и DTO-хранилище добавлены `Version`, `HasVersions` и признак типа chunk-хранения `ChunkStorageType`.
- `DataProviderMiniApp` переведен на append-only сохранение версионируемых `BusinessEntityDataItems`; чтение payload выбирает актуальную запись с максимальной версией.
- Для rich-doc добавлено версионирование измененных чанков и UI-вкладка `Версии`, показывающая версии документа из `BusinessEntityDataItems`.

#### UserMiniApp
- Добавлен `UserMiniApp` с собственным PostgreSQL-хранилищем пользователей и пользовательских properties.
- Закладки rich-doc перенесены в пользовательские properties текущего пользователя.
- Re-seed теперь удаляет локальную запись текущего пользователя перед повторной заливкой данных.

### MINOR-FEATURES

#### Rich-text document UI
- Сообщения rich-doc перенесены из строки под заголовком в отдельный блок `Сообщения` крайнего правого сайдбара.
- Поиск, закладки и версии объединены в единый tab-control в правой колонке документа.
- Добавлены настройки rich-doc, конвертеры импорта и клиентские JS-модули viewport/editor.

#### DataProviderMiniApp и storage
- Расширены DTO и property-таблицы для `BusinessEntityData`, `BusinessEntityDataChunks` и их properties.
- Добавлены EF/Postgres и in-memory репозитории для chunk/property DTO, индексы версий и schema bootstrap в `Program.cs`.
- Добавлен публичный DataProvider API для чтения списка версий payload.

#### TreeMiniApp и навигация
- Главное дерево вынесено в `TreeMiniApp` с connector/service/facade границей.
- Добавлена модель узла rich-text документа и маршрутизация на страницу rich-doc из дерева.

#### Политики и сопровождение
- Добавлены политики по rich-doc хранению, чтению, редактированию, пользователям и версионированию бизнес-сущностей.
- Обновлены отчеты и служебные routines для анализа chunk-хранилища и сопровождения changelog.

## [0.7.0] - 2026-04-26 18:48:56 +03:00

### MAJOR-FEATURES

#### DataProviderMiniApp и формализованное хранение payload
- В `DataProviderMiniApp` введена формализованная система typed payload-конвертеров для `BusinessEntityData` с отдельными реализациями по типам бизнес-объектов.
- Чтение и запись payload переведены на симметричную схему `typed payload <-> converter <-> storage envelope`, чтобы правила хранения больше не были размазаны по connector, service и helper-слою.
- Для payload-типов `Document`, `Folder`, `Space` и `SysParameters` добавлены отдельные storage-конвертеры и фабрика их разрешения по типу сущности.

### MINOR-FEATURES

#### Envelope и data-provider API
- `DataPayloadEnvelopeSerializer` упрощен до общей envelope-утилиты без knowledge о конкретных типах payload.
- Контракты и реализации `IDataProviderConnector` и `IDataProviderCrudService` выровнены на typed `IBusinessEntityData` вместо частичного string-based пути.

#### BusinessEntityHelper
- `BusinessEntityHelper` переведен с raw string-пути документа на typed `Document` payload при сохранении и чтении.
- Старые локальные special-case ветки извлечения и восстановления document payload из helper-а удалены.

## [0.6.0] - 2026-04-25 16:50:26 +03:00

### MAJOR-FEATURES

#### Главное дерево как mini-app
- Главное дерево вынесено в отдельный `TreeMiniApp` с собственными контрактами, фасадом, сервисом, компонентом и startup-регистрацией.
- Внутренняя логика дерева переразложена по mini-app-границе: внутри mini-app компонент работает напрямую с сервисом, а bus оставлен только для внешнего сценария загрузки дерева пространства.
- Shell приложения переключен на новый tree-компонент mini-app вместо старого встроенного компонента.

### MINOR-FEATURES

#### Навигация по документам из дерева
- Открытие документов из дерева переработано: одиночный клик открывает страницу документа с короткой задержкой для корректного распознавания двойного клика.
- Двойной клик теперь открывает документ сразу в режиме редактирования через route/query-флаг.
- В контекстное меню документа в дереве добавлены пункты `Открыть` и `Редактировать`.

#### Страница документа
- Страница документа и компонент документа расширены поддержкой стартового режима редактирования при открытии по ссылке из дерева.

## [0.5.0] - 2026-04-25 03:24:00 +03:00

### MAJOR-FEATURES

#### Системные параметры и административные настройки
- Введен новый тип бизнес-объекта `SysParametersTp` и typed payload `SysParameters` для хранения общих системных настроек без связей в дереве.
- `BusinessEntityHelper` расширен generic-операциями загрузки singleton-объекта с payload и сохранения `BusinessEntity<T>` как пары `BusinessEntity` + `BusinessEntityData`.
- В админке добавлена отдельная вкладка системных параметров с редактированием `CompanyName`, сохраняемого в singleton-объект `SysParameters`.

#### Шапка приложения и пользовательское меню
- Содержимое верхней шапки выделено в отдельный Razor-компонент и переведено на чтение системных параметров из singleton `SysParameters`.
- В шапке появилось динамическое отображение названия компании, которое скрывается, если параметр не задан.
- Для аутентифицированного пользователя добавлен аватар-кружок с выпадающим меню навигации и logout-сценарием.

### MINOR-FEATURES

#### Админка
- Вкладка `Общее` разгружена: системные параметры вынесены в отдельную вкладку, а в общем разделе оставлены служебные административные действия.

#### Главное дерево и шапка
- Из верхней панели убрано дублирующее отображение текущего пространства.
- Дерево и shell согласованы с новой шапкой и пользовательским меню без изменения core-логики загрузки дерева.

## [0.4.0] - 2026-04-24 19:57:58 +03:00

### MAJOR-FEATURES

#### Core-модель бизнес-объектов
- Базовая модель core-слоя перестроена вокруг разделения `BusinessEntity`, `BusinessEntityData` и `BusinessEntityRelation` с typed-агрегатом `BusinessEntity<T>`.
- Введены `IBusinessEntity`, `IBusinessEntityData`, `IBusinessEntityFactory` и новая фабрика сущностей, а `BusinessEntityHelper` адаптирован под создание и работу с новой моделью бизнес-объектов.
- Из business-слоя убрано наследование от storage-`BaseEntity`: идентификаторы и временные поля перенесены напрямую в бизнес-сущности и их интерфейсы.

#### Хранение данных и интеграция с Postgres
- `DataProviderMiniApp` переведен с in-memory режима на рабочее Postgres-хранилище для `BusinessEntityDto`, `BusinessEntityDataDto` и `BusinessEntityRelationDto`.
- Основное приложение и web-logger разведены по разным базам данных внутри PostgreSQL-инстанса: `business_entity` и `web_logger`.
- Хранение payload переведено с `byte[]` на versioned JSON envelope в строковом поле `Data`, а сериализация централизована так, чтобы JSON в БД оставался читаемым и сохранял Unicode без `\\uXXXX`.

#### Seed, helper-слой и управление данными
- Seed-логика полностью переведена на `BusinessEntityHelper`, а не на прямую работу с connector-слоем.
- Добавлен принудительный `Re-seed`: очистка business-данных через специальное debug-сообщение `DataProviderMiniApp` и повторная инициализация данных.
- Семантика дерева и отношений упрощена: `VisuallyContains` удален, все дерево построено на `Contains`.

### MINOR-FEATURES

#### Логирование и web-logger
- На стороне web-logger введена очередь входящих сообщений и фоновая запись в БД с retry, чтобы не терять логи при наплыве запросов.
- Клиентский `WebLoggerService` в основном приложении переведен на очередь исходящих сообщений с повторной отправкой текущего сообщения до успеха.
- Исправлены дубли логов в UI web-logger: защищен polling, добавлен дедуп по `Id`, `Timer` заменен на `PeriodicTimer`.

#### Пространства и администрирование
- Добавлена вкладка управления пространствами в админке и выделен `SpaceHelper`, инкапсулирующий создание, переименование и удаление пространств через `BusinessEntityHelper`.
- Исправлен выбор пространства: добавлен серверный endpoint, доработан `UserContextService` и устранены проблемы с cookie/state flow.
- Для имен пространств введена централизованная валидация по допустимым символам и минимальной длине.

#### Главное дерево и UX
- Доработан multiselect в дереве: первый `CTRL + ЛКМ` начинает корректную групповую сессию, может подхватывать уже выбранный элемент и синхронизирует фактический набор выбранных узлов.
- Улучшены визуальные состояния дерева: переработано выделение выбранных узлов и настроен hover-стейт отдельно от selected state.

#### Инфраструктура и сопровождение
- Починены Dockerfile и container build для основного приложения и web-logger.
- Обновлена политика хранения данных с фиксацией фактической схемы хранения, JSON envelope и Postgres-модели.

## [0.3.0] - 2026-04-21 16:01:57 +03:00

### MAJOR-FEATURES

#### Тестовые данные и mini-app архитектура
- Добавлен `SampleDataMiniApp` как отдельная mini-app-обёртка над существующей логикой заливки тестовых данных.
- Инициализация тестового сидирования перенесена из прямого вызова core-сервиса в startup-процедуру через mini-app контракт, чтобы логика заливатора не смешивалась с общей core-логикой приложения.

### MINOR-FEATURES

#### Старт приложения
- `Program.cs` обновлён так, чтобы startup-последовательность работала через mini-app регистрации и явную инициализацию `SampleDataMiniApp`.

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
