using System;
using System.Linq;
using OpenCvSharp;
using Rect = OpenCvSharp.Rect;

namespace MeterReader
{
    public class MeterReader
    {
        private readonly MeterReaderSettings _settings;
        private readonly ImageStringExtractor _stringExtractor;

        public MeterReader(MeterReaderSettings settings)
        {
            _settings = settings;
            _stringExtractor = new ImageStringExtractor();
        }

        public void ReverseNumberColors()
        {
            _settings.DarkSectors = !_settings.DarkSectors;
        }

        public Reading Analyze(Mat input)
        {
            var resized = input.ResizePreserveAspectRatio(maxSize: _settings.InputAnalysisMaxSize);
            var grayScale = resized.Image.CvtColor(ColorConversionCodes.BGR2GRAY);
            
            var noiseReduced = grayScale.Erode(new Mat()).Dilate(new Mat());
            
            var meterNumberRectangles = DetectMeterNumbers(noiseReduced);

            if (!meterNumberRectangles.Any())
            {
                return new Reading("");
            }

            Mat[] croppedImages;
            if (_settings.DarkSectors)
            {
                croppedImages = meterNumberRectangles.Select(r =>
                {
                    var rect = new Rect(r.Position.X, r.Position.Y, r.Width, r.Height);
                    return new Mat(noiseReduced, rect)
                        .Threshold(0, 255, ThresholdTypes.Otsu)
                        .Erode(new Mat())
                        .ResizePreserveAspectRatio(_settings.DarkSectorsSeparateMeterNumbersResizeMaxSize).Image;
                }).ToArray();
            }
            else
            {
                croppedImages = meterNumberRectangles.Select(r =>
                {
                    var rect = new Rect(r.Position.X, r.Position.Y, r.Width, r.Height);
                    return new Mat(noiseReduced, rect)
                        .AdaptiveThreshold(255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, _settings.LightSectorsAdaptiveThresholdBlockSize, _settings.LightSectorsAdaptiveThresholdC)
                        .ResizePreserveAspectRatio(_settings.LightSectorsSeparateMeterNumbersResizeMaxSize).Image;
                }).ToArray();
                
                croppedImages = croppedImages.Select(i =>
                {
                    var rect = new BlobDetector().GetLargestBlob(i);
                    return new Mat(i, rect);
                }).ToArray();
            }
            
            var resizedCroppedNumbers = croppedImages.ResizeToAverageHeight();

            var combinedNumbers = resizedCroppedNumbers.Skip(1)
                .Aggregate(croppedImages[0], (a, b) => a.CombineImages(b), r => r);

            var numbers = combinedNumbers.WidenEdges(10);
            if (!_settings.DarkSectors)
            {
                Cv2.BitwiseNot(numbers, numbers);
            }

            var reading = new Reading(InsertDecimal(_stringExtractor.GetStringFromImage(numbers)),
                GetScaledRectangles(resized.OriginalScale, meterNumberRectangles));
            return reading;
        }

        private Rectangle[] DetectMeterNumbers(Mat image)
        {
            var blobDetector = new BlobDetector();

            var threshold = image.AdaptiveThreshold(255, 
                AdaptiveThresholdTypes.GaussianC, 
                _settings.DarkSectors ? ThresholdTypes.Binary : ThresholdTypes.BinaryInv, 
                _settings.NumbersDetectionAdaptiveThresholdBlockSize, 
                _settings.NumbersDetectionAdaptiveThresholdC);
            return blobDetector.DetectMeterNumbers(threshold).ToArray();
        }

        private Rectangle[] GetScaledRectangles(float scale, Rectangle[] input)
        {
            return input.Select(r => new Rectangle((int) Math.Ceiling(r.Position.X / scale), 
                (int) Math.Ceiling(r.Position.Y / scale), 
                (int) Math.Ceiling(r.Width / scale), 
                (int) Math.Ceiling(r.Height / scale))).ToArray();
        }

        private string InsertDecimal(string inputReading)
        {
            if (inputReading.Length <= _settings.FullPartSectors)
            {
                return inputReading;
            }
            return inputReading.Insert(_settings.FullPartSectors, ",");
        }
    }
}