using OpenCvSharp;

namespace MeterReader.Xamarin
{
    public class ResizedImage
    {
        public ResizedImage(Mat image, float originalScale)
        {
            Image = image;
            OriginalScale = originalScale;
        }

        public float OriginalScale { get; }
        public Mat Image { get; }
    }
}