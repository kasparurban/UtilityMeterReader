using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace MeterReader
{
    public class BlobDetector
    {
        public Rect GetLargestBlob(Mat img)
        {
            var rects = GetBoundingRectangles(img);
            return rects.OrderBy(r => Math.Abs(r.Area)).FirstOrDefault()?.Rectangle ?? new Rect(1, 1, 1, 1);
        }

        private BoundingRect[] GetBoundingRectangles(Mat threshold)
        {
            threshold.FindContours(out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0)
            {
                return new BoundingRect[0];
            }
            var whiteBlobContours = contours.Select(c => new {Contour = c, Area = Cv2.ContourArea(c, true)}).Where(c => c.Area <= 0);
            var boundingRectangles = whiteBlobContours.Select(c => new BoundingRect
            {
                Rectangle = Cv2.BoundingRect(c.Contour),
                Area = c.Area
            }).Where(rect => 
            {
                var size = rect.Rectangle.Width * rect.Rectangle.Height;
                return size >= 200 && size <= 3500;
            });
            return boundingRectangles.ToArray();
        }

        public Rectangle[] DetectMeterNumbers(Mat image)
        {

            var boundingRectangles = GetBoundingRectangles(image);

            var linedSameSizedRects = FilterLinedAndSameHeight(boundingRectangles.Select(r => r.Rectangle).ToArray());
            
            var rects = linedSameSizedRects.OrderBy(r => r.X)
                .Select(r => new Rectangle(r.X, r.Y, r.Width, r.Height))
                .ToArray();
            return FillMissingRectangles(rects);
        }

        public Rectangle[] FillMissingRectangles(Rectangle[] input)
        {
            if (input.Length < 3)
                return input;

            var correctDistance = (int)Enumerable.Range(0, input.Length - 1)
                .Select(i => input[i + 1].Center.X - input[i].Center.X)
                .OrderBy(x => x)
                .Take(Math.Max(1, (int)Math.Ceiling(input.Length / 2d)))
                .Average();

            var averageWidth = (int)input.Average(i => i.Width);
            var averageHeight = (int)input.Average(i => i.Height);

            var rects = new List<Rectangle>();
            for (var i = 0; i < input.Length; i++)
            {
                rects.Add(input[i]);
                if (i == input.Length - 1)
                {
                    break;
                }
                while ((input[i + 1].Center.X - rects.Last().Center.X) / (double)correctDistance > 1.5)
                {
                    if (rects.Count >= 20) break; // Safety
                    var currentBlob = rects.Last();
                    rects.Add(new Rectangle(currentBlob.Center.X + (correctDistance - averageWidth / 2), (input[i + 1].Position.Y + currentBlob.Position.Y) / 2, averageWidth, averageHeight));
                }
            }
            return rects.ToArray();
        }
        
        private Rect[] FilterLinedAndSameHeight(Rect[] input)
        {
            var yGroups = new List<BlobGroup>();
            foreach (var rect in input)
            {
                var nearBlobs = yGroups.FirstOrDefault(g =>
                    Math.Abs(g.Y - rect.Top) < 15 && Math.Abs(g.Height - rect.Height) < g.Height * 0.3);
                if (nearBlobs != null)
                {
                    nearBlobs.Blobs.Add(rect);
                }
                else
                {
                    yGroups.Add(new BlobGroup(rect.Top, rect.Height, rect));
                }
            }

            if (!yGroups.Any())
            {
                return new Rect[0];
            }

            var result = yGroups.Where(g => g.Blobs.Count() > 4).OrderByDescending(b => b.TotalSize).ToArray();
            return !result.Any() ? new Rect[0] : result.First().Blobs.ToArray();
        }

        private class BoundingRect
        {
            public Rect Rectangle { get; set; }
            public double Area { get; set; }
        }
    }

    public class Rectangle
    {
        public Point Position { get; set; }
        public int Width { get; }
        public int Height { get; }

        public int Top => Position.Y;
        public int Bottom => Position.Y + Height;
        public int Left => Position.X;
        public int Right => Position.X + Width;
        public Point Center { get; }


        public Rectangle(int x, int y, int width, int height)
        {
            Position = new Point((int) (x), (int) (y));
            Width = (int) (width);
            Height = (int) (height);
            Center = new Point((int) ((x + width / 2)), (int) ((y + height / 2)));
        }
    }

    public class Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    public class BlobGroup
    {
        public BlobGroup(int y, int height, params Rect[] blobs)
        {
            Y = y;
            Height = height;
            Blobs.AddRange(blobs);
        }

        public int Y { get; private set; }
        public int Height { get; private set; }
        public List<Rect> Blobs { get; set; } = new List<Rect>();
        public int TotalSize => (int)Blobs.Sum(b => b.Width * b.Height);
    }
}