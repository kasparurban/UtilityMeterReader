using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using OpenCvSharp;

namespace MeterReader.Tests
{
    public class DifferentMetersTests
    {
        [TestCase("1", "01148,995")]
        [TestCase("2", "00551,893")]
        [TestCase("3", "02257,73")]
        [TestCase("4", "04922,466")]
        [TestCase("5", "03828,88")]
        [TestCase("6", "01368,48")]
        public void Detects_Reading_From_Meter_That_Has_Dark_Number_Sectors(string imageName, string expectedResult)
        {
            var settings = new MeterReaderSettings
            {
                DarkSectors = true
            };
            var meterReader = new MeterReader(settings);
            var image = GetImage("DarkSectors", $"{imageName}.PNG");

            // Act
            var result = meterReader.Analyze(image);

            // Assert
            result.Result.Should().StartWith(expectedResult);
        }

        [TestCase("1", "00078,30")]
        [TestCase("2", "00098,36")]
        [TestCase("3", "00090,87")]
        [TestCase("4", "00081,73")]
        [TestCase("5", "00101,98")]
        public void Detects_Reading_From_Meter_That_Has_Light_Number_Sectors(string imageName, string expectedResult)
        {
            var settings = new MeterReaderSettings
            {
                DarkSectors = false,
            };
            var meterReader = new MeterReader(settings);
            var image = GetImage("LightSectors", $"{imageName}.PNG");

            // Act
            var result = meterReader.Analyze(image);

            // Assert
            result.Result.Should().StartWith(expectedResult);
        }

        [Test]
        public void Detects_Reading_From_Meter_That_Has_Segment_Separators_As_Black_Lines()
        {
            var settings = new MeterReaderSettings
            {
                DarkSectors = false,
                FullPartSectors = 6,
                NumbersDetectionAdaptiveThresholdBlockSize = 35,
                NumbersDetectionAdaptiveThresholdC = 40,
            };
            var meterReader = new MeterReader(settings);
            var image = GetImage("MeterWithBlackSegmentSeparators", "1.PNG");

            // Act
            var result = meterReader.Analyze(image);

            // Assert
            result.Result.Should().Be("000010");
        }

        [TestCase("1", "01148,995")]
        [TestCase("2", "01148,995")]
        [TestCase("3", "01148,995")]
        [TestCase("4", "01148,995")]
        public void Detects_Reading_From_Meter_From_Different_Lightning(string imageName, string expectedResult)
        {
            var settings = new MeterReaderSettings
            {
                DarkSectors = true
            };
            var meterReader = new MeterReader(settings);
            var image = GetImage("DifferentLighting", $"{imageName}.png");

            // Act
            var result = meterReader.Analyze(image);

            // Assert
            result.Result.Should().StartWith(expectedResult);
        }

        private Mat GetImage(string directory, string name)
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new Mat(Path.Combine($"{baseDir}/TestData", directory, name));
        }
    }
}