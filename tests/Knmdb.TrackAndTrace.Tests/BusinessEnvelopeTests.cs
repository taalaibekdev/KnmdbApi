using System.Net;
using System.Net.Http;
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Enums;
using Knmdb.TrackAndTrace.Models.Requests;
using Knmdb.TrackAndTrace.Models.Responses;
using Knmdb.TrackAndTrace.Serialization;
using Knmdb.TrackAndTrace.Tests.Infrastructure;

namespace Knmdb.TrackAndTrace.Tests;

/// <summary>
/// Проверяет поведение SDK при работе с реальным сервером KNMDB: разбор конверта
/// <c>{ resultCode, resultMessage, actionResult }</c>, ошибки бизнес-логики
/// с HTTP-статусом 200, обязательный заголовок User-Agent и формат дат.
/// </summary>
/// <remarks>
/// Сценарии основаны на результатах тестирования SDK на боевом контуре
/// с тестовыми организациями (Тест_склад → тест-аптека).
/// </remarks>
public sealed class BusinessEnvelopeTests
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

    private static HttpResponseMessage TokenResponse(string accessToken = "token-a")
        => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, $$"""
        {
          "access_token": "{{accessToken}}",
          "token_type": "Bearer",
          "expires_in": 3600,
          "refresh_token": "refresh-a"
        }
        """);

    // ---------------------------------------------------------------------
    // Конверт ответа
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Успешный_ответ_в_конверте_разбирается_из_actionResult()
    {
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""
            {
              "numberOfStakeholders": 2,
              "stakeholders": [
                { "code": 100, "name": "Тест_склад", "type": 7 },
                { "code": 205, "name": "тест аптека", "type": 5 }
              ]
            }
            """),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            var response = await client.GetAllStakeholdersAsync();

            Assert.NotNull(response);
            Assert.Equal(2, response!.NumberOfStakeholders);
            Assert.Equal(StakeholderType.Warehouse, response.Stakeholders![0].Type);
            Assert.Equal(StakeholderType.Pharmacy, response.Stakeholders[1].Type);
        }
    }

    [Fact]
    public async Task Конверт_с_нулевым_actionResult_возвращает_null()
    {
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("null"),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            var response = await client.GetAllStakeholdersAsync();

            Assert.Null(response);
        }
    }

    [Fact]
    public async Task Ответ_без_конверта_по_прежнему_поддерживается()
    {
        // На случай обращения к серверу, который конверт не добавляет.
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Json(
                HttpStatusCode.OK,
                """{ "numberOfStakeholders": 5, "stakeholders": [] }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            var response = await client.GetAllStakeholdersAsync();

            Assert.NotNull(response);
            Assert.Equal(5, response!.NumberOfStakeholders);
        }
    }

    // ---------------------------------------------------------------------
    // Ошибки бизнес-логики с HTTP 200
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Ошибка_6022_упаковка_не_найдена_распознаётся()
    {
        const string serverMessage = "Product with QRCode 0104600000000017 not found";

        var (client, _, http) = CreateClient((_, _) =>
            RecordingHttpMessageHandler.EnvelopeError(6022, serverMessage));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<KnddbApiException>(
                () => client.ProductInquiryByQrCodeAsync("0104600000000017"));

            // Ключевое: ошибка пришла с HTTP 200, по статусу её не отличить.
            Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
            Assert.Equal(6022, exception.ResultCode);
            Assert.Equal(serverMessage, exception.ResultMessage);
            Assert.True(exception.IsBusinessError);
            Assert.True(exception.IsProductNotFound);
            Assert.False(exception.IsProductNotSuitableForSale);

            // Русское пояснение и ответ сервера — оба в сообщении.
            Assert.Contains("не найдена", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(serverMessage, exception.Message, StringComparison.Ordinal);
            Assert.Contains("не найдена", exception.GetResultDescription()!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Ошибка_6023_повторная_продажа_распознаётся()
    {
        const string serverMessage = "Product with QRCode 0104600000000017 is not suitable for sale";

        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.EnvelopeError(6023, serverMessage),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var exception = await Assert.ThrowsAsync<KnddbApiException>(() =>
                client.SalesDeclarationAsync(new SalesDeclarationRequest
                {
                    IsPharmacyConsumption = true,
                    Details = [new SalesDeclarationDetail { QrCode = "0104600000000017", Price = 165m }],
                }));

            Assert.Equal(6023, exception.ResultCode);
            Assert.True(exception.IsBusinessError);
            Assert.True(exception.IsProductNotSuitableForSale);
            Assert.False(exception.IsProductNotFound);
            Assert.Contains("не подходит для продажи", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(serverMessage, exception.ResponseBody is null ? null : exception.ResultMessage);
        }
    }

    [Fact]
    public async Task Ошибка_кода_валидации_1_с_HTTP_200_распознаётся()
    {
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.EnvelopeError(1, "Validation error(s) occurred: \r\nПоле qrCodes обязательно"),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var exception = await Assert.ThrowsAsync<KnddbApiException>(() =>
                client.ImportDeclarationAsync(new ImportDeclarationRequest
                {
                    Gtin = "04600000000017",
                    BatchNo = "B-1",
                    ExpirationDate = DateTimeOffset.UtcNow,
                    Price = 1m,
                    ProductionDate = DateTimeOffset.UtcNow,
                    DocumentDate = DateTimeOffset.UtcNow,
                    DocumentNo = "1",
                    QrTypeId = 1,
                    QrCodes = [],
                }));

            Assert.Equal(KnddbResultCodes.ValidationError, exception.ResultCode);
            Assert.True(exception.IsBusinessError);
            Assert.Contains("проверки входных данных", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Поле qrCodes обязательно", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Ошибка_2_внутренняя_ошибка_сервера_описывается_понятно()
    {
        var (client, _, http) = CreateClient((_, _) => RecordingHttpMessageHandler.EnvelopeError(
            2,
            "Unexpected error(s) occurred: \r\nAn error occurred while saving the entity changes"));

        using (http)
        {
            // Для эндпоинта токена используется KnddbAuthenticationException —
            // он наследуется от KnddbApiException, поэтому общие обработчики работают.
            var exception = await Assert.ThrowsAsync<KnddbAuthenticationException>(
                () => client.SignInAsync("user", "pwd"));

            // Ошибка пришла в конверте с HTTP 200 на /connect/token.
            Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
            Assert.Equal(KnddbResultCodes.UnexpectedError, exception.ResultCode);
            Assert.True(exception.IsBusinessError);

            // Сообщение объясняет суть и подсказывает вероятную причину.
            Assert.Contains("Внутренняя ошибка сервера", exception.Message, StringComparison.Ordinal);
            Assert.Contains("User-Agent", exception.Message, StringComparison.Ordinal);
            Assert.Contains(
                "An error occurred while saving the entity changes",
                exception.Message,
                StringComparison.Ordinal);

            Assert.Equal("default", exception.SessionKey);
            Assert.False(client.IsAuthenticated);
        }
    }

    [Fact]
    public async Task Неизвестный_код_результата_передаётся_как_есть()
    {
        var (client, _, http) = CreateClient((_, _) => RecordingHttpMessageHandler.EnvelopeError(
            9999,
            "Some unknown business error"));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<KnddbApiException>(
                () => client.ProductInquiryByQrCodeAsync("01..."));

            Assert.Equal(9999, exception.ResultCode);
            Assert.True(exception.IsBusinessError);

            // Расшифровки нет — показываем сообщение сервера.
            Assert.Null(KnddbResultCodes.Describe(9999));
            Assert.Equal("Some unknown business error", exception.GetResultDescription());
            Assert.Contains("Some unknown business error", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Статус_маркировки_0_из_реальных_данных_читается()
    {
        // Проверено на тестовом контуре: из 3919 препаратов у 3676 (94 %)
        // trackAndTraceStatus равен нулю. В серверном перечислении такого
        // члена нет, поэтому без явного значения 0 препарат получал бы
        // неправильный статус.
        var (client, _, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""
            {
              "numberOfMedicines": 4,
              "medicineList": [
                { "gtin": "5944728003125", "brandName": "A", "trackAndTraceStatus": 0 },
                { "gtin": "5944728003118", "brandName": "B", "trackAndTraceStatus": 1 },
                { "gtin": "4840456000355", "brandName": "C", "trackAndTraceStatus": 2 },
                { "gtin": "4840456000356", "brandName": "D", "trackAndTraceStatus": 3 }
              ]
            }
            """),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            var response = await client.GetMedicineListAsync();

            var list = response!.MedicineList!;

            Assert.Equal(TrackAndTraceStatus.NotSpecified, list[0].TrackAndTraceStatus);
            Assert.Equal(0, (int)list[0].TrackAndTraceStatus);

            // Не путаем «статус не задан» с «маркировка не требуется».
            Assert.NotEqual(TrackAndTraceStatus.NotTracked, list[0].TrackAndTraceStatus);

            Assert.Equal(TrackAndTraceStatus.NotTracked, list[1].TrackAndTraceStatus);
            Assert.Equal(TrackAndTraceStatus.LabelMandatory, list[2].TrackAndTraceStatus);
            Assert.Equal(TrackAndTraceStatus.LabelAndTraceMandatory, list[3].TrackAndTraceStatus);
        }
    }

    [Fact]
    public void Описания_кодов_покрывают_ключевые_случаи()
    {
        Assert.Equal("Операция выполнена успешно.", KnddbResultCodes.Describe(0));
        Assert.Contains("не найдена", KnddbResultCodes.Describe(6022)!);
        Assert.Contains("не подходит для продажи", KnddbResultCodes.Describe(6023)!);
        Assert.Contains("Внутренняя ошибка", KnddbResultCodes.Describe(2)!);
        Assert.Contains("повторяющиеся QR-коды", KnddbResultCodes.Describe(6003)!);
        Assert.Contains("деактивировать", KnddbResultCodes.Describe(6045)!);
        Assert.Contains("Рецепт не найден", KnddbResultCodes.Describe(6100)!);
        Assert.Null(KnddbResultCodes.Describe(null));
        Assert.Null(KnddbResultCodes.Describe(123456));
    }

    [Fact]
    public async Task ToString_ошибки_содержит_код_результата_и_описание()
    {
        var (client, _, http) = CreateClient((_, _) =>
            RecordingHttpMessageHandler.EnvelopeError(6022, "Product with QRCode X not found"));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<KnddbApiException>(
                () => client.ProductInquiryByQrCodeAsync("X"));

            var text = exception.ToString();

            Assert.Contains("resultCode: 6022", text, StringComparison.Ordinal);
            Assert.Contains("не найдена", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------------
    // User-Agent
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Заголовок_UserAgent_отправляется_всегда()
    {
        // Регрессия: без User-Agent сервер KNMDB не может сохранить запись
        // о входе и отвечает кодом результата 2.
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            await client.GetAllStakeholdersAsync();

            Assert.All(handler.Requests, request =>
            {
                Assert.NotNull(request.UserAgent);
                Assert.Contains("Knmdb.TrackAndTrace.SDK", request.UserAgent!, StringComparison.Ordinal);
            });

            // Проверяем именно запрос токена: с него начинается работа.
            Assert.NotNull(handler.Requests[0].UserAgent);
        }
    }

    [Fact]
    public async Task Приложение_может_задать_свой_UserAgent()
    {
        var handler = new RecordingHttpMessageHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var client = new KnddbApiClient(http, new KnddbClientOptions
        {
            Environment = KnddbEnvironment.Test,
            UserAgent = "MyPharmacyApp/2.1",
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            await client.GetAllStakeholdersAsync();

            Assert.All(handler.Requests, request =>
                Assert.Equal("MyPharmacyApp/2.1", request.UserAgent));
        }
    }

    // ---------------------------------------------------------------------
    // Область доступа (scope)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Запрос_токена_не_содержит_offline_access()
    {
        // Регрессия, найденная на живом сервере: сервер регистрирует только
        // область "api" (options.RegisterScopes("api")). Область offline_access
        // в OpenIddict разрешена лишь при включённом потоке обновления токена,
        // которого на сервере KNMDB нет, поэтому запрос с ней отклоняется
        // целиком: invalid_request — «The 'offline_access' scope is not allowed».
        // Вход после этого невозможен полностью.
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");
            await client.GetAllStakeholdersAsync();

            var tokenRequest = handler.Requests.First(r => r.RequestUri!.AbsolutePath == "/connect/token");
            var form = ParseForm(tokenRequest.Body!);

            Assert.Equal("api", form["scope"]);
            Assert.DoesNotContain("offline_access", tokenRequest.Body!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Область_доступа_настраивается()
    {
        var handler = new RecordingHttpMessageHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var client = new KnddbApiClient(http, new KnddbClientOptions
        {
            Environment = KnddbEnvironment.Test,
            Scope = "api custom",
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var tokenRequest = handler.Requests.First(r => r.RequestUri!.AbsolutePath == "/connect/token");
            Assert.Equal("api custom", ParseForm(tokenRequest.Body!)["scope"]);
        }
    }

    [Fact]
    public async Task Пустая_область_доступа_не_передаётся()
    {
        var handler = new RecordingHttpMessageHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "numberOfStakeholders": 0 }"""),
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var client = new KnddbApiClient(http, new KnddbClientOptions
        {
            Environment = KnddbEnvironment.Test,
            Scope = string.Empty,
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            var tokenRequest = handler.Requests.First(r => r.RequestUri!.AbsolutePath == "/connect/token");
            Assert.False(ParseForm(tokenRequest.Body!).ContainsKey("scope"));
        }
    }

    private static Dictionary<string, string> ParseForm(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString(pair[..separator]).Replace("+", " ", StringComparison.Ordinal);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]).Replace("+", " ", StringComparison.Ordinal);

            result[key] = value;
        }

        return result;
    }

    // ---------------------------------------------------------------------
    // Формат дат
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Даты_запросов_уходят_без_времени_и_без_суффикса_Z()
    {
        // Регрессия: сервер KNMDB не принимает дату вида «2026-03-15T12:00:00Z»
        // и отвечает кодом результата 2.
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope(
                """{ "declarationId": 1, "declarationDate": "2026-03-15T00:00:00Z" }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            await client.TransferDeclarationAsync(new TransferDeclarationRequest
            {
                FromStakeholder = 100,
                ToStakeholder = 205,
                DeclarationDate = DateTimeOffset.UtcNow,
                DocumentDate = DateTimeOffset.UtcNow,
                Details = [new TransferDeclarationDetail { QrCode = "01...", Price = 140m }],
            });

            using var body = handler.Requests[^1].ParseBody();
            var root = body.RootElement;

            var declarationDate = root.GetProperty("declarationDate").GetString();
            var documentDate = root.GetProperty("documentDate").GetString();

            Assert.NotNull(declarationDate);
            Assert.NotNull(documentDate);

            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", declarationDate!);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", documentDate!);
            Assert.DoesNotContain("T", declarationDate, StringComparison.Ordinal);
            Assert.DoesNotContain("Z", declarationDate, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Дата_с_явным_временем_всё_равно_отправляется_без_времени()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "declarationId": 1 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            // Дата с ненулевым временем — сервер такие значения отклоняет,
            // поэтому SDK приводит их к yyyy-MM-dd.
            await client.StockDeclarationAsync(new StockDeclarationRequest
            {
                DeclarationDate = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero),
                StakeholderCode = 100,
            });

            using var body = handler.Requests[^1].ParseBody();

            Assert.Equal("2026-03-15", body.RootElement.GetProperty("declarationDate").GetString());
        }
    }

    [Fact]
    public async Task Дата_со_смещением_часового_пояса_не_меняет_день()
    {
        var (client, handler, http) = CreateClient((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/connect/token" => TokenResponse(),
            _ => RecordingHttpMessageHandler.Envelope("""{ "declarationId": 1 }"""),
        });

        using (http)
        {
            await client.SignInAsync("user", "pwd");

            // 15 марта 2026 года, 00:30 по Бишкеку (+06:00).
            await client.StockDeclarationAsync(new StockDeclarationRequest
            {
                DeclarationDate = new DateTimeOffset(2026, 3, 15, 0, 30, 0, TimeSpan.FromHours(6)),
                StakeholderCode = 100,
            });

            using var body = handler.Requests[^1].ParseBody();

            // В UTC это ещё 14 марта, но оператор указал 15-е — день не должен «съезжать».
            Assert.Equal("2026-03-15", body.RootElement.GetProperty("declarationDate").GetString());
        }
    }
}
