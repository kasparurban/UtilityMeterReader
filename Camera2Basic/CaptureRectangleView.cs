using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Camera2Basic
{
    public class CaptureRectangleView : View
    {
        private Paint mPaint;

        public System.Drawing.Rectangle CaptureRectangle { get; private set; }

        public CaptureRectangleView(Context context):base(context)
        {
            // Set up the pen: 10pt, no fill
            mPaint = new Paint(PaintFlags.AntiAlias);
            mPaint.StrokeCap = Paint.Cap.Round;
            mPaint.StrokeWidth = 10.0f;
            mPaint.Color = Color.Green;
            mPaint.SetStyle(Paint.Style.Stroke);
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            var width = 550;
            var height = 220;
            CaptureRectangle = new System.Drawing.Rectangle(canvas.Width / 2 - width / 2, 300, width, height);
            canvas.DrawRect(CaptureRectangle.Left, CaptureRectangle.Top, CaptureRectangle.Right, CaptureRectangle.Bottom, mPaint);
        }

    }
}