namespace BB_Cow.Class
{
    public class NumberEntry
    {
        public NumberEntry()
        {
        }
        public NumberEntry(int value, string ear_value)
        {
            Value = value;
            EarValue = ear_value;
        }
        public int? Value { get; set; }
        public string? EarValue { get; set; }


    }
}
