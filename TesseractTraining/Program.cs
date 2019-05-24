using System;

namespace TesseractTraining
{
    public class Program
    {
        static void Main(string[] args)
        {
            var tesstrain = new TessTraining(50, @"", @"", @"");
            tesstrain.TrainTesseract();
            Console.WriteLine("END");
        }
    }
}