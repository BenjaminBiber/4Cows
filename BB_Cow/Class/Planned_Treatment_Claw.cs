namespace BB_Cow.Class
{
    public class Planned_Treatment_Claw
    {   public int Planned_Claw_Treatment_ID { get; set; }
        public int Collar_Number { get; set; }
        public string Description { get; set; }
        public DateTime? Treatment_Date { get; set; }
        public bool Claw_Finding_LV { get; set; }
       
        public bool Claw_Finding_LH { get; set; }
       
        public bool Claw_Finding_RV { get; set; }
      
        public bool Claw_Finding_RH { get; set; }
       
        public Planned_Treatment_Claw() { }
    }
}
