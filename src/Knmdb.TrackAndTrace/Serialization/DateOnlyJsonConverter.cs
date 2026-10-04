using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knmdb.TrackAndTrace.Serialization;

/// <summary>
/// Конвертер <see cref="DateTimeOffset"/> в строку формата <c>yyyy-MM-dd</c> и обратно.
/// </summary>
/// <remarks>
/// <para>
/// Большинство дат в API KNMDB документированы как «yyyy-mm-dd» (например,
/// <c>declarationDate</c>, <c>productionDate</c>, <c>expirationDate</c>), но на сервере объявлены
/// как <c>DateTimeOffset</c>. Такой конвертер гарантирует, что клиент отправит ровно ту строку,
/// которую ожидает сервер, и корректно прочитает как «дату без времени», так и полный ISO-8601.
/// </para>
/// <para>Применяется точечно, через атрибут <see cref="JsonConverterAttribute"/> на свойстве.</para>
/// </remarks>
public sealed class DateOnlyJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>Формат строки, ожидаемый API: <c>yyyy-MM-dd</c>.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        if (DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
}

/// <summary>
/// Конвертер <see cref="Nullable{DateTimeOffset}"/> в строку формата <c>yyyy-MM-dd</c> и обратно.
/// </summary>
/// <remarks>
/// <see langword="null"/> записывается как JSON <c>null</c> — это важно, чтобы необязательные
/// поля-фильтры не превращались в «пустую дату» и не меняли поведение поиска на сервере.
/// </remarks>
public sealed class NullableDateOnlyJsonConverter : JsonConverter<DateTimeOffset?>
{
    /// <inheritdoc />
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, DateOnlyJsonConverter.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(DateOnlyJsonConverter.DateFormat, CultureInfo.InvariantCulture));
    }
}
