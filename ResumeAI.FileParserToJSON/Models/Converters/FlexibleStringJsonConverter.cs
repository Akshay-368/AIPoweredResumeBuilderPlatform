using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResumeAI.FileParserToJson.Models.Converters;

public sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.True)
        {
            return "true";
        }

        if (reader.TokenType == JsonTokenType.False)
        {
            return "false";
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var values = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? new List<string>();
            return string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
