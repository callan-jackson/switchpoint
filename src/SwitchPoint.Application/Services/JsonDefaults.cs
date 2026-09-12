using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchPoint.Application.Services;

/// <summary>Engine/application version stamped on every stored result and report.</summary>
public static class EngineVersion
{
    public const string Current = "1.0.0";
}

/// <summary>
/// Deterministic JSON for stored results and audit payloads: camelCase, string enums, no indentation,
/// invariant culture, ordered exactly as the record declares. The same object always serialises to the
/// same bytes, so SHA-256 hashes are stable across runs and machines.
/// </summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    /// <summary>Lower-case SHA-256 hex of the UTF-8 bytes.</summary>
    public static string Sha256(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    public static string Sha256(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions o = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.Strict,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.Converters.Add(new NormalisedDecimalConverter());
        o.Converters.Add(new UtcDateTimeConverter());
        return o;
    }

    /// <summary>Strips trailing zeros from the decimal scale so 0.2500 and 0.25 serialise identically (stable hashes).</summary>
    public static decimal Normalise(decimal value) => decimal.Parse(value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Writes decimals with a canonical scale; reads them as usual.</summary>
public sealed class NormalisedDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(JsonDefaults.Normalise(value));
    }
}

/// <summary>
/// Writes every <see cref="DateTime"/> with an explicit UTC offset. Every timestamp the API stores is UTC,
/// but both database providers hand values back with <see cref="DateTimeKind.Unspecified"/>, so a value
/// written as "…376378Z" came back as "…376378" and a browser parsed it as local time — an hour out through
/// British Summer Time. Treating an unspecified kind as UTC here fixes reads without touching every entity.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture));
    }
}
