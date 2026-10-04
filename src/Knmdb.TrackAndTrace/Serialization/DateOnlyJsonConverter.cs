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
    /// <remarks>
    /// <para>
    /// Время отбрасывается всегда, даже если вызывающая сторона задала его явно.
    /// </para>
    /// <para>
    /// Это важно: сервер KNMDB принимает в полях-датах только <c>yyyy-MM-dd</c>.
    /// Значение с временем (<c>2026-03-15T12:00:00Z</c>) он отклоняет, отвечая
    /// кодом результата 2 — «An error occurred while saving the entity changes».
    /// </para>
    /// <para>
    /// День берётся в той зоне, которую указала вызывающая сторона: дата
    /// <c>2026-03-15 00:30 +06:00</c> уходит как <c>2026-03-15</c>, а не как
    /// <c>2026-03-14</c>, хотя в UTC это ещё предыдущий день. Заменять зону
    /// на UTC нельзя — иначе дата операции сдвигалась бы на день назад.
    /// </para>
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var dateOnly = new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

        writer.WriteStringValue(dateOnly.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
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
    /// <remarks>
    /// Время отбрасывается так же, как в <see cref="DateOnlyJsonConverter"/>:
    /// сервер KNMDB принимает в полях-датах только <c>yyyy-MM-dd</c>.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var source = value.Value;
        var dateOnly = new DateTimeOffset(source.Year, source.Month, source.Day, 0, 0, 0, source.Offset);

        writer.WriteStringValue(dateOnly.ToString(DateOnlyJsonConverter.DateFormat, CultureInfo.InvariantCulture));
    }
}
