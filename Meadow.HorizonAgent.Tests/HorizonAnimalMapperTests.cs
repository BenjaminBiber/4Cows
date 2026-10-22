using System.Data;
using Meadow.HorizonAgent.Reading;

namespace Meadow.HorizonAgent.Tests;

public class HorizonAnimalMapperTests
{
    // Baut einen Reader wie ihn SqlDataReader liefern wuerde, nur ohne Datenbank.
    private static IDataReader Reader(DataTable table) => table.CreateDataReader();

    private static DataTable FullTable()
    {
        var table = new DataTable();
        table.Columns.Add("AnimalId", typeof(string));
        table.Columns.Add("CollarNumber", typeof(int));
        table.Columns.Add("EarTag", typeof(string));
        table.Columns.Add("IsGone", typeof(bool));
        table.Columns.Add("Location", typeof(string));
        table.Columns.Add("Group", typeof(string));
        table.Columns.Add("Description", typeof(string));
        return table;
    }

    [Fact]
    public void Maps_all_columns_and_trims()
    {
        var table = FullTable();
        table.Rows.Add(" 42 ", 1234, " DE 123 ", true, " Stall 1 ", " Gruppe A ", " lahm ");

        var animals = HorizonAnimalMapper.Map(Reader(table));

        var animal = Assert.Single(animals);
        Assert.Equal("42", animal.AnimalId);
        Assert.Equal(1234, animal.CollarNumber);
        Assert.Equal("DE 123", animal.EarTag);
        Assert.True(animal.IsGone);
        Assert.Equal("Stall 1", animal.Location);
        Assert.Equal("Gruppe A", animal.Group);
        Assert.Equal("lahm", animal.Description);
    }

    [Fact]
    public void Missing_required_column_names_it()
    {
        var table = FullTable();
        table.Columns.Remove("EarTag");
        table.Rows.Add("1", 2, false, "Stall", "Gruppe", "Text");

        var error = Assert.Throws<InvalidOperationException>(() => HorizonAnimalMapper.Map(Reader(table)));

        Assert.Contains("EarTag", error.Message);
    }

    [Fact]
    public void Missing_optional_columns_become_null()
    {
        var table = new DataTable();
        table.Columns.Add("AnimalId", typeof(string));
        table.Columns.Add("CollarNumber", typeof(int));
        table.Columns.Add("EarTag", typeof(string));
        table.Columns.Add("IsGone", typeof(bool));
        table.Rows.Add("7", 70, "DE 7", false);

        var animal = Assert.Single(HorizonAnimalMapper.Map(Reader(table)));

        Assert.Null(animal.Location);
        Assert.Null(animal.Group);
        Assert.Null(animal.Description);
    }

    [Fact]
    public void Null_collar_becomes_zero_and_bigint_collar_is_converted()
    {
        var table = new DataTable();
        table.Columns.Add("AnimalId", typeof(string));
        table.Columns.Add("CollarNumber", typeof(long));
        table.Columns.Add("EarTag", typeof(string));
        table.Columns.Add("IsGone", typeof(bool));
        table.Rows.Add("1", DBNull.Value, "DE 1", false);
        table.Rows.Add("2", 9001L, "DE 2", false);

        var animals = HorizonAnimalMapper.Map(Reader(table));

        Assert.Equal(0, animals[0].CollarNumber);
        Assert.Equal(9001, animals[1].CollarNumber);
    }

    [Fact]
    public void Empty_strings_become_null()
    {
        var table = FullTable();
        table.Rows.Add("5", 5, "   ", false, "", "   ", "");

        var animal = Assert.Single(HorizonAnimalMapper.Map(Reader(table)));

        Assert.Null(animal.EarTag);
        Assert.Null(animal.Location);
        Assert.Null(animal.Group);
        Assert.Null(animal.Description);
    }

    [Fact]
    public void Blank_animal_id_throws_with_row_number()
    {
        var table = FullTable();
        table.Rows.Add("1", 1, "DE 1", false, null, null, null);
        table.Rows.Add("   ", 2, "DE 2", false, null, null, null);

        var error = Assert.Throws<InvalidOperationException>(() => HorizonAnimalMapper.Map(Reader(table)));

        Assert.Contains("2", error.Message);
        Assert.Contains("AnimalId", error.Message);
    }

    [Fact]
    public void Column_names_are_matched_case_insensitive()
    {
        var table = new DataTable();
        table.Columns.Add("animalid", typeof(string));
        table.Columns.Add("COLLARNUMBER", typeof(int));
        table.Columns.Add("eartag", typeof(string));
        table.Columns.Add("isGONE", typeof(bool));
        table.Columns.Add("location", typeof(string));
        table.Rows.Add("3", 33, "DE 3", true, "Stall 2");

        var animal = Assert.Single(HorizonAnimalMapper.Map(Reader(table)));

        Assert.Equal("3", animal.AnimalId);
        Assert.Equal(33, animal.CollarNumber);
        Assert.Equal("DE 3", animal.EarTag);
        Assert.True(animal.IsGone);
        Assert.Equal("Stall 2", animal.Location);
    }
}
