using System;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace Camera2Basic
{
    public class NumberRectanglesView : View
    {
        private readonly CaptureRectangleView mCaptureView;
        private Paint mPaint;
        private Random mRandom = new Random();
        private MeterReader.Xamarin.Rectangle[] mRectangles = new MeterReader.Xamarin.Rectangle[0];
    
        public NumberRectanglesView(Context context, CaptureRectangleView captureView):base(context)
        {
            mPaint = new Paint(PaintFlags.AntiAlias);
            mPaint.StrokeCap = Paint.Cap.Round;
            mPaint.Color = Android.Graphics.Color.Blue;
            mPaint.StrokeWidth = 5.0f;
            mPaint.SetStyle(Paint.Style.Stroke);
            mCaptureView = captureView;
        }

        public void UpdateData(MeterReader.Xamarin.Rectangle[] rectangles)
        {
            this.mRectangles = rectangles;
            this.Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            foreach(var rect in mRectangles)
            {
                var captureRect = mCaptureView.CaptureRectangle;
                canvas.DrawRect(captureRect.X + rect.Left, captureRect.Y + rect.Top, captureRect.X + rect.Right, captureRect.Y + rect.Bottom, mPaint);
            }
        }

    }
}