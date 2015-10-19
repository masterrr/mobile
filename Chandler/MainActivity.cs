using Android.App;
using Android.OS;
using Android.Support.V4.App;
using Android.Widget;
using Android.Gms.Wearable;

namespace Chandler
{
    [Activity (Label = "Chandler", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity, IDataApiDataListener
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            var v = FindViewById<FrameLayout> (Resource.Id.watch_view_stub);
        }

        public void OnDataChanged (DataEventBuffer dataEvents)
        {
            throw new System.NotImplementedException ();
        }
    }
}



