using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services
{
    public class ClawTreatmentService
    {
        private ImmutableDictionary<int, ClawTreatment> _cachedTreatments = ImmutableDictionary<int, ClawTreatment>.Empty;
        private ImmutableList<string> _cachedClawFindingList = ImmutableList<string>.Empty;

        public ImmutableDictionary<int, ClawTreatment> Treatments => _cachedTreatments;
        public ImmutableList<string> ClawFindingList => _cachedClawFindingList;

        public async Task GetAllDataAsync()
        {
            var treatments = await DatabaseService.ReadDataAsync(@"SELECT * FROM `Claw_Treatment`;", reader =>
            {
                var treatment = new ClawTreatment
                {
                    ClawTreatmentId = reader.GetInt32("Claw_Treatment_ID"),
                    EarTagNumber = reader.GetString("Ear_Tag_Number"),
                    TreatmentDate = reader.GetDateTime("Treatment_Date"),
                    
                    ClawFindingLV = reader.GetString("Claw_Finding_LV"),
                    BandageLV = reader.GetBoolean("Bandage_LV"),
                    BlockLV = reader.GetBoolean("Block_LV"),
                    
                    ClawFindingLH = reader.GetString("Claw_Finding_LH"),
                    BandageLH = reader.GetBoolean("Bandage_LH"),
                    BlockLH = reader.GetBoolean("Block_LH"),
                    
                    ClawFindingRV = reader.GetString("Claw_Finding_RV"),
                    BandageRV = reader.GetBoolean("Bandage_RV"),
                    BlockRV = reader.GetBoolean("Block_RV"),
                    
                    ClawFindingRH = reader.GetString("Claw_Finding_RH"),
                    BandageRH = reader.GetBoolean("Bandage_RH"),
                    BlockRH = reader.GetBoolean("Block_RH"),
                    
                    IsBandageRemoved = reader.GetBoolean("IsBandageRemoved")
                };
                return treatment;
            });

            _cachedTreatments = treatments.ToImmutableDictionary(t => t.ClawTreatmentId);
            _cachedClawFindingList = treatments.SelectMany(t => new[] { t.ClawFindingLV, t.ClawFindingLH, t.ClawFindingRV, t.ClawFindingRH }).Distinct().ToImmutableList();
            LoggerService.LogInformation(typeof(ClawTreatmentService), $"Loaded {_cachedTreatments.Count} claw treatments.");
        }

        public async Task<bool> InsertDataAsync(ClawTreatment clawTreatment)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Claw_Treatment` (`Ear_Tag_Number`, `Treatment_Date`, `Claw_Finding_LV`, `Bandage_LV`, `Block_LV`, `Claw_Finding_LH`, `Bandage_LH`, `Block_LH`, `Claw_Finding_RV`, `Bandage_RV`, `Block_RV`, `Claw_Finding_RH`, `Bandage_RH`, `Block_RH`, `IsBandageRemoved`) VALUES (@EarTagNumber, @TreatmentDate, @ClawFindingLV, @BandageLV, @BlockLV, @ClawFindingLH, @BandageLH, @BlockLH, @ClawFindingRV, @BandageRV, @BlockRV, @ClawFindingRH, @BandageRH, @BlockRH, @IsBandageRemoved);";
                command.Parameters.AddWithValue("@EarTagNumber", clawTreatment.EarTagNumber);
                command.Parameters.AddWithValue("@TreatmentDate", clawTreatment.TreatmentDate);
                
                command.Parameters.AddWithValue("@ClawFindingLV", clawTreatment.ClawFindingLV);
                command.Parameters.AddWithValue("@BandageLV", clawTreatment.BandageLV);
                command.Parameters.AddWithValue("@BlockLV", clawTreatment.BlockLV);
                
                command.Parameters.AddWithValue("@ClawFindingLH", clawTreatment.ClawFindingLH);
                command.Parameters.AddWithValue("@BandageLH", clawTreatment.BandageLH);
                command.Parameters.AddWithValue("@BlockLH", clawTreatment.BlockLH);
                
                command.Parameters.AddWithValue("@ClawFindingRV", clawTreatment.ClawFindingRV);
                command.Parameters.AddWithValue("@BandageRV", clawTreatment.BandageRV);
                command.Parameters.AddWithValue("@BlockRV", clawTreatment.BlockRV);
                
                command.Parameters.AddWithValue("@ClawFindingRH", clawTreatment.ClawFindingRH);
                command.Parameters.AddWithValue("@BandageRH", clawTreatment.BandageRH);
                command.Parameters.AddWithValue("@BlockRH", clawTreatment.BlockRH);
                
                command.Parameters.AddWithValue("@IsBandageRemoved", clawTreatment.IsBandageRemoved);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                _cachedTreatments = _cachedTreatments.Add(clawTreatment.ClawTreatmentId, clawTreatment);
                _cachedClawFindingList = _cachedClawFindingList.AddRange(new[] { clawTreatment.ClawFindingLV, clawTreatment.ClawFindingLH, clawTreatment.ClawFindingRV, clawTreatment.ClawFindingRH }).Distinct().ToImmutableList();
                LoggerService.LogInformation(typeof(ClawTreatmentService), "Inserted claw treatment: {@clawTreatment}.", clawTreatment);
            }

            return isSuccess;
        }

        public async Task<ClawTreatment> GetByIDAsync(int id)
        {
            if (_cachedTreatments.ContainsKey(id))
            {
                return _cachedTreatments[id];
            }

            var treatments = await DatabaseService.ReadDataAsync("SELECT * FROM `Claw_Treatment` WHERE Claw_Treatment_ID = @Id;", reader =>
            {
                var treatment = new ClawTreatment
                {
                    ClawTreatmentId = reader.GetInt32("Claw_Treatment_ID"),
                    EarTagNumber = reader.GetString("Ear_Tag_Number"),
                    TreatmentDate = reader.GetDateTime("Treatment_Date"),
                    
                    ClawFindingLV = reader.GetString("Claw_Finding_LV"),
                    BandageLV = reader.GetBoolean("Bandage_LV"),
                    BlockLV = reader.GetBoolean("Block_LV"),
                    
                    ClawFindingLH = reader.GetString("Claw_Finding_LH"),
                    BandageLH = reader.GetBoolean("Bandage_LH"),
                    BlockLH = reader.GetBoolean("Block_LH"),
                    
                    ClawFindingRV = reader.GetString("Claw_Finding_RV"),
                    BandageRV = reader.GetBoolean("Bandage_RV"),
                    BlockRV = reader.GetBoolean("Block_RV"),
                    
                    ClawFindingRH = reader.GetString("Claw_Finding_RH"),
                    BandageRH = reader.GetBoolean("Bandage_RH"),
                    BlockRH = reader.GetBoolean("Block_RH"),
                    
                    IsBandageRemoved = reader.GetBoolean("IsBandageRemoved")
                };
                return treatment;
            }, new { Id = id });

            var treatmentResult = treatments.FirstOrDefault();
            if (treatmentResult != null)
            {
                _cachedTreatments = _cachedTreatments.Add(id, treatmentResult);
            }

            return treatmentResult ?? new ClawTreatment();
        }

        public async Task<bool> RemoveBandageAsync(int id)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"UPDATE `Claw_Treatment` SET `IsBandageRemoved` = true WHERE `Claw_Treatment_ID` = @id;";
                command.Parameters.AddWithValue("@id", id);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedTreatments.ContainsKey(id))
            {
                var updatedTreatment = _cachedTreatments[id];
                updatedTreatment.IsBandageRemoved = true;
                _cachedTreatments = _cachedTreatments.SetItem(id, updatedTreatment);
                LoggerService.LogInformation(typeof(ClawTreatmentService), "Removed bandage from claw treatment: {@clawTreatment}.", updatedTreatment);
            }

            return isSuccess;
        }

        public int[] GetClawTreatmentChartData()
        {
            var currentYear = DateTime.Now.Year;
            var months = Enumerable.Range(1, 12);

            var groupedData = _cachedTreatments.Values
                .Where(obj => obj.TreatmentDate.Year == currentYear)
                .GroupBy(obj => obj.TreatmentDate.Month)
                .ToDictionary(g => g.Key, g => g.Count());

            return months
                .Select(month => groupedData.ContainsKey(month) ? groupedData[month] : 0)
                .ToArray();
        }

        public List<ClawTreatment> GetClawTreatments()
        {
            return Treatments.Values.ToList();
        }

        public List<ClawTreatment> GetClawTreatmentsWithBandage()
        {
            return Treatments.Values.Where(x => (x.BandageLH || x.BandageLV || x.BandageRV || x.BandageRH) && !x.IsBandageRemoved).OrderBy(x => x.TreatmentDate).ToList();
        }

    }
}
