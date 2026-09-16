using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meadow.Shared.Models;

/// <summary>
/// A string enum converter that falls back to the enum's default member instead of throwing.
///
/// For PRESENTATION fields only, and the distinction is the whole point. JsonStringEnumConverter
/// throws on a name it does not know, KpiDefinition.Deserialize catches that and returns null, and
/// the tile then reads "Definition unlesbar" - the entire KPI killed by one field that only
/// decides how it looks.
///
/// So: an unknown Source, Measure, GroupBy or Timeframe still fails loudly, because those change
/// WHAT is counted and a silent fallback would make the tile quietly count something else. An
/// unknown SeriesDisplay or TargetDirection degrades to None, because the worst case is a tile
/// without its chart or without its traffic light - which is exactly what an older build would
/// have shown anyway.
///
/// Found the hard way: renaming a member of KpiSeriesDisplay blanked every tile whose definition
/// had been written by the previous build.
/// </summary>
public sealed class KpiLenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Numbers are tolerated for the same reason names are: whatever wrote this, the definition
        // itself is still readable and the value only decides an appearance.
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number))
        {
            var value = (T)Enum.ToObject(typeof(T), number);
            return Enum.IsDefined(typeof(T), value) ? value : default;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            return default;
        }

        return Enum.TryParse<T>(reader.GetString(), ignoreCase: true, out var parsed) ? parsed : default;
    }

    // Always written as a name, like every other enum here: reordering the members later must not
    // silently reinterpret a stored definition.
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
