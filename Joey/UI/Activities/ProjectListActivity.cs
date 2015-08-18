using System.Collections.Generic;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Toggl.Joey.UI.Fragments;
using Activity = Android.Support.V7.App.AppCompatActivity;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;
using Toggl.Joey.UI.Views;

namespace Toggl.Joey.UI.Activities
{
    [Activity (Label = "ProjectListActivity",
               ScreenOrientation = ScreenOrientation.Portrait,
               Theme = "@style/Theme.Toggl.App")]
    public class ProjectListActivity : BaseActivity
    {
        private static readonly string fragmentTag = "projectlist_fragment";
        public static readonly string ExtraTimeEntriesIds = "com.toggl.timer.time_entries_ids";

        protected override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);
            SetContentView (Resource.Layout.ProjectListActivityLayout);

            // Check if fragment is still in Fragment manager.
            var fragment = FragmentManager.FindFragmentByTag (fragmentTag);

            if (fragment == null) {
                var extras = Intent.Extras;
                if (extras == null) {
                    Finish ();
                }

                var extraGuids = extras.GetStringArrayList (ExtraTimeEntriesIds);
                fragment = ProjectListFragment.NewInstance (extraGuids);
                FragmentManager.BeginTransaction ()
                .Add (Resource.Id.ProjectListActivityLayout, fragment, fragmentTag)
                .Commit ();
            } else {
                FragmentManager.BeginTransaction ()
                .Attach (fragment)
                .Commit ();
            }
        }
    }
}

