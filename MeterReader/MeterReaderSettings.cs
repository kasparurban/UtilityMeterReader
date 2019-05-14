namespace MeterReader
{
    public class MeterReaderSettings
    {
        public bool DarkSectors { get; set; } = false;
        public int LightSectorsAdaptiveThresholdBlockSize { get; set; } = 61;
        public double LightSectorsAdaptiveThresholdC { get; set; } = 31;
        public int InputAnalysisMaxSize { get; set; } = 400;
        public int LightSectorsSeparateMeterNumbersResizeMaxSize { get; set; } = 40;
        public int DarkSectorsSeparateMeterNumbersResizeMaxSize { get; set; } = 45;
        public int NumbersDetectionAdaptiveThresholdBlockSize { get; set; } = 99;
        public double NumbersDetectionAdaptiveThresholdC { get; set; } = 9;
        public int FullPartSectors { get; set; } = 5;
    }
}