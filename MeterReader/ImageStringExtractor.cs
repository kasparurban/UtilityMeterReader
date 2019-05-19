using System.Text.RegularExpressions;
using OpenCvSharp;
using Tesseract;

namespace MeterReader
{
    public class ImageStringExtractor
    {
        private TesseractEngine _tessEngine;

        public ImageStringExtractor()
        {
            _tessEngine = new TesseractEngine(@"App_Data", "num",
                EngineMode.Default);
            _tessEngine.SetVariable("tessedit_char_whitelist", "0123456789");
        }
        
        public string GetStringFromImage(Mat image)
        {
            using (var pix = Pix.LoadTiffFromMemory(image.ImEncode(".tiff")))
            {
                using (var page = _tessEngine.Process(pix))
                {
                    var result = Regex.Replace(page.GetText(), @"\s+", "");
                    return result;
                }
            }
        }
    }
}
