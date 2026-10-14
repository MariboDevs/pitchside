using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pitchside.Contracts;

/// <summary>Writes a <see cref="FactValue"/> as a bare JSON number or string, and reads either back.</summary>
public sealed class FactValueJsonConverter : JsonConverter<FactValue>
{
    public override FactValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => FactValue.Of(reader.GetDouble()),
            JsonTokenType.String => FactValue.Of(reader.GetString()!),
            _ => throw new JsonException($"A fact value is a number or a string, not {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, FactValue value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.Number is { } number)
        {
            writer.WriteNumberValue(number);
        }
        else if (value.Text is { } text)
        {
            writer.WriteStringValue(text);
        }
        else
        {
            // default(FactValue) holds neither; writing null would break the number-or-string contract.
            throw new JsonException("A fact value must be a number or a string; default(FactValue) cannot be written.");
        }
    }
}
