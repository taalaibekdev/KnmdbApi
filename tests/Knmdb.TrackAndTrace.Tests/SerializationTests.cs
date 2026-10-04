using System.Text.Json;
using Knmdb.TrackAndTrace.Models.Enums;
using Knmdb.TrackAndTrace.Models.Requests;
using Knmdb.TrackAndTrace.Models.Responses;
using Knmdb.TrackAndTrace.Serialization;

namespace Knmdb.TrackAndTrace.Tests;

/// <summary>
/// Проверяет соответствие JSON-контрактов SDK фактическому формату API KNMDB.
/// </summary>
public sealed class SerializationTests
{
    private static readonly JsonSerializerOptions Options = KnddbJson.DefaultOptions;

    [Fact]
    public void Enum_сериализуется_числом_как_на_сервере()
    {
        var json = JsonSerializer.Serialize(StakeholderType.Pharmacy, Options);

        Assert.Equal("5", json);
    }

    [Fact]
    public void Enum_десериализуется_из_числа()
    {
        var value = JsonSerializer.Deserialize<StakeholderType>("5", Options);

        Assert.Equal(StakeholderType.Pharmacy, value);
    }

    [Theory]
    [InlineData("\"MedicalOrganization\"", StakeholderType.MedicalOrganization)]
    [InlineData("\"medicalOrganization\"", StakeholderType.MedicalOrganization)]
    [InlineData("\"medical_organization\"", StakeholderType.MedicalOrganization)]
    public void Enum_десериализуется_из_имени_члена(string json, StakeholderType expected)
    {
        var value = JsonSerializer.Deserialize<StakeholderType>(json, Options);

        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("\"Label And TraceMandatory\"", TrackAndTraceStatus.LabelAndTraceMandatory)]
    [InlineData("\"Not Required\"", TrackAndTraceStatus.NotTracked)]
    [InlineData("\"DISPOSAL FOR DEADLINE\"", ConsumptionType.DisposalForDeadline)]
    [InlineData("\"SYSTEM IN\"", ConsumptionType.SystemIn)]
    [InlineData("\"Transfer Initiated\"", ProductState.TransferInitiated)]
    public void Enum_десериализуется_из_человекочитаемого_названия(string json, object expected)
    {
        var actual = expected switch
        {
            TrackAndTraceStatus status => (object)JsonSerializer.Deserialize<TrackAndTraceStatus>(json, Options)!,
            ConsumptionType consumption => JsonSerializer.Deserialize<ConsumptionType>(json, Options)!,
            _ => JsonSerializer.Deserialize<ProductState>(json, Options)!,
        };

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"TransferInitiated\"", ProductState.TransferInitiated)]
    [InlineData("\"PartialSalesCancelled\"", ProductState.PartialSalesCancelled)]
    [InlineData("\"DeactivationCancelled\"", ProductState.DeactivationCancelled)]
    [InlineData("3", ProductState.Sales)]
    [InlineData("18", ProductState.DeactivationCancelled)]
    public void Enum_десериализуется_из_имени_и_из_числа(string json, ProductState expected)
    {
        var value = JsonSerializer.Deserialize<ProductState>(json, Options);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void Enum_значения_совпадают_с_серверными()
    {
        Assert.Equal(0, (int)ConsumptionType.SystemIn);
        Assert.Equal(10, (int)ConsumptionType.SystemOut);
        Assert.Equal(60, (int)ConsumptionType.Consumption);
        Assert.Equal(100, (int)ConsumptionType.Sample);

        Assert.Equal(1, (int)TrackAndTraceStatus.NotTracked);
        Assert.Equal(2, (int)TrackAndTraceStatus.LabelMandatory);
        Assert.Equal(3, (int)TrackAndTraceStatus.LabelAndTraceMandatory);

        Assert.Equal(1, (int)StakeholderType.Producer);
        Assert.Equal(5, (int)StakeholderType.Pharmacy);
        Assert.Equal(7, (int)StakeholderType.Warehouse);

        Assert.Equal(1, (int)ProductState.Production);
        Assert.Equal(15, (int)ProductState.ReturnTransferAccepted);
        Assert.Equal(18, (int)ProductState.DeactivationCancelled);

        Assert.Equal(3, (int)ProductStatus.InTransfer);
        Assert.Equal(1, (int)TransferState.Initiated);
        Assert.Equal(2, (int)TransferType.Accept);
    }

    [Fact]
    public void Даты_в_запросе_импорта_пишутся_в_формате_yyyy_MM_dd()
    {
        var request = new ImportDeclarationRequest
        {
            Gtin = "12345678901234",
            BatchNo = "B-1",
            ExpirationDate = new DateTimeOffset(2027, 5, 1, 10, 30, 0, TimeSpan.FromHours(6)),
            Price = 12.5m,
            ProductionDate = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero),
            DocumentDate = new DateTimeOffset(2025, 5, 2, 0, 0, 0, TimeSpan.Zero),
            DocumentNo = "DOC-1",
            QrTypeId = 1,
            QrCodes = ["01...21...17...10..."],
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, Options));
        var root = document.RootElement;

        Assert.Equal("2027-05-01", root.GetProperty("expirationDate").GetString());
        Assert.Equal("2025-05-01", root.GetProperty("productionDate").GetString());
        Assert.Equal("2025-05-02", root.GetProperty("documentDate").GetString());
        Assert.Equal("12345678901234", root.GetProperty("gtin").GetString());
        Assert.Equal(1, root.GetProperty("qrTypeId").GetInt32());
        Assert.Equal(12.5m, root.GetProperty("price").GetDecimal());
    }

    [Fact]
    public void Необязательные_даты_фильтра_пишутся_как_null()
    {
        var request = new GetTransferListByFilterRequest { DeclarationId = 42 };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, Options));
        var root = document.RootElement;

        Assert.Equal(42, root.GetProperty("declarationId").GetInt64());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("documentDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("declarationDateFrom").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("currentState").ValueKind);
    }

    [Fact]
    public void Фильтр_перемещений_передаёт_enum_числом()
    {
        var request = new GetTransferListByFilterRequest
        {
            CurrentState = TransferState.Accepted,
            TransferType = TransferType.Accept,
            IsReturn = true,
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, Options));
        var root = document.RootElement;

        Assert.Equal((int)TransferState.Accepted, root.GetProperty("currentState").GetInt32());
        Assert.Equal((int)TransferType.Accept, root.GetProperty("transferType").GetInt32());
        Assert.True(root.GetProperty("isReturn").GetBoolean());
    }

    [Fact]
    public void Ответ_GetAllStakeholders_разбирается_корректно()
    {
        const string json = """
        {
          "numberOfStakeholders": 2,
          "stakeholders": [
            {
              "code": 100,
              "name": "ОсОО «Фарма»",
              "type": 5,
              "taxNumber": "0123456789",
              "address": "ул. Киевская, 1",
              "city": "Бишкек",
              "district": "Первомайский",
              "parentCode": 5,
              "parentName": "Головная организация"
            },
            {
              "code": 200,
              "name": "Склад №1",
              "type": 7,
              "city": "Ош",
              "parentCode": null,
              "parentName": null
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<GetAllStakeholdersResponse>(json, Options);

        Assert.NotNull(response);
        Assert.Equal(2, response!.NumberOfStakeholders);
        Assert.NotNull(response.Stakeholders);
        Assert.Equal(2, response.Stakeholders!.Count);
        Assert.Equal(100, response.Stakeholders[0].Code);
        Assert.Equal(StakeholderType.Pharmacy, response.Stakeholders[0].Type);
        Assert.Equal("Бишкек", response.Stakeholders[0].City);
        Assert.Equal(5, response.Stakeholders[0].ParentCode);
        Assert.Equal(StakeholderType.Warehouse, response.Stakeholders[1].Type);
        Assert.Null(response.Stakeholders[1].ParentCode);
    }

    [Fact]
    public void Ответ_проверки_лекарства_разбирается_включая_поля_вне_OpenAPI_схемы()
    {
        const string json = """
        {
          "productBoxId": 777,
          "productPackageItemId": "0f8fad5b-d9cb-469f-a165-70867728950e",
          "productName": "Парацетамол",
          "gtin": "04600000000017",
          "serialNumber": "SN0000000000000001",
          "batchNumber": "B-2025-01",
          "productionDate": "2025-01-01T00:00:00+06:00",
          "expirationDate": "2027-01-01T00:00:00+06:00",
          "qrCode": "010460000000001721SN00000000000000011727010110B-2025-01",
          "stakeHolderName": "Аптека №5",
          "stakeholderTaxNumber": "0123456789",
          "isExpired": false,
          "isAvailableForSale": true,
          "isSuspendedOrRecalled": false,
          "productStatus": 1,
          "productState": 10,
          "suspendRecallInfo": null,
          "productInquiryHistory": [
            {
              "declarationNumber": 9001,
              "stakeHolder": "Импортёр ОсОО",
              "state": 2,
              "stateDate": "2025-02-01T09:15:00+06:00",
              "price": 100.50,
              "partialSaleAmount": null
            }
          ],
          "overallRetailPrice": "150.00",
          "certificateNumber": "KG-12345",
          "instructionForUse": "Инструкция",
          "instructionForUseDocId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "packagingImageDocId": null,
          "isFomsDrug": true,
          "compensation": "50%",
          "consumptionType": null,
          "manufacturerName": "Завод «Фарм»",
          "partialSaleRemainingAmount": null
        }
        """;

        var result = JsonSerializer.Deserialize<ProductInquiryResult>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(777, result!.ProductBoxId);
        Assert.Equal("Аптека №5", result.StakeHolderName);
        Assert.Equal("0123456789", result.StakeholderTaxNumber);
        Assert.True(result.IsAvailableForSale);
        Assert.False(result.IsExpired);
        Assert.Equal(ProductStatus.InStock, result.ProductStatus);
        Assert.Equal(ProductState.TransferAccepted, result.ProductState);
        Assert.Null(result.ConsumptionType);
        Assert.NotNull(result.ProductInquiryHistory);
        Assert.Single(result.ProductInquiryHistory!);
        Assert.Equal(ProductState.Import, result.ProductInquiryHistory![0].State);
        Assert.Equal(9001, result.ProductInquiryHistory[0].DeclarationNumber);
        Assert.Equal(100.50m, result.ProductInquiryHistory[0].Price);
        Assert.Null(result.ProductInquiryHistory[0].PartialSaleAmount);
        Assert.Equal(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), result.InstructionForUseDocId);
    }

    [Fact]
    public void Ответ_списка_препаратов_разбирается_и_содержит_статус_маркировки()
    {
        const string json = """
        {
          "numberOfMedicines": 1,
          "medicineList": [
            {
              "gtin": "04600000000017",
              "brandName": "Парацетамол",
              "fullBrandName": "Парацетамол, таблетки 500 мг №20",
              "manufacturerCompany": "Завод «Фарм»",
              "country": "Кыргызстан",
              "atcCode": "N02BE01",
              "formula": "таблетки",
              "innList": "Парацетамол",
              "trackAndTraceStatus": 3,
              "lastUpdate": "2026-02-21T10:30:00+06:00"
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<GetMedicineListResponse>(json, Options);

        Assert.NotNull(response);
        Assert.Equal(1, response!.NumberOfMedicines);
        Assert.Equal(TrackAndTraceStatus.LabelAndTraceMandatory, response.MedicineList![0].TrackAndTraceStatus);
        Assert.Equal("N02BE01", response.MedicineList[0].AtcCode);
    }

    [Fact]
    public void Ответ_отмены_складской_декларации_разбирается()
    {
        const string json = """
        { "declarationId": 15, "isSuccess": false, "message": "Декларация уже отменена" }
        """;

        var response = JsonSerializer.Deserialize<StockDeclarationCancelResponse>(json, Options);

        Assert.NotNull(response);
        Assert.Equal(15, response!.DeclarationId);
        Assert.False(response.IsSuccess);
        Assert.Equal("Декларация уже отменена", response.Message);
    }

    [Fact]
    public void Ответ_токена_разбирается_и_рассчитывает_срок_действия()
    {
        const string json = """
        {
          "access_token": "eyJhbGciOiJSUzI1NiJ9.payload.signature",
          "token_type": "Bearer",
          "expires_in": 3600,
          "scope": "api offline_access",
          "refresh_token": "refresh-value"
        }
        """;

        var token = JsonSerializer.Deserialize<Models.Auth.TokenResponse>(json, Options);

        Assert.NotNull(token);
        Assert.True(token!.HasAccessToken);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(3600, token.ExpiresIn);
        Assert.Equal("refresh-value", token.RefreshToken);
        Assert.Equal(["api", "offline_access"], token.GetScopes());

        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(now.AddHours(1), token.GetExpiresAt(now));
    }

    [Fact]
    public void ProblemDetails_разбирается_вместе_с_нестандартными_полями()
    {
        const string json = """
        {
          "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
          "title": "One or more validation errors occurred.",
          "status": 400,
          "detail": "Поле qrCodes не может быть пустым.",
          "instance": "/api/TrackAndTrace/ImportDeclaration",
          "traceId": "00-8f1a-01",
          "errors": { "qrCodes": ["Поле обязательно"] }
        }
        """;

        var problem = JsonSerializer.Deserialize<Models.Api.ApiProblemDetails>(json, Options);

        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Поле qrCodes не может быть пустым.", problem.Detail);
        Assert.Equal("Поле qrCodes не может быть пустым.", problem.GetMessage());
        Assert.NotNull(problem.Extensions);
        Assert.True(problem.Extensions!.ContainsKey("errors"));
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public void Числа_в_виде_строк_читаются_корректно()
    {
        const string json = """
        { "declarationId": "12345", "declarationDate": "2026-01-15T10:00:00+06:00" }
        """;

        var response = JsonSerializer.Deserialize<StockDeclarationResponse>(json, Options);

        Assert.NotNull(response);
        Assert.Equal(12345, response!.DeclarationId);
    }

    [Fact]
    public void Неизвестное_строковое_значение_enum_вызывает_понятную_ошибку()
    {
        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ProductStatus>("\"UnknownStatus\"", Options));

        Assert.Contains("UnknownStatus", exception.Message, StringComparison.Ordinal);
    }
}
