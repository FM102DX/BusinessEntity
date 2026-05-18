using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal;

// Тонкий клиент Authentik Admin API для user mini-app.
internal sealed class AuthentikManagementClient
{
    private const string HttpClientName = "AuthentikAuth";
    private const string DefaultApplicationUsersGroupName = "GeoUsers";
    private const string DefaultManagedUsernamePrefix = "user-";
    private const int DefaultGeneratedUsernameCodeLength = 5;
    private const int PageSize = 100;

    private static readonly char[] GeneratedCodeAlphabet = "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiToken;
    private readonly string _applicationUsersGroupName;
    private readonly string _managedUsernamePrefix;
    private readonly int _generatedUsernameCodeLength;

    public AuthentikManagementClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;

        var section = configuration.GetSection("AuthentikAuth");
        _apiToken = Environment.GetEnvironmentVariable("AUTHENTIK_API_TOKEN")
                    ?? section["ApiToken"]
                    ?? section["ManagementApiToken"]
                    ?? string.Empty;
        _applicationUsersGroupName = Environment.GetEnvironmentVariable("AUTHENTIK_APPLICATION_USERS_GROUP")
                                     ?? section["ApplicationUsersGroupName"]
                                     ?? DefaultApplicationUsersGroupName;
        _managedUsernamePrefix = section["ManagedUsernamePrefix"] ?? DefaultManagedUsernamePrefix;
        _generatedUsernameCodeLength = Math.Clamp(
            ReadConfiguredInt(section["GeneratedUsernameCodeLength"], DefaultGeneratedUsernameCodeLength),
            1,
            16);
    }

    // Возвращает пользователей Authentik, допущенных к приложению через configured group.
    public async Task<IReadOnlyList<AuthentikUserRecord>> GetApplicationUsersAsync(CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["page_size"] = PageSize.ToString(),
            ["type"] = "internal"
        };

        if (!string.IsNullOrWhiteSpace(_applicationUsersGroupName))
        {
            query["groups_by_name"] = _applicationUsersGroupName;
        }

        return await ReadPagedUsersAsync("/api/v3/core/users/" + QueryHelpers.AddQueryString(string.Empty, query), cancellationToken);
    }

    // Создает Authentik-пользователя приложения с системным username user-[5 букв].
    public async Task<AuthentikUserRecord> CreateApplicationUserAsync(CancellationToken cancellationToken)
    {
        var username = await GenerateUniqueUsernameAsync(cancellationToken);
        var groups = new List<string>();

        var applicationGroupPk = await TryGetApplicationGroupPkAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(applicationGroupPk))
        {
            groups.Add(applicationGroupPk);
        }

        var payload = new
        {
            username,
            name = username,
            is_active = true,
            groups,
            email = string.Empty,
            path = "users",
            type = "internal",
            attributes = new { }
        };

        using var request = CreateJsonRequest(HttpMethod.Post, "/api/v3/core/users/", payload);
        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "создать пользователя в Authentik");

        using var document = JsonDocument.Parse(body);
        return ReadUser(document.RootElement);
    }

    // Меняет username пользователя в Authentik и возвращает обновленную запись.
    public async Task<AuthentikUserRecord> UpdateUsernameAsync(
        int authentikUserPk,
        string username,
        CancellationToken cancellationToken)
    {
        if (authentikUserPk <= 0)
        {
            throw new InvalidOperationException("У пользователя нет идентификатора Authentik.");
        }

        var payload = new { username };
        using var request = CreateJsonRequest(new HttpMethod("PATCH"), $"/api/v3/core/users/{authentikUserPk}/", payload);
        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "изменить логин пользователя в Authentik");

        using var document = JsonDocument.Parse(body);
        return ReadUser(document.RootElement);
    }

    // Удаляет пользователя из Authentik.
    public async Task DeleteUserAsync(int authentikUserPk, CancellationToken cancellationToken)
    {
        if (authentikUserPk <= 0)
        {
            throw new InvalidOperationException("У пользователя нет идентификатора Authentik.");
        }

        using var request = CreateRequest(HttpMethod.Delete, $"/api/v3/core/users/{authentikUserPk}/");
        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "удалить пользователя из Authentik");
    }

    private async Task<string> GenerateUniqueUsernameAsync(CancellationToken cancellationToken)
    {
        var existingNames = (await ReadPagedUsersAsync(
                $"/api/v3/core/users/?page_size={PageSize}&type=internal",
                cancellationToken))
            .Select(user => user.Username)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var username = _managedUsernamePrefix + GenerateCode(_generatedUsernameCodeLength);
            if (!existingNames.Contains(username))
            {
                return username;
            }
        }

        throw new InvalidOperationException("Не удалось подобрать свободный логин Authentik для нового пользователя.");
    }

    private async Task<string?> TryGetApplicationGroupPkAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_applicationUsersGroupName))
        {
            return null;
        }

        var query = QueryHelpers.AddQueryString(
            "/api/v3/core/groups/",
            new Dictionary<string, string?>
            {
                ["search"] = _applicationUsersGroupName,
                ["page_size"] = PageSize.ToString()
            });

        using var request = CreateRequest(HttpMethod.Get, query);
        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "получить группу пользователей приложения из Authentik");

        using var document = JsonDocument.Parse(body);
        foreach (var group in document.RootElement.GetProperty("results").EnumerateArray())
        {
            var name = ReadString(group, "name");
            if (string.Equals(name, _applicationUsersGroupName, StringComparison.Ordinal))
            {
                return ReadString(group, "pk");
            }
        }

        throw new InvalidOperationException($"Группа пользователей приложения '{_applicationUsersGroupName}' не найдена в Authentik.");
    }

    private async Task<IReadOnlyList<AuthentikUserRecord>> ReadPagedUsersAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        var users = new List<AuthentikUserRecord>();
        var nextUri = requestUri;

        while (!string.IsNullOrWhiteSpace(nextUri))
        {
            using var request = CreateRequest(HttpMethod.Get, nextUri);
            using var response = await SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(response, body, "получить пользователей из Authentik");

            using var document = JsonDocument.Parse(body);
            foreach (var user in document.RootElement.GetProperty("results").EnumerateArray())
            {
                var record = ReadUser(user);
                if (string.Equals(record.Type, "internal", StringComparison.OrdinalIgnoreCase))
                {
                    users.Add(record);
                }
            }

            var nextPage = document.RootElement.GetProperty("pagination").TryGetProperty("next", out var nextElement)
                ? nextElement.GetInt32()
                : 0;
            nextUri = nextPage > 0 ? ReplacePage(nextUri, nextPage) : string.Empty;
        }

        return users;
    }

    private HttpRequestMessage CreateJsonRequest(HttpMethod method, string requestUri, object payload)
    {
        var request = CreateRequest(method, requestUri);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, UserMiniAppJsonOptions.Default),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
    {
        EnsureConfigured();

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            throw new InvalidOperationException("Не настроен AUTHENTIK_API_TOKEN или AuthentikAuth:ApiToken.");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Не удалось {operation}. Authentik вернул {(int)response.StatusCode}: {body}");
    }

    private static AuthentikUserRecord ReadUser(JsonElement user)
    {
        return new AuthentikUserRecord(
            ReadInt(user, "pk"),
            ReadString(user, "username"),
            ReadString(user, "name"),
            ReadString(user, "uid"),
            ReadString(user, "uuid"),
            ReadBool(user, "is_active"),
            ReadString(user, "email"),
            ReadString(user, "type"));
    }

    private static string ReplacePage(string uri, int page)
    {
        var separator = uri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return uri.Contains("page=", StringComparison.Ordinal)
            ? System.Text.RegularExpressions.Regex.Replace(uri, @"([?&]page=)\d+", "${1}" + page)
            : uri + separator + "page=" + page;
    }

    private static string GenerateCode(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = GeneratedCodeAlphabet[RandomNumberGenerator.GetInt32(GeneratedCodeAlphabet.Length)];
        }

        return new string(chars);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
    }

    private static int ReadConfiguredInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}

internal sealed record AuthentikUserRecord(
    int Pk,
    string Username,
    string Name,
    string Uid,
    string Uuid,
    bool IsActive,
    string Email,
    string Type);
