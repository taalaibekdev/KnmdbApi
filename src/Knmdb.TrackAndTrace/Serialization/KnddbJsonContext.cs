using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Knmdb.TrackAndTrace.Models.Api;
using Knmdb.TrackAndTrace.Models.Auth;
using Knmdb.TrackAndTrace.Models.Requests;
using Knmdb.TrackAndTrace.Models.Responses;

namespace Knmdb.TrackAndTrace.Serialization;

/// <summary>
/// Контекст сериализации System.Text.Json с генерацией кода во время сборки.
/// </summary>
/// <remarks>
/// <para>
/// Генерация сериализаторов на этапе компиляции даёт три практических преимущества:
/// </para>
/// <list type="bullet">
///   <item><description>совместимость с AOT-компиляцией и обрезкой кода (важно для iOS/MAUI и для запуска без JIT);</description></item>
///   <item><description>заметно более высокую скорость сериализации по сравнению с reflection-режимом;</description></item>
///   <item><description>ошибки в контрактах JSON выявляются на этапе сборки, а не в момент выполнения.</description></item>
/// </list>
/// <para>
/// Экземпляр настроенных параметров доступен через <see cref="JsonSerializerOptions"/> (см. <see cref="KnddbJson.DefaultOptions"/>).
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(ApiProblemDetails))]
[JsonSerializable(typeof(KnddbActionResultEnvelope))]
[JsonSerializable(typeof(GetAllStakeholdersResponse))]
// Устаревший контракт: оставлен для совместимости, пока метод GetSupportedQRTypesAsync не удалён из API.
[JsonSerializable(typeof(GetSupportedQRTypesResponse))]
[JsonSerializable(typeof(GetTransferDeclarationResponse))]
[JsonSerializable(typeof(GetTransferListByFilterRequest))]
[JsonSerializable(typeof(GetTransferListByFilterResponse))]
[JsonSerializable(typeof(ImportDeclarationRequest))]
[JsonSerializable(typeof(ImportDeclarationResponse))]
[JsonSerializable(typeof(ProductionDeclarationRequest))]
[JsonSerializable(typeof(ProductionDeclarationResponse))]
[JsonSerializable(typeof(TransferDeclarationRequest))]
[JsonSerializable(typeof(TransferDeclarationResponse))]
[JsonSerializable(typeof(TransferAcceptRequest))]
[JsonSerializable(typeof(TransferCancelRequest))]
[JsonSerializable(typeof(TransferReturnRequest))]
[JsonSerializable(typeof(TransferDeclarationCancelResponse))]
[JsonSerializable(typeof(StockDeclarationRequest))]
[JsonSerializable(typeof(StockDeclarationResponse))]
[JsonSerializable(typeof(StockDeclarationCancelRequest))]
[JsonSerializable(typeof(StockDeclarationCancelResponse))]
[JsonSerializable(typeof(SalesDeclarationRequest))]
[JsonSerializable(typeof(SalesDeclarationResponse))]
[JsonSerializable(typeof(SalesDeclarationCancelRequest))]
[JsonSerializable(typeof(SalesDeclarationBoxCancelRequest))]
[JsonSerializable(typeof(DeactivateDeclarationRequest))]
[JsonSerializable(typeof(DeactivateDeclarationResponse))]
[JsonSerializable(typeof(ProductInquiryWithQrCodeRequest))]
[JsonSerializable(typeof(ProductInquiryWithGtinSnRequest))]
[JsonSerializable(typeof(ProductInquiryResult))]
[JsonSerializable(typeof(GetStockInheldListResponse))]
[JsonSerializable(typeof(GetStockInheldListByGtinRequest))]
[JsonSerializable(typeof(GetStockInheldListByGtinResponse))]
[JsonSerializable(typeof(GetPartialSaleInfoRequest))]
[JsonSerializable(typeof(GetPartialSaleInfoResponse))]
[JsonSerializable(typeof(GetMedicineListRequest))]
[JsonSerializable(typeof(GetMedicineListResponse))]
public sealed partial class KnddbJsonContext : JsonSerializerContext;

/// <summary>
/// Точка доступа к параметрам сериализации JSON, используемым SDK.
/// </summary>
/// <remarks>
/// Если приложение хочет использовать собственные настройки JSON, передайте их
/// в <c>KnddbApiClient</c> напрямую или настройте через <c>AddKnddbTrackAndTrace</c>.
/// </remarks>
public static class KnddbJson
{
    private static readonly JsonSerializerOptions Options = CreateDefaultOptions();

    /// <summary>
    /// Параметры сериализации по умолчанию, совместимые с API KNMDB.
    /// </summary>
    /// <remarks>
    /// <para>Настроено следующее:</para>
    /// <list type="bullet">
    ///   <item><description>имена свойств — <c>camelCase</c> (как отдаёт сервер KNMDB);</description></item>
    ///   <item><description>перечисления — числа (формат сервера), при чтении дополнительно принимаются строковые имена
    ///   и человекочитаемые описания; см. <see cref="KnddbEnumConverter{TEnum}"/>;</description></item>
    ///   <item><description>регистр имён при чтении не учитывается — защита от расхождений в регистре;</description></item>
    ///   <item><description>числа, пришедшие строкой, читаются корректно;</description></item>
    ///   <item><description>сериализация через сгенерированный контекст <see cref="KnddbJsonContext"/>.</description></item>
    /// </list>
    /// </remarks>
    public static JsonSerializerOptions DefaultOptions => Options;

    /// <summary>
    /// Возвращает метаданные типа, соответствующие параметрам по умолчанию <see cref="DefaultOptions"/>.
    /// </summary>
    /// <typeparam name="T">Тип контракта KNMDB.</typeparam>
    /// <returns>Метаданные типа для передачи в <c>JsonSerializer</c>.</returns>
    /// <remarks>
    /// <para>
    /// Всегда используйте этот метод (а не свойства <see cref="KnddbJsonContext"/> напрямую),
    /// когда нужен доступ к метаданным типа: только так применяются конвертеры,
    /// зарегистрированные в <see cref="DefaultOptions"/> — в частности
    /// <see cref="KnddbEnumConverter{TEnum}"/> (терпимое чтение перечислений)
    /// и <see cref="ApiProblemDetailsJsonConverter"/> (расширения ProblemDetails).
    /// </para>
    /// <para>
    /// Метаданные кэшируются System.Text.Json, поэтому повторные вызовы не создают накладных расходов.
    /// </para>
    /// </remarks>
    public static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            TypeInfoResolver = KnddbJsonContext.Default,
        };

        // Перечисления KNMDB передаются числами (Newtonsoft.Json по умолчанию на сервере),
        // но SDK принимает и строковые представления — см. KnddbEnumConverter.
        options.Converters.Add(new KnddbEnumConverterFactory());
        options.Converters.Add(new ApiProblemDetailsJsonConverter());

        return options;
    }
}
