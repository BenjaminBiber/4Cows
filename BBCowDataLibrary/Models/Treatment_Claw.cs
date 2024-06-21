namespace BB_Cow.Class
{
    public class Treatment_Claw
    {
        public int Claw_Treatment_ID { get; set; }
        public int Collar_Number { get; set; }
        public DateTime Treatment_Date { get; set; }
        public string Claw_Finding_LV { get; set; }
        public bool Bandage_LV { get; set; }
        public bool Block_LV { get; set; }

        public string Claw_Finding_LH { get; set; }
        public bool Bandage_LH { get; set; }
        public bool Block_LH { get; set; }

        public string Claw_Finding_RV { get; set; }
        public bool Bandage_RV { get; set; }
        public bool Block_RV { get; set; }

        public string Claw_Finding_RH { get; set; }
        public bool Bandage_RH { get; set; }
        public bool Block_RH { get; set; }

        public bool IsBandageRemoved { get; set; }

        public Treatment_Claw() {
            IsBandageRemoved = false;
        }
    }
}
