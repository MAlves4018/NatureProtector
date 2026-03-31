using System.Text.Json;
using System.Text.Json.Serialization;

namespace NatureProtector.Shared.Messaging;

public static class JsonEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    public static T? Deserialize<T>(ReadOnlyMemory<byte> body)
    {
        return JsonSerializer.Deserialize<T>(body.Span, Options);
    }

    public static string SerializeToString<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }
}