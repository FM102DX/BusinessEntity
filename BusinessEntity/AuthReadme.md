# Сценарий аутентификации BusinessEntity  
(пошаговая ASCII-диаграмма вызовов и сетевых обращений)

------------------------------------------------------------------
ВХОД (браузер → BusinessEntity → Authentic)  
------------------------------------------------------------------

 1. [Pages.Index]                 Браузер (аноним) GET /
    └─ обнаруживает отсутствие аутентификации

 2. [Pages.Index]                 → 302 на
       http://localhost:9000/application/o/authorize/
       (client_id, redirect_uri, scope …)

 3. Браузер                       → форма логина Authentic
    └─ пользователь вводит учётные данные

 4. Authentic                     → 302 обратно на
       http://localhost:7000/auth/callback?code=XXX&state=Lw==

 5. [AuthController.Callback]     получает code/state

 6. AuthController.Callback       → POST /token  
       URL: http://authentic-server-1:9000/application/o/token/  
       ЧТО: обмен `code` на access- и id-токены

 7. ApplicationSideAuthService    .ValidateToken  
       → POST /introspect  
       URL: http://authentic-server-1:9000/application/o/introspect/  
       ЧТО: проверка валидности токена и scope

 8. AuthController.Callback       формирует ClaimsPrincipal,  
       устанавливает cookie (.AddCookie)

 9. AuthController.Callback       → 302 /  (пользователь авторизован)

10. [Pages.Index]                 Браузер GET /  
    └─ пользователь admin, 19 клеймов


------------------------------------------------------------------
ВЫХОД (браузер → BusinessEntity → Authentic)  
------------------------------------------------------------------

 1. [Pages.Index]                 Пользователь нажал «Выход»
    └─ лог: «admin запрашивает sign out»

 2. [Pages.Index]                 → 302 /auth/logout

 3. [AuthController.Logout]       начинает обработку

 4. ApplicationSideAuthService    .HealthCheck  
       → GET /-/health/live/  
       URL: http://authentic-server-1:9000/-/health/live/  
       ЧТО: проверка живости сервера аутентика

 5. ApplicationSideAuthService    .RevokeAsync  
       → POST /revoke  
       URL: http://authentic-server-1:9000/application/o/revoke/  
       ЧТО: отзыв refresh/access токена (back-channel)  
       ПОДРОБНЕЕ:  
         • Приложение передаёт текущий *refresh-токен* (а также client_id /
           client_secret) в теле POST-запроса.  
         • Authentic помечает токен как «revoked» в БД и кладёт его в
           внутренний список блокировок.  
         • Любая последующая попытка использовать этот токен через
           `/token` (refresh-flow) или `/introspect` вернёт `invalid_token`.  
         • Так как refresh-токен аннулирован, связанный *access-токен*
           доживает максимум до окончания своего TTL и не может быть
           обновлён; пользователь считается вышедшим во всех остальных
           клиентах.  
         • Локально сервис немедленно забывает пару токенов и переводит
           контекст ASP.NET Core в состояние «SignedOut» до очистки cookie
           на шаге 8.

 6. ApplicationSideAuthService    .EndSessionAsync  
       → GET /end-session  
       URL: http://authentic-server-1:9000/application/o/kms-be/end-session/
            ?client_id=…&post_logout_redirect_uri=…&id_token_hint=…  
       ЧТО: уведомляет Authentic завершить OIDC-сеанс

 7. ApplicationSideAuthService    ← revoke=True, end-session=True  
       └─ лог: «Local sign out completed successfully»

 8. AuthController.Logout         удаляет локальные cookie

 9. AuthController.Logout         → 302 front-channel logout  
       http://localhost:9000/application/o/kms-be/end-session/…

10. Браузер                       открывает страницу Logout Authentic,
                                  затем 302 на /auth/logged-out

11. [Pages.Index]                 Браузер GET /
    └─ пользователь теперь анонимен
------------------------------------------------------------------