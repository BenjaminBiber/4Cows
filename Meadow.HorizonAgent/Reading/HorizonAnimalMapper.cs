using System.Data;
using Meadow.Shared.Models.Horizon;

namespace Meadow.HorizonAgent.Reading;

/// <summary>
/// Macht aus dem Ergebnis der AnimalQuery Tiere. Kennt keine Datenbank, nur IDataReader -
/// deshalb laesst er sich ohne Horizon gegen einen DataTable-Reader testen.
/// </summary>
public static class HorizonAnimalMapper
{
    public static readonly string[] RequiredColumns = { "AnimalId", "CollarNumber", "EarTag", "IsGone" };

    public static List<HorizonAnimal> Map(IDataReader reader)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            ordinals[reader.GetName(i)] = i;
        }

        var missing = RequiredColumns.Where(c => !ordinals.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Die Abfrage liefert folgende Pflichtspalten nicht: {string.Join(", ", missing)}. " +
                $"Geliefert wurden: {string.Join(", ", ordinals.Keys)}.");
        }

        var animals = new List<HorizonAnimal>();
        var row = 0;
        while (reader.Read())
        {
            row++;

            var animalId = Text(reader, ordinals, "AnimalId");
            if (string.IsNullOrWhiteSpace(animalId))
            {
                throw new InvalidOperationException($"Zeile {row}: AnimalId ist leer. Abbruch.");
            }

            animals.Add(new HorizonAnimal
            {
                AnimalId = animalId!,
                CollarNumber = Collar(reader, ordinals),
                EarTag = Text(reader, ordinals, "EarTag"),
                IsGone = Flag(reader, ordinals, "IsGone"),
                Location = Text(reader, ordinals, "Location"),
                Group = Text(reader, ordinals, "Group"),
                Description = Text(reader, ordinals, "Description"),
            });
        }

        return animals;
    }

    // Fehlende optionale Spalte, NULL und Leerstring sind fuer uns dasselbe: kein Wert.
    private static string? Text(IDataReader reader, Dictionary<string, int> ordinals, string column)
    {
        if (!ordinals.TryGetValue(column, out var i) || reader.IsDBNull(i))
        {
            return null;
        }

        var value = Convert.ToString(reader.GetValue(i))?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    // Das Halsband kommt je nach Abfrage als int oder bigint; NULL heisst "kein Halsband" = 0.
    private static int Collar(IDataReader reader, Dictionary<string, int> ordinals)
    {
        var i = ordinals["CollarNumber"];
        return reader.IsDBNull(i) ? 0 : Convert.ToInt32(reader.GetValue(i));
    }

    private static bool Flag(IDataReader reader, Dictionary<string, int> ordinals, string column)
    {
        var i = ordinals[column];
        return !reader.IsDBNull(i) && Convert.ToBoolean(reader.GetValue(i));
    }
}
