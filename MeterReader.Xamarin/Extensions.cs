using System;
using System.Linq;
using OpenCvSharp;

namespace MeterReader.Xamarin
{
    public static class Extensions
    {
        internal static ResizedImage ResizePreserveAspectRatio(this Mat mat, float maxSize)
        {
            var scale = Math.Min(maxSize / mat.Width, maxSize / mat.Height);
            return new ResizedImage(mat.Resize(new Size(mat.Width * scale, mat.Height * scale)), scale);
        }
        
        internal static Mat[] ResizeToAverageHeight(this Mat[] input)
        {
            if (!input.Any())
            {
                return input;
            }

            var averageHeight = (int) Math.Ceiling(input.Average(i => i.Height));
            return input.Select(i => i.ResizePreserveAspectRatio(averageHeight).Image).ToArray();
        }

        internal static Mat CombineImages(this Mat image1, Mat image2)
        {
            var imageHeight = Math.Max(image1.Height, image2.Height);
            var imageWidth = image1.Width + image2.Width + 12;

            var final = new Mat(new Size(imageWidth, imageHeight), image1.Type()).SetTo(Scalar.Black);
            var roi1 = new Rect(0, 0, image1.Width, image1.Height);
            var roi2 = new Rect(image1.Width + 6, 0, image2.Width, image2.Height);
            image1.CopyTo(new Mat(final, roi1));
            image2.CopyTo(new Mat(final, roi2));
            return final;
        }

        internal static Mat WidenEdges(this Mat input, int pixels)
        {
            var final = new Mat(new Size(input.Width + pixels, input.Height + pixels), input.Type()).SetTo(Scalar.Black);
            var roi = new Rect(pixels / 2, pixels / 2, input.Width, input.Height);
            input.CopyTo(new Mat(final, roi));
            return final;
        }
    }
}