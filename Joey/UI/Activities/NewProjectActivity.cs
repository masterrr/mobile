using Android.App;
using Android.Content.PM;
using Android.OS;
using Toggl.Joey.UI.Fragments;
using Activity = Android.Support.V7.App.AppCompatActivity;
using Fragment = Android.Support.V4.App.Fragment;

namespace Toggl.Joey.UI.Activities
{
    [Activity (Label = "NewProjectActivity",
               ScreenOrientation = ScreenOrientation.Portrait,
               Theme = "@style/Theme.Toggl.App")]
    public class NewProjectActivity : BaseActivity
    {
        public static readonly string ExtraTimeEntryDataListId = "com.toggl.timer.timeEntryDataList_id";

        protected override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);
            SetContentView (Resource.Layout.NewProjectActivity);

            SupportFragmentManager.BeginTransaction ()
            .Add (Resource.Id.NewProjectActivityLayout, NewProjectFragment.NewInstance ())
            .Commit ();
        }
    }
}

