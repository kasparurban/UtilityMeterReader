using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace TesseractTraining
{
    public class TessTraining
    {
        private readonly int _testImageCount;
        private readonly string _sampleDir;
        private readonly string _destinationDir;
        private readonly string _tesseractExecutables;
        private Random _random;

        public TessTraining(int testImageCount, string sampleDir, string destinationDir, string tesseractExecutables)
        {
            _testImageCount = testImageCount;
            _sampleDir = sampleDir;
            _destinationDir = destinationDir;
            _tesseractExecutables = tesseractExecutables;
            _random = new Random();
        }
        
        public void TrainTesseract()
        {
            var numbers = new List<Mat>();
            for (int i = 0; i < 10; i++)
            {
                var mat = new Mat($@"{_sampleDir}\{i}.PNG");
                numbers.Add(mat);
            }
            
            var random = new Random();
            
            for (int i = 0; i < _testImageCount; i++)
            {
                var randomized = Enumerable.Range(0, 300).Select(index => numbers[_random.Next(0, numbers.Count)]).ToArray();
                var randomizedNumbers = randomized.Select(m => numbers.IndexOf(m)).ToArray();

                var thresholded = randomized.Select(GetRandomThreshold).ToArray();
                var combinedNumbers = thresholded.Skip(1)
                    .Aggregate(thresholded[0], CombineImages, r => r);

                var result = WidenEdges(combinedNumbers, 5);
                Cv2.BitwiseNot(result, result);

                result.SaveImage($@"{_testImageCount}\num.meter.exp{i}.png");
                CreateBox(i);
                FixTessBoxes(i, randomizedNumbers);
            }
            
            Train();
            CreateUnicharSetFile();
            ShapeClustering();
            Shapetable();
            Normproto();
            RenameFiles();
            Combine();
        }
        
        private void CreateBox(int i)
        {
            ExecuteTesseract("tesseract.exe",$@" num.meter.exp{i}.png num.meter.exp{i} batch.nochop makebox");
        }

        private void FixTessBoxes(int index, int[] numbers)
        {
            var file = File.ReadLines($@"{_destinationDir}\num.meter.exp{index}.box");
            var fixedLines = file.Select((l, i) =>
            {
                var sb = new StringBuilder(l);
                sb[0] = numbers[i].ToString()[0];
                return sb.ToString();
            }).ToArray();
            File.WriteAllLines($@"{_destinationDir}\num.meter.exp{index}.box", fixedLines);
        }

        private void Train()
        {
            for (int i = 0; i < _testImageCount; i++)
            {
                ExecuteTesseract("tesseract.exe", $@" num.meter.exp{i}.png num.meter.exp{i} box.train");
            }
        }

        private void CreateUnicharSetFile()
        {
            var commandArguments = Enumerable.Range(0, _testImageCount).Select(i => $"num.meter.exp{i}.box").ToArray();
            ExecuteTesseract("unicharset_extractor.exe", string.Join(" ", commandArguments));
        }

        private void ShapeClustering()
        {
            var commandArguments = Enumerable.Range(0, _testImageCount).Select(i => $"num.meter.exp{i}.tr").ToArray();
            ExecuteTesseract("shapeclustering.exe", " -F num.font_properties -U unicharset " + string.Join(" ", commandArguments));
        }
        
        private void Shapetable()
        {
            var commandArguments = Enumerable.Range(0, _testImageCount).Select(i => $"num.meter.exp{i}.tr").ToArray();
            ExecuteTesseract("mftraining.exe", " -F num.font_properties -U unicharset " + string.Join(" ", commandArguments));
        }
        
        private void Normproto()
        {
            var commandArguments = Enumerable.Range(0, _testImageCount).Select(i => $"num.meter.exp{i}.tr").ToArray();
            ExecuteTesseract("cntraining.exe", string.Join(" ", commandArguments));
        }

        private void RenameFiles()
        {
            var path = $@"{_destinationDir}\";
            var allFiles = Directory.GetFiles(path)
                .Where(f => !f.Contains("num.")).ToArray();

            foreach (var allFile in allFiles)
            {
                File.Move(allFile, allFile.Replace(path, $"{path}num."));
            }
        }
        
        private void Combine()
        {
            ExecuteTesseract("combine_tessdata.exe", " num.");
        }
        
        private void ExecuteTesseract(string executable, string command)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = $@"{_tesseractExecutables}\{executable}";
            startInfo.WorkingDirectory = $@"{_destinationDir}\";
            startInfo.UseShellExecute = false;
            startInfo.Arguments = command;
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }
        
        private Mat GetRandomThreshold(Mat input)
        {
            var threshold = input.Threshold(0, 255, ThresholdTypes.Otsu);
            var randomNumber = _random.Next(0, 100);
            if (randomNumber < 33)
            {
                return threshold.Erode(new Mat());
            }
            if (randomNumber < 77)
            {
                return threshold.Dilate(new Mat());
            }
            return threshold;
        }
        
        private Mat CombineImages(Mat image1, Mat image2)
        {
            var imageHeight = Math.Max(image1.Height, image2.Height);
            var imageWidth = image1.Width + image2.Width + 8;

            var final = new Mat(new Size(imageWidth, imageHeight), image1.Type()).SetTo(Scalar.Black);
            var roi1 = new Rect(0, 0, image1.Width, image1.Height);
            var roi2 = new Rect(image1.Width + 4, 0, image2.Width, image2.Height);
            image1.CopyTo(new Mat(final, roi1));
            image2.CopyTo(new Mat(final, roi2));
            return final;
        }

        private Mat WidenEdges(Mat input, int pixels)
        {
            var final = new Mat(new Size(input.Width + pixels, input.Height + pixels), input.Type())
                .SetTo(Scalar.Black);
            var roi = new Rect(pixels / 2, pixels / 2, input.Width, input.Height);
            input.CopyTo(new Mat(final, roi));
            return final;
        }
    }
}