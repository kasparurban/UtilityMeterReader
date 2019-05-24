using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Camera2Basic
{
    public class ReadingAccuracyModule
    {
        public ReadingAccuracyModule(int accuracy)
        {
            Accuracy = accuracy;
        }

        public int Accuracy { get; }
        public bool AccuracyAchieved => (mReadings.GroupBy(x => x).OrderByDescending(x => x.Count()).FirstOrDefault()?.Count() ?? 0) >= Accuracy;

        private List<string> mReadings = new List<string>();

        public void AddReading(string reading)
        {
            mReadings.Add(reading);
        }

        public string GetReading()
        {
            var reading = mReadings.GroupBy(x => x).OrderByDescending(x => x.Count()).FirstOrDefault().Key ?? "-";
            mReadings.Clear();
            return reading;
        }
    }
}