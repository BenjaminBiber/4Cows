using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services;

public class CowService
{
    private ImmutableDictionary<string, Cow> _cachedCows = ImmutableDictionary<string, Cow>.Empty;

        public ImmutableDictionary<string, Cow> Cows => _cachedCows;

        public async Task GetAllDataAsync()
        {
            var cows = await DatabaseService.ReadDataAsync(@"SELECT * FROM Cow;", reader =>
            {
                var cow = new Cow
                {
                    EarTagNumber = reader.GetString("Ear_Tag_Number"),
                    CollarNumber = reader.GetInt32("Collar_Number"),
                    IsGone = reader.GetBoolean("IsGone")
                };
                return cow;
            });

            _cachedCows = cows.ToImmutableDictionary(c => c.EarTagNumber);
        }

        public async Task<bool> InsertDataAsync(Cow cow)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Cow` (`Ear_Tag_Number`, `Collar_Number`, `IsGone`) VALUES (@EarTagNumber, @CollarNumber, @IsGone);";
                command.Parameters.AddWithValue("@EarTagNumber", cow.EarTagNumber);
                command.Parameters.AddWithValue("@CollarNumber", cow.CollarNumber);
                command.Parameters.AddWithValue("@IsGone", cow.IsGone);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                _cachedCows = _cachedCows.Add(cow.EarTagNumber, cow);
            }

            return isSuccess;
        }

        public async Task<bool> RemoveByIdAsync(string earTagNumber)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"DELETE FROM `Cow` WHERE `Ear_Tag_Number` = @EarTagNumber;";
                command.Parameters.AddWithValue("@EarTagNumber", earTagNumber);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedCows.ContainsKey(earTagNumber))
            {
                _cachedCows = _cachedCows.Remove(earTagNumber);
            }

            return isSuccess;
        }

        public Cow GetById(string earTagNumber)
        {
            return _cachedCows.ContainsKey(earTagNumber) ? _cachedCows[earTagNumber] : null;
        }

        public string GetEarTagNumberByCollarNumber(int collarNumber)
        {
            return _cachedCows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber)?.EarTagNumber ?? String.Empty;
        }
        
        public int GetCollarNumberByEarTagNumber(string earTagNumber)
        {
            return _cachedCows.ContainsKey(earTagNumber) ? _cachedCows[earTagNumber].CollarNumber : int.MinValue;
        }
        public async Task<bool> UpdateCollarNumberAsync(string earTagNumber, int newCollarNumber)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"UPDATE `Cow` SET `Collar_Number` = @CollarNumber WHERE `Ear_Tag_Number` = @EarTagNumber;";
                command.Parameters.AddWithValue("@CollarNumber", newCollarNumber);
                command.Parameters.AddWithValue("@EarTagNumber", earTagNumber);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedCows.ContainsKey(earTagNumber))
            {
                var updatedCow = _cachedCows[earTagNumber];
                updatedCow.CollarNumber = newCollarNumber;
                _cachedCows = _cachedCows.SetItem(earTagNumber, updatedCow);
            }

            return isSuccess;
        }
        
}