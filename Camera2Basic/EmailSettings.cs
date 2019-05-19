using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Camera2Basic
{
    public class EmailSettings
    {
        public string Recipient { get; set; }
        public string Subject { get; set; }
        public string MessageTemplate { get; set; } = "Tere\n\nSaadan korteri 1 veenäidud\n\nKülm: {0}\nSoe: {1}\n\nLugupidamisega\nKasutaja";
        public int NumberOfRequiredReadings => Regex.Matches(MessageTemplate, "{.}").Count;

        public string GetMessage(string[] readings)
        {
            return string.Format(MessageTemplate, readings);
        }
    }
}