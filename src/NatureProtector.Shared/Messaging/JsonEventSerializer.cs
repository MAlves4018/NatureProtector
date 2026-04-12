using System.Text.Json;
using System.Text.Json.Serialization;

/*
 * Este helper centraliza a serialização JSON dos contratos partilhados.
 *
 * Rationale:
 * - O simulador, o fluxo de prevenção e os mecanismos de persistência têm de
 *   usar a mesma configuração de serialização para evitar deriva entre runtime,
 *   testes e storage.
 * - Um único ponto de configuração simplifica a evolução do envelope e dos
 *   payloads publicados.
 *
 * Design considerations:
 * - A serialização usa camelCase para alinhar com os contratos de integração e
 *   com o JSON persistido.
 * - Os enum são serializados como texto para facilitar leitura humana e
 *   depuração.
 * - Valores nulos são omitidos para manter os payloads compactos.
 */

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

    /// <summary>
    /// Serializa um valor para um buffer UTF-8 pronto a publicar ou persistir.
    /// </summary>
    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    /// <summary>
    /// Desserializa um corpo binário usando a configuração JSON partilhada.
    /// </summary>
    public static T? Deserialize<T>(ReadOnlyMemory<byte> body)
    {
        return JsonSerializer.Deserialize<T>(body.Span, Options);
    }

    /// <summary>
    /// Serializa um valor para string para uso em logs e persistência textual.
    /// </summary>
    public static string SerializeToString<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }
}
