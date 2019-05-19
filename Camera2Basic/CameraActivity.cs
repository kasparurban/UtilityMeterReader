using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content.PM;

[assembly: Application(LargeHeap = true)]
namespace Camera2Basic
{
	[Activity (Label = "Camera2Basic", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Portrait)]
	public class CameraActivity : Activity
	{
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			ActionBar.Hide ();
			SetContentView (Resource.Layout.activity_camera);

			if (bundle == null) {
				FragmentManager.BeginTransaction ().Replace (Resource.Id.container, Camera2BasicFragment.NewInstance ()).Commit ();
			}
		}
	}
}


