namespace MeterReader.Xamarin
{
    public class Reading
    {
        public Reading(string result, params Rectangle[] rectangles)
        {
            Result = result;
            Rectangles = rectangles;
        }

        public bool Success => !string.IsNullOrWhiteSpace(Result);
        public string Result { get; private set; }
        public Rectangle[] Rectangles { get; private set; }
    }
}