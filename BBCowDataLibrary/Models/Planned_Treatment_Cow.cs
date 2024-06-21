namespace BB_Cow.Class
{
    public class Planned_Treatment_Cow
    {
        public int Planned_Cow_Treatment_ID { get; set; }
        public int Collar_Number { get; set; }
        public DateTime Administration_Date { get; set; }
        public float Medicine_Dosage { get; set; }
        public string Medicine_Name { get; set; }
        public string WhereHow { get; set; }
        public bool IsFound { get; set; }
        public bool IsTreatet { get; set; }
        public int Ear_Number { get; set; }


        public Planned_Treatment_Cow() { }

        public Planned_Treatment_Cow(int collar_Number, DateTime administration_Date, int medicine_Dosage, string medicine_Name)
        {
            Collar_Number = collar_Number;
            Administration_Date = administration_Date;
            Medicine_Dosage = medicine_Dosage;
            Medicine_Name = medicine_Name;
        }
    }
}
