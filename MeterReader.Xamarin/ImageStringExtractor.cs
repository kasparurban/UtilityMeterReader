using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenCvSharp;
using Tesseract;

namespace MeterReader.Xamarin
{
    public class ImageStringExtractor
    {
        private readonly ITesseractApi _tesseractEngine;

        public ImageStringExtractor(ITesseractApi tesseractApi)
        {
            _tesseractEngine = tesseractApi;
        }
        
        public async Task Init()
        {
            await _tesseractEngine.Init("num");
            _tesseractEngine.SetWhitelist("0123456789");
        }
        
        public async Task<string> GetStringFromImage(Mat image)
        {
            await _tesseractEngine.SetImage(image.ToBytes());
            var result = Regex.Replace(_tesseractEngine.Text, @"\s+", "");
            _tesseractEngine.Clear();
            return result;
        }
    }
}
