using OpenCvSharp;

namespace MeterReader
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