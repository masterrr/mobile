using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.Widget;
using Android.Views;
using Toggl.Joey.UI.Fragments;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using Activity = Android.Support.V7.App.AppCompatActivity;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;

namespace Toggl.Joey.UI.Activities
{
    [Activity (Label = "ProjectListActivity",
               ScreenOrientation = ScreenOrientation.Portrait,
               Theme = "@style/Theme.Toggl.App")]
    public class ProjectListActivity : BaseActivity
    {
        public static readonly string ExtraTimeEntriesIds = "com.toggl.timer.time_entries_ids";
        private static readonly int ProjectCreatedRequestCode = 1;

        private ViewPager viewPager;
        private ProjectFragmentAdapter projectFragmentAdapter;
        private TabLayout tabLayout;
        private FloatingActionButton fab;
        private IList<TimeEntryData> timeEntryList;

        protected async override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);

            timeEntryList = await GetIntentTimeEntryData (Intent);
            if (timeEntryList.Count == 0) {
                Finish ();
            }

            SetContentView (Resource.Layout.ProjectListActivityLayout);
            projectFragmentAdapter = new ProjectFragmentAdapter (SupportFragmentManager, timeEntryList);
            viewPager = FindViewById<ViewPager> (Resource.Id.ProjectListViewPager);
            viewPager.Adapter = projectFragmentAdapter;
            tabLayout = FindViewById<TabLayout> (Resource.Id.WorkspaceTabLayout);
            tabLayout.SetupWithViewPager (viewPager);
            tabLayout.Visibility = projectFragmentAdapter.Count == 1 ? ViewStates.Gone : ViewStates.Visible;

            fab = FindViewById<AddProjectFab> (Resource.Id.AddNewProjectFAB);
            fab.Click += OnFABClick;

            var toolbar = FindViewById<Toolbar> (Resource.Id.ProjectListToolbar);
            SetSupportActionBar (toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            SupportActionBar.SetTitle (Resource.String.ChooseTimeEntryProjectDialogTitle);
        }

        private void OnFABClick (object sender, EventArgs e)
        {
            var entryList = new List<TimeEntryData> (timeEntryList);

            ChangeListWorkspace (entryList, projectFragmentAdapter.GetWorkspaceIdOfPosition (tabLayout.SelectedTabPosition));

            var intent = BaseActivity.CreateDataIntent<NewProjectActivity, List<TimeEntryData>>
                         (this, entryList, NewProjectActivity.ExtraTimeEntryDataListId);

            StartActivityForResult (intent, ProjectCreatedRequestCode);
        }

        private void ChangeListWorkspace (List<TimeEntryData> list, Guid wsId)
        {
            foreach (var entry in list ) {
                entry.WorkspaceId = wsId;
            }
        }

        private class ProjectFragmentAdapter : FragmentPagerAdapter
        {
            private List<Fragment> fragments = new List<Fragment> ();
            private List<String> fragmentTitles = new List<String> ();
            private List<WorkspaceData> workspaces;

            public ProjectFragmentAdapter (FragmentManager fm, IList<TimeEntryData> timeEntryList) : base (fm)
            {
                workspaces = WorkspaceProjectsView.GetWorkspaces().Result;
                foreach (var ws in workspaces) {
                    fragmentTitles.Add (ws.Name);
                    fragments.Add (new ProjectListFragment (timeEntryList, ws.Id));
                }

            }

            public override int Count
            {
                get {
                    return fragments.Count;
                }
            }

            public override Fragment GetItem (int position)
            {
                return fragments[position];
            }

            public override Java.Lang.ICharSequence GetPageTitleFormatted (int position)
            {
                return new Java.Lang.String (fragmentTitles[position]);
            }

            public Guid GetWorkspaceIdOfPosition (int position)
            {
                return workspaces [position].Id;
            }
        }

        public async static Task<IList<TimeEntryData>> GetIntentTimeEntryData (Android.Content.Intent intent)
        {
            var extras = intent.Extras;
            if (extras == null) {
                return new List<TimeEntryData> ();
            }

            // Get TimeEntryData from intent.
            var extraGuids = extras.GetStringArrayList (ExtraTimeEntriesIds);
            var timeEntryList = await TimeEntryGroup.GetTimeEntryDataList (extraGuids);
            return timeEntryList;
        }
    }
}

