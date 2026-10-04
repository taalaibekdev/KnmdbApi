using System.Text.Json;
using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Api;

namespace Knmdb.TrackAndTrace.Serialization;

/// <summary>
/// Конвертер для <see cref="ApiProblemDetails"/>.
/// </summary>
/// <remarks>
/// Стандартная десериализация «в объект» описана в RFC 7807: незнакомые серверу свойства
/// верхнего уровня считаются расширениями и складываются в <see cref="ApiProblemDetails.Extensions"/>.
/// Такой конвертер нужен, чтобы SDK показывал прикладные поля ошибок KNMDB, не теряя их,
/// и при этом был совместим с генерацией сериализаторов во время сборки (AOT/trimming).
/// </remarks>
internal sealed class ApiProblemDetailsJsonConverter : JsonConverter<ApiProblemDetails>
{
    private static readonly HashSet<string> KnownProperties =
    [
        "type", "title", "status", "detail", "instance",
    ];

    /// <inheritdoc />
    public override ApiProblemDetails Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            if (reader.TokenType is JsonTokenType.Null)
            {
                throw new JsonException("Ожидался объект ProblemDetails, получено null.");
            }

            reader.Skip();
            return new ApiProblemDetails();
        }

        var result = new ApiProblemDetails();
        Dictionary<string, object?>? extensions = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var name = reader.GetString();
            reader.Read();

            switch (name)
            {
                case "type":
                    result.Type = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "title":
                    result.Title = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "status":
                    result.Status = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
                    break;
                case "detail":
                    result.Detail = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "instance":
                    result.Instance = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case null:
                    reader.Skip();
                    break;
                default:
                    extensions ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    extensions[name] = ReadExtension(ref reader);
                    break;
            }
        }

        result.Extensions = extensions;
        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ApiProblemDetails value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        WriteString(writer, "type", value.Type);
        WriteString(writer, "title", value.Title);

        if (value.Status is not null)
        {
            writer.WriteNumber("status", value.Status.Value);
        }

        WriteString(writer, "detail", value.Detail);
        WriteString(writer, "instance", value.Instance);

        if (value.Extensions is not null)
        {
            foreach (var (key, item) in value.Extensions)
            {
                if (KnownProperties.Contains(key))
                {
                    continue;
                }

                writer.WritePropertyName(key);
                WriteExtensionValue(writer, item);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
        {
            return;
        }

        writer.WriteString(propertyName, value);
    }

    /// <summary>
    /// Записывает значение расширения. Значения нормализуются при чтении
    /// (<see cref="ReadExtension"/>), поэтому здесь достаточно простого ветвления
    /// без обращения к рефлексии и без риска для AOT-компиляции.
    /// </summary>
    private static void WriteExtensionValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    private static object? ReadExtension(ref Utf8JsonReader reader)
        => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone(),
        };
}
