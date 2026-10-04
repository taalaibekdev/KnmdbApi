using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knmdb.TrackAndTrace.Serialization;

/// <summary>
/// Конвертер значений перечислений KNMDB.
/// </summary>
/// <typeparam name="TEnum">Тип перечисления.</typeparam>
/// <remarks>
/// <para>
/// <b>Формат записи — число.</b> Сервер KNMDB сериализует перечисления стандартными средствами
/// Newtonsoft.Json (строковый конвертер не подключён), поэтому по сети они передаются целыми
/// числами; так же они описаны и в OpenAPI-схеме (<c>"type": "integer", "format": "int32"</c>).
/// Именно поэтому SDK тоже отправляет числа — формат гарантированно совпадает с ожидаемым.
/// </para>
/// <para>
/// <b>Формат чтения — число или строка.</b> На входе принимаются:
/// </para>
/// <list type="bullet">
///   <item><description>число — штатный формат ответа KNMDB;</description></item>
///   <item><description>имя члена перечисления, например <c>"Pharmacy"</c> или <c>"TransferInitiated"</c>;</description></item>
///   <item><description>значение атрибута <see cref="DescriptionAttribute"/>, например
///   <c>"Label And TraceMandatory"</c> или <c>"Partial Sales Cancelled"</c> — так эти значения
///   указаны в человекочитаемых описаниях методов.</description></item>
/// </list>
/// <para>
/// Такая терпимость при чтении защищает приложение от изменений формата на стороне сервера
/// и позволяет читать заранее подготовленные JSON-файлы в человекочитаемом виде.
/// </para>
/// <para>
/// Значения по умолчанию <c>0</c>, не описанные в перечислении, принимаются как есть: это
/// полезно для перечислений со смещённой шкалой (например, <c>ConsumptionType</c>).
/// </para>
/// </remarks>
public sealed class KnddbEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Names = BuildNames();
    private static readonly Dictionary<string, TEnum> ReadMap = BuildReadMap();

    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var text = reader.GetString();
                if (text is null)
                {
                    throw new JsonException($"Не удалось прочитать значение перечисления {typeof(TEnum).Name}: получено null.");
                }

                if (ReadMap.TryGetValue(text, out var byName))
                {
                    return byName;
                }

                if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var byParse))
                {
                    return byParse;
                }

                throw new JsonException(
                    $"Значение '{text}' не найдено в перечислении {typeof(TEnum).Name}. " +
                    $"Допустимые значения: {string.Join(", ", Names.Values)}.");

            case JsonTokenType.Number:
                var number = reader.GetInt64();
                return (TEnum)Enum.ToObject(typeof(TEnum), number);

            case JsonTokenType.Null:
                throw new JsonException($"Перечисление {typeof(TEnum).Name} не может быть null.");

            default:
                throw new JsonException(
                    $"Неподдерживаемый токен {reader.TokenType} для перечисления {typeof(TEnum).Name}.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        // Сервер KNMDB ожидает число: см. примечание к классу.
        writer.WriteNumberValue(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Read(ref reader, typeToConvert, options);

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WritePropertyName(
            Names.TryGetValue(value, out var name)
                ? name
                : value.ToString());

    private static Dictionary<TEnum, string> BuildNames()
    {
        var values = Enum.GetValues<TEnum>();
        var result = new Dictionary<TEnum, string>(values.Length);

        foreach (var value in values)
        {
            result[value] = value.ToString();
        }

        return result;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "Перечисления SDK объявлены в этой же сборке и не обрезаются: они используются как типы свойств публичных контрактов.")]
    private static Dictionary<string, TEnum> BuildReadMap()
    {
        var values = Enum.GetValues<TEnum>();
        var result = new Dictionary<string, TEnum>(values.Length * 4, StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var name = value.ToString();
            result[name] = value;

            // Принимаем camelCase/snake_case/kebab-case на случай смены настроек сериализации.
            result[JsonNamingPolicy.CamelCase.ConvertName(name)] = value;
            result[JsonNamingPolicy.SnakeCaseLower.ConvertName(name)] = value;
            result[JsonNamingPolicy.KebabCaseLower.ConvertName(name)] = value;

            var description = GetDescription(value);
            if (!string.IsNullOrWhiteSpace(description))
            {
                result[description] = value;
            }
        }

        return result;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "Перечисления SDK объявлены в этой же сборке и не обрезаются: они используются как типы свойств публичных контрактов.")]
    private static string? GetDescription(TEnum value)
    {
        var name = value.ToString();
        var field = typeof(TEnum).GetField(name, BindingFlags.Public | BindingFlags.Static);

        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
    }
}

/// <summary>
/// Фабрика, регистрирующая <see cref="KnddbEnumConverter{TEnum}"/> для всех типов перечислений.
/// </summary>
/// <remarks>
/// <para>
/// Достаточно один раз добавить <c>new KnddbEnumConverterFactory()</c> в
/// <see cref="JsonSerializerOptions.Converters"/>, чтобы все перечисления SDK
/// обрабатывались единообразно.
/// </para>
/// <para>
/// Для обнуляемых перечислений (<c>TransferState?</c>, <c>ConsumptionType?</c>) фабрика создаёт
/// отдельный конвертер: System.Text.Json требует, чтобы один конвертер обслуживал ровно один тип.
/// Именно поэтому для <c>T?</c> и <c>T</c> используются разные экземпляры.
/// </para>
/// </remarks>
public sealed class KnddbEnumConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum || IsNullableEnum(typeToConvert);

    /// <inheritdoc />
    /// <remarks>
    /// Конвертер создаётся для конкретного типа перечисления динамически.
    /// В Native AOT используется сгенерированный контекст <c>KnddbJsonContext</c>,
    /// которому известны все типы контрактов SDK.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Известное ограничение: конвертер создаётся для типа перечисления динамически; в Native AOT применяется сгенерированный контекст KnddbJsonContext.")]
    [UnconditionalSuppressMessage(
        "Trim",
        "IL2071",
        Justification = "Все члены перечисления читаются через Enum.GetValues<TEnum>() внутри самого конвертера.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var enumType = Nullable.GetUnderlyingType(typeToConvert);

        if (enumType is null)
        {
            return (JsonConverter)Activator.CreateInstance(
                typeof(KnddbEnumConverter<>).MakeGenericType(typeToConvert))!;
        }

        return (JsonConverter)Activator.CreateInstance(
            typeof(NullableKnddbEnumConverter<>).MakeGenericType(enumType))!;
    }

    private static bool IsNullableEnum(Type type)
        => Nullable.GetUnderlyingType(type)?.IsEnum ?? false;
}

/// <summary>
/// Конвертер обнуляемого перечисления KNMDB.
/// </summary>
/// <typeparam name="TEnum">Тип перечисления.</typeparam>
/// <remarks>
/// <see langword="null"/> записывается как JSON <c>null</c> и читается из <c>null</c>.
/// Во всех остальных случаях работа делегируется <see cref="KnddbEnumConverter{TEnum}"/>.
/// </remarks>
public sealed class NullableKnddbEnumConverter<TEnum> : JsonConverter<TEnum?>
    where TEnum : struct, Enum
{
    private static readonly KnddbEnumConverter<TEnum> Inner = new();

    /// <inheritdoc />
    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(TEnum), options);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }

    /// <inheritdoc />
    public override TEnum? ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Inner.ReadAsPropertyName(ref reader, typeof(TEnum), options);

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WritePropertyName(string.Empty);
            return;
        }

        Inner.WriteAsPropertyName(writer, value.Value, options);
    }
}
