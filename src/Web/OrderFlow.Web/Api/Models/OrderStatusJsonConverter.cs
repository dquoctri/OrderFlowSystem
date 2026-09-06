using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderFlow.Web.Api.Models;

public sealed class OrderStatusJsonConverter : JsonConverter<string>
{
    private static readonly string[] StatusNames = ["Pending", "Reserving", "Charging", "Confirmed", "Cancelled"];

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString() ?? string.Empty,
        JsonTokenType.Number when reader.TryGetInt32(out var value) => value >= 0 && value < StatusNames.Length
            ? StatusNames[value]
            : value.ToString(CultureInfo.InvariantCulture),
        JsonTokenType.Null => string.Empty,
        _ => throw new JsonException($"Order status must be a string or numeric enum value, but was {reader.TokenType}.")
    };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
