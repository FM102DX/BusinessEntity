# UserMiniApp

## Назначение

`UserMiniApp` отвечает за всё, что связано с текущим пользователем приложения.

Он:
- получает текущего пользователя из `AuthentikSessionManager`
- превращает raw claims в нормализованный объект `BusinessEntityUser`
- извлекает группы пользователя
- отдаёт данные о пользователе другим частям приложения через `ReactiveUI IMessageBus`
- предоставляет короткий `IUserConnector` для адресного доступа к пользователю из UI и сервисов

## Что наружу отдаёт MiniApp

Публичные контракты:
- `BusinessEntityUser`
- `BusinessEntityClaim`
- `IUserMiniApp`
- `IUserConnector`
- `GetUserRequest`
- `GetUserResponse`

Главный объект, который получают другие части системы:
- `BusinessEntityUser`

В нём лежат:
- `UserId`
- `UserName`
- `Email`
- `IsAuthenticated`
- `Groups`
- `Claims`

## Как работает

Схема работы такая:

```text
UI / Service
  -> IUserConnector.GetCurrentUserAsync()
  -> UserConnector
  -> IMessageBus.SendMessage(GetUserRequest)
  -> UserMiniAppMessageHandler
  -> UserMiniAppService
  -> BusinessEntityUserFactory
  -> AuthentikSessionManager
  -> ClaimsPrincipal
  -> BusinessEntityUser
  -> IMessageBus.SendMessage(GetUserResponse)
  -> UserConnector
  -> UI / Service
```

## Внутреннее устройство

Основные части mini-app:
- `UserMiniApp`
  - фасад mini-app
- `UserMiniAppMessageHandler`
  - подписка на `GetUserRequest` и публикация `GetUserResponse`
- `UserMiniAppService`
  - внутренняя логика получения пользователя
- `UserMiniAppState`
  - кэш пользователя в пределах текущего DI scope
- `BusinessEntityUserFactory`
  - сборка `BusinessEntityUser` из `ClaimsPrincipal`
- `UserConnector`
  - внешний точечный адаптер для других модулей

## Где используется

Сейчас mini-app уже используется в:
- `AuthInfo`
- `Index`
- `Logging`

То есть UI больше не обязан напрямую разбирать claims из `AuthentikSessionManager`, а получает готовый объект пользователя.

## Зачем это нужно

Этот mini-app убирает разрозненный разбор claims по приложению и делает пользовательские данные единым модулем.

Польза:
- меньше прямых зависимостей от auth-сервиса
- единая модель пользователя для всего приложения
- группы и claims нормализуются в одном месте
- другие mini-app и сервисы могут брать пользователя через маленький connector
- межмодульный доступ идёт через bus и не раздувает DI-граф
