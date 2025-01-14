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
            LoggerService.LogInformation(typeof(CowService), $"Loaded {_cachedCows.Count} cows.");
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
                LoggerService.LogInformation(typeof(CowService), "Inserted cow: {@cow}.", cow);
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
                LoggerService.LogInformation(typeof(CowService), "Updated collar number for cow with ear tag number {earTagNumber} to {newCollarNumber}.", earTagNumber, newCollarNumber);
            }

            return isSuccess;
        }
        
        public async Task<bool> UpdateIsGoneAsync(string earTagNumber, bool isGone)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"UPDATE `Cow` SET `IsGone` = @IsGone WHERE `Ear_Tag_Number` = @EarTagNumber;";
                command.Parameters.AddWithValue("@IsGone", isGone);
                command.Parameters.AddWithValue("@EarTagNumber", earTagNumber);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedCows.ContainsKey(earTagNumber))
            {
                var updatedCow = _cachedCows[earTagNumber];
                updatedCow.IsGone = isGone;
                _cachedCows = _cachedCows.SetItem(earTagNumber, updatedCow);
                LoggerService.LogInformation(typeof(CowService), "Updated is gone for cow with ear tag number {earTagNumber} to {isGone}.", earTagNumber, isGone);
            }

            return isSuccess;
        }
        
        public async Task<IEnumerable<string>> SearchCowEarTagNumbers(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value) || !Cows.Any())
            {
                return Cows.Keys;
            }
            return Cows.Keys.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
        }
        
        
        public bool FilterFuncCow(string earTagNumber, string searchString)
        {
            if (!Cows.ContainsKey(earTagNumber))
            {
                return false;
            }
            var Cow = Cows[earTagNumber];
            if (string.IsNullOrWhiteSpace(searchString))
                return true;
            if (searchString.Length <= 3 && searchString.ToLower() == Cow.CollarNumber.ToString().ToLower())
            {
                return true;
            }else if(searchString.Length > 3 && Cow.EarTagNumber.ToLower().Contains(searchString.ToLower()) || Cow.CollarNumber.ToString().ToLower() == searchString.ToLower())
            {
                return true;
            }
            return false;
        }
}