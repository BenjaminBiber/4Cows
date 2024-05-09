using BB_Cow.Class;
using System.Collections.Generic;
using System.Linq;

namespace BB_Cow.Services
{
    public static class StaticService
    {
        public static List<Treatment_Claw> StaticClawTreatments { get; set; } = new();
        public static List<string> StaticClawFindingList { get; set; } = new();

        public static List<Treatment_Cow> StaticCowTreatments { get; set; } = new();
        public static List<string> StaticCowMedicineTreatmentList { get; set; } = new();
        public static List<string> StaticCowWhereHowList { get; set; } = new();

        public static List<Planned_Treatment_Claw> StaticPlannedClawTreatments { get; set; } = new();
        public static List<Planned_Treatment_Cow> StaticPlannedCowTreatments { get; set; } = new();

    }
}
