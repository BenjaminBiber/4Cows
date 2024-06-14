namespace BB_Cow.Class
{
    public class NumberEntry
    {
        public NumberEntry()
        {
        }
        public NumberEntry(int value, int ear_value)
        {
            Value = value;
            EarValue = ear_value;
        }
        public int? Value { get; set; }
        public int? EarValue { get; set; }


    }
}
