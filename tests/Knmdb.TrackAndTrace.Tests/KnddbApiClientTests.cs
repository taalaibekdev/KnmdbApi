using System.Net;
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Enums;
using Knmdb.TrackAndTrace.Models.Requests;
using Knmdb.TrackAndTrace.Tests.Infrastructure;

namespace Knmdb.TrackAndTrace.Tests;

/// <summary>
/// Проверяет работу клиента SDK: вход, подстановку токена, смену пользователя
/// и преобразование ошибок.
/// </summary>
public sealed class KnddbApiClientTests
{
    private const string BaseAddress = "https://testndbapi.med.kg/";

    private static (KnddbApiClient Client, RecordingHttpMessageHandler Handler, HttpClient Http) CreateClient(
        Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        var handler = new RecordingHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };

        var client = new KnddbApiClient(http, new KnddbClientOptions
        {
            Environment = KnddbEnvironment.Test,
        });

        return (client, handler, http);
    }

    private static HttpResponseMessage TokenResponse(string accessToken, string refreshToken)
        => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, $$"""
        {
          "access_token": "{{accessToken}}",
          "token_type": "Bearer",
          "expires_in": 3600,
          "scope": "api offline_access",
          "refresh_token": "{{refreshToken}}"
        }
        """);

    [Fact]
    public void Контуры_указывают_на_правильные_адреса()
    {
        Assert.Equal(
            "https://testndbapi.med.kg/",
            KnddbClientOptions.GetEnvironmentAddress(KnddbEnvironment.Test).AbsoluteUri);

        Assert.Equal(
            "https://ndbapi.med.kg/",
            KnddbClientOptions.GetEnvironmentAddress(KnddbEnvironment.Production).AbsoluteUri);

        var test = new KnddbClientOptions();
        Assert.Equal(KnddbEnvironment.Test, test.Environment);
        Assert.Equal(KnddbEnvironment.Test, test.ResolvedEnvironment);
        Assert.False(test.EffectiveBaseAddress.Host.Contains("ndbapi.med.kg", StringComparison.Ordinal) && test.EffectiveBaseAddress.Host.StartsWith("ndbapi", StringComparison.Ordinal));

        var production = new KnddbClientOptions { Environment = KnddbEnvironment.Production };
        Assert.Equal(KnddbEnvironment.Production, production.ResolvedEnvironment);
        Assert.Equal("ndbapi.med.kg", production.EffectiveBaseAddress.Host);
    }

    [Fact]
    public async Task Вход_отправляет_форму_grant_type_password()
    {
        var (client, handler, http) = CreateClient((_, _) => TokenResponse("token-a", "refresh-a"));

        using (http)
        {
            await client.SignInAsync("pharmacist", "secret");

            var request = handler.Requests[0];

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/connect/token", request.Path);
            Assert.NotNull(request.Body);
            Assert.Contains("grant_type=password", request.Body, StringComparison.Ordinal);
            Assert.Contains("username=pharmacist", request.Body, StringComparison.Ordinal);
            Assert.Contains("password=secret", request.Body, StringComparison.Ordinal);
            Assert.Contains("application/x-www-form-urlencoded", request.ContentType, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Защищённый_метод_получает_заголовок_Authorization()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "refresh-a"),
            _ => RecordingHttpMessageHandler.Envelope(
                """{ "numberOfStakeholders": 1, "stakeholders": [ { "code": 1, "name": "A", "type": 5 } ] }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            var response = await client.GetAllStakeholdersAsync();

            Assert.NotNull(response);
            Assert.Equal(1, response!.NumberOfStakeholders);
            Assert.Equal(StakeholderType.Pharmacy, response.Stakeholders![0].Type);

            var apiRequest = handler.Requests[1];
            Assert.Equal(HttpMethod.Get, apiRequest.Method);
            Assert.Equal("/api/TrackAndTrace/GetAllStakeholders", apiRequest.Path);
            Assert.Equal("Bearer token-a", apiRequest.Authorization);
        }
    }

    [Fact]
    public async Task Токен_запрашивается_один_раз_на_несколько_запросов()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "refresh-a"),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            await client.GetAllStakeholdersAsync();
            await client.GetMedicineListAsync();

            Assert.Equal(3, handler.Requests.Count);
            Assert.Single(handler.Requests, r => r.Path == "/connect/token");
        }
    }

    [Fact]
    public async Task Два_пользователя_работают_со_своими_токенами()
    {
        var tokenCounter = 0;

        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse($"token-{++tokenCounter}", $"refresh-{tokenCounter}"),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        using (http)
        {
            var first = await client.SignInAsync("user-a", "pwd-a", sessionKey: "user:a");
            var second = await client.SignInAsync("user-b", "pwd-b", sessionKey: "user:b");

            Assert.Equal("token-1", first.AccessToken);
            Assert.Equal("token-2", second.AccessToken);

            client.UseSession("user:a");
            await client.GetAllStakeholdersAsync();
            Assert.Equal("Bearer token-1", handler.Requests[^1].Authorization);

            client.UseSession("user:b");
            await client.GetAllStakeholdersAsync();
            Assert.Equal("Bearer token-2", handler.Requests[^1].Authorization);

            // Смена пользователя не приводит к повторному запросу токена.
            Assert.Equal(2, handler.Requests.Count(r => r.Path == "/connect/token"));
        }
    }

    [Fact]
    public async Task Область_пользователя_временно_переключает_токен()
    {
        var tokenCounter = 0;

        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse($"token-{++tokenCounter}", $"refresh-{tokenCounter}"),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        using (http)
        {
            var defaultSession = await client.SignInAsync("user-default", "pwd");
            var otherSession = await client.SignInAsync("user-other", "pwd", sessionKey: "user:other");
            client.UseSession("default");

            await using (client.BeginUserScope("user:other"))
            {
                await client.GetAllStakeholdersAsync();
                Assert.Equal($"Bearer {otherSession.AccessToken}", handler.Requests[^1].Authorization);
            }

            // После выхода из области снова действует пользователь по умолчанию.
            await client.GetAllStakeholdersAsync();
            Assert.Equal($"Bearer {defaultSession.AccessToken}", handler.Requests[^1].Authorization);
        }
    }

    [Fact]
    public async Task Анонимный_метод_выполняется_без_токена()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/api/TrackAndTrace/ProductInquiryQRCode" => RecordingHttpMessageHandler.Envelope(
                """{ "productBoxId": 1, "gtin": "12345678901234", "isAvailableForSale": true, "productStatus": 1, "productState": 3 }"""),
            _ => RecordingHttpMessageHandler.Empty(HttpStatusCode.InternalServerError),
        });

        using (http)
        {
            var result = await client.ProductInquiryByQrCodeAsync("01...");

            Assert.NotNull(result);
            Assert.True(result!.IsAvailableForSale);
            Assert.Equal(ProductState.Sales, result.ProductState);
            Assert.Single(handler.Requests);
            Assert.Null(handler.Requests[0].Authorization);
        }
    }

    [Fact]
    public async Task Метод_с_параметром_в_строке_запроса_формирует_правильный_адрес()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "r"),
            _ => RecordingHttpMessageHandler.Envelope("""{ "declarationId": 99 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            await client.GetTransferDeclarationAsync(99);

            var request = handler.Requests[^1];
            Assert.Equal("/api/TrackAndTrace/GetTransferDeclaration", request.Path);
            Assert.Equal("?declarationId=99", request.Query);
        }
    }

    [Fact]
    public async Task Ошибка_403_преобразуется_в_KnddbApiException_с_подсказкой_о_роли()
    {
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "r"),
            _ => RecordingHttpMessageHandler.Json(
                HttpStatusCode.Forbidden,
                """{ "type": "about:blank", "title": "Forbidden", "status": 403, "detail": "Роль не назначена." }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var exception = await Assert.ThrowsAsync<KnddbApiException>(() => client.GetAllStakeholdersAsync());

            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
            Assert.True(exception.IsForbidden);
            Assert.Contains("роли", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Роль не назначена.", exception.Message, StringComparison.Ordinal);
            Assert.Equal(403, exception.ProblemDetails!.Status);
        }
    }

    [Fact]
    public async Task Ошибка_404_в_проверке_лекарства_распознаётся_как_IsNotFound()
    {
        var (client, _, http) = CreateClient((_, _) => RecordingHttpMessageHandler.Json(
            HttpStatusCode.NotFound,
            """{ "type": "about:blank", "title": "Not Found", "status": 404 }"""));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<KnddbApiException>(
                () => client.ProductInquiryByQrCodeAsync("unknown"));

            Assert.True(exception.IsNotFound);
            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        }
    }

    [Fact]
    public async Task Ошибки_валидации_извлекаются_в_словарь_Errors()
    {
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "r"),
            _ => RecordingHttpMessageHandler.Json(
                HttpStatusCode.BadRequest,
                """
                {
                  "type": "about:blank",
                  "title": "One or more validation errors occurred.",
                  "status": 400,
                  "errors": { "qrCodes": ["Поле обязательно"], "gtin": ["Неверная длина"] }
                }
                """),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var exception = await Assert.ThrowsAsync<KnddbApiException>(() => client.GetMedicineListAsync());

            Assert.NotNull(exception.Errors);
            Assert.Equal("Поле обязательно", exception.Errors!["qrCodes"]);
            Assert.Equal("Неверная длина", exception.Errors["gtin"]);
        }
    }

    [Fact]
    public async Task Ответ_401_приводит_к_обновлению_токена_и_повтору_запроса()
    {
        var apiAttempts = 0;

        var (client, handler, http) = CreateClient((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect/token")
            {
                return TokenResponse("refreshed-token", "refresh-2");
            }

            apiAttempts++;
            return apiAttempts == 1
                ? RecordingHttpMessageHandler.Empty(HttpStatusCode.Unauthorized)
                : RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 7 }""");
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            var response = await client.GetAllStakeholdersAsync();

            Assert.NotNull(response);
            Assert.Equal(7, response!.NumberOfStakeholders);
            Assert.Equal(2, apiAttempts);

            var apiRequests = handler.Requests.Where(r => r.Path.StartsWith("/api/", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, apiRequests.Length);
            Assert.Equal("Bearer refreshed-token", apiRequests[1].Authorization);
        }
    }

    [Fact]
    public async Task Неверный_логин_или_пароль_вызывает_KnddbAuthenticationException()
    {
        var (client, _, http) = CreateClient((_, _) => RecordingHttpMessageHandler.Json(
            HttpStatusCode.BadRequest,
            """{ "error": "invalid_grant", "error_description": "The username/password couple is invalid." }"""));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<KnddbAuthenticationException>(
                () => client.SignInAsync("user", "wrong-password"));

            Assert.Equal("invalid_grant", exception.OAuthError);
            Assert.Contains("invalid_grant", exception.Message, StringComparison.Ordinal);
            Assert.False(client.IsAuthenticated);
        }
    }

    [Fact]
    public async Task Без_входа_защищённый_метод_сообщает_об_отсутствии_учётных_данных()
    {
        var (client, _, http) = CreateClient((_, _) => RecordingHttpMessageHandler.Empty(HttpStatusCode.OK));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<KnddbAuthenticationException>(
                () => client.GetAllStakeholdersAsync());

            Assert.Contains("учётные данные", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Выход_очищает_сессию_даже_если_сервер_вернул_ошибку()
    {
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "r"),
            "/connect/logout" => RecordingHttpMessageHandler.Empty(HttpStatusCode.InternalServerError),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            Assert.True(client.IsAuthenticated);

            await client.SignOutAsync();

            Assert.False(client.IsAuthenticated);
            Assert.Null(client.CurrentSession);
            Assert.Null(client.FindSession(KnddbSession.DefaultKey));
        }
    }

    [Fact]
    public async Task Деактивация_передаёт_тип_списания_числом()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse("token-a", "r"),
            _ => RecordingHttpMessageHandler.Envelope("""{ "declarationId": 55 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var response = await client.DeactivateDeclarationAsync(new DeactivateDeclarationRequest
            {
                ConsumptionType = ConsumptionType.DisposalForDeadline,
                Description = "Истёк срок годности",
                Details = [new DeactivateDeclarationDetail { QrCode = "01..." }],
            });

            Assert.NotNull(response);
            Assert.Equal(55, response!.DeclarationId);

            using var body = handler.Requests[^1].ParseBody();
            Assert.Equal((int)ConsumptionType.DisposalForDeadline, body.RootElement.GetProperty("consumptionType").GetInt32());
            Assert.Equal("Истёк срок годности", body.RootElement.GetProperty("description").GetString());
        }
    }
}
