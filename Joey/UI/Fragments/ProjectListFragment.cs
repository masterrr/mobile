using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Adapters;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using ActionBar = Android.Support.V7.App.ActionBar;
using Activity = Android.Support.V7.App.AppCompatActivity;
using Fragment = Android.Support.V4.App.Fragment;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace Toggl.Joey.UI.Fragments
{
    public class ProjectListFragment : Fragment, Toolbar.IOnMenuItemClickListener, TabLayout.IOnTabSelectedListener
    {
        private static readonly string TimeEntryIdsArg = "time_entries_ids_param";
        private static readonly int ProjectCreatedRequestCode = 1;

        private RecyclerView recyclerView;
        private TabLayout tabLayout;
        private Toolbar toolBar;
        private FloatingActionButton newProjectFab;
        private LinearLayout emptyStateLayout;

        private ProjectListViewModel viewModel;

        private IList<string> TimeEntryIds
        {
            get {
                return Arguments.GetStringArrayList (TimeEntryIdsArg);
            }
        }

        public ProjectListFragment ()
        {
        }

        public ProjectListFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public static ProjectListFragment NewInstance (IList<string> timeEntryIds)
        {
            var fragment = new ProjectListFragment ();

            var args = new Bundle ();
            args.PutStringArrayList (TimeEntryIdsArg, timeEntryIds);
            fragment.Arguments = args;

            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ProjectListFragment, container, false);

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.ProjectListRecyclerView);
            recyclerView.SetLayoutManager (new LinearLayoutManager (Activity));
            recyclerView.AddItemDecoration (new ShadowItemDecoration (Activity));
            recyclerView.AddItemDecoration (new DividerItemDecoration (Activity, DividerItemDecoration.VerticalList));

            emptyStateLayout = view.FindViewById<LinearLayout> (Resource.Id.ProjectListEmptyState);
            tabLayout = view.FindViewById<TabLayout> (Resource.Id.WorkspaceTabLayout);
            newProjectFab = view.FindViewById<AddProjectFab> (Resource.Id.AddNewProjectFAB);
            toolBar = view.FindViewById<Toolbar> (Resource.Id.ProjectListToolbar);

            var activity = (Activity)Activity;
            activity.SetSupportActionBar (toolBar);
            activity.SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            activity.SupportActionBar.SetTitle (Resource.String.ChooseTimeEntryProjectDialogTitle);

            HasOptionsMenu = true;
            newProjectFab.Click += OnNewProjectFabClick;
            tabLayout.SetOnTabSelectedListener (this);

            return view;
        }

        public async override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            viewModel = new ProjectListViewModel (TimeEntryIds);

            var adapter = new ProjectListAdapter (recyclerView, viewModel.ProjectList);
            adapter.HandleProjectSelection = OnItemSelected;
            recyclerView.SetAdapter (adapter);

            viewModel.OnIsLoadingChanged += OnDataLoaded;
            viewModel.ProjectList.OnIsLoadingChanged += OnDataLoaded;

            await viewModel.Init ();
        }

        private void OnDataLoaded (object sender, EventArgs e)
        {
            if (!viewModel.IsLoading) {
                if (viewModel.Model == null) {
                    Activity.Finish ();
                }
            }

            if (viewModel.ProjectList.IsLoading || viewModel.IsLoading) {
                return;
            }

            EnsureCorrectState ();

            // Create tabs
            if (viewModel.ProjectList.Workspaces.Count > 1) {
                int i = 0;
                foreach (var ws in viewModel.ProjectList.Workspaces) {
                    var tab = tabLayout.NewTab().SetText (ws.Data.Name);
                    tabLayout.AddTab (tab);
                    try {
                        if (ws.Data.Id == viewModel.TimeEntryList[0].WorkspaceId) {
                            viewModel.ProjectList.CurrentWorkspaceIndex = i;
                            tab.Select();
                        }
                    } catch (Exception ex) {
                        Console.WriteLine ("here!");
                    }

                    i++;
                }
            }
        }

        private void EnsureCorrectState ()
        {
            // Set toolbar scrollable or not.
            var _params = new AppBarLayout.LayoutParams (toolBar.LayoutParameters);

            if (viewModel.ProjectList.Workspaces.Count > 1) {
                tabLayout.Visibility = ViewStates.Visible;
                _params.ScrollFlags  = AppBarLayout.LayoutParams.ScrollFlagScroll | AppBarLayout.LayoutParams.ScrollFlagEnterAlways;
            } else {
                tabLayout.Visibility = ViewStates.Gone;
                _params.ScrollFlags = 0;
            }

            toolBar.LayoutParameters = _params;
            recyclerView.Visibility = viewModel.ProjectList.IsEmpty ? ViewStates.Gone : ViewStates.Visible;
            emptyStateLayout.Visibility = viewModel.ProjectList.IsEmpty ? ViewStates.Visible : ViewStates.Gone;
        }

        private void OnNewProjectFabClick (object sender, EventArgs e)
        {
            var entryList = new List<TimeEntryData> (viewModel.TimeEntryList);
            ChangeListWorkspace (entryList, viewModel.ProjectList.Workspaces[viewModel.ProjectList.CurrentWorkspaceIndex].Data.Id);

            // Show create project activity instead
            var intent = BaseActivity.CreateDataIntent<NewProjectActivity, List<TimeEntryData>>
                         (Activity, entryList, NewProjectActivity.ExtraTimeEntryDataListId);
            StartActivityForResult (intent, ProjectCreatedRequestCode);
        }

        private void ChangeListWorkspace (List<TimeEntryData> list, Guid wsId)
        {
            foreach (var entry in list) {
                entry.WorkspaceId = wsId;
            }
        }

        private async void OnItemSelected (object m)
        {
            ProjectModel project = null;
            WorkspaceModel workspace = null;
            TaskData task = null;

            if (m is WorkspaceProjectsView.Project) {
                var wrap = (WorkspaceProjectsView.Project)m;
                if (wrap.IsNoProject) {
                    workspace = new WorkspaceModel (wrap.WorkspaceId);
                } else if (wrap.IsNewProject) {
                    // Show create project activity instead
                    var entryList = new List<TimeEntryData> (viewModel.TimeEntryList);
                    var intent = BaseActivity.CreateDataIntent<NewProjectActivity, List<TimeEntryData>>
                                 (Activity, entryList, NewProjectActivity.ExtraTimeEntryDataListId);
                    StartActivityForResult (intent, ProjectCreatedRequestCode);
                } else {
                    project = (ProjectModel)wrap.Data;
                    workspace = project.Workspace;
                }
            } else if (m is ProjectAndTaskView.Workspace) {
                var wrap = (ProjectAndTaskView.Workspace)m;
                workspace = (WorkspaceModel)wrap.Data;
            } else if (m is TaskData) {
                task = (TaskData)m;
                project = new ProjectModel (task.ProjectId);
                workspace = new WorkspaceModel (task.WorkspaceId);
            }

            if (project != null || workspace != null) {
                await viewModel.SaveModelAsync (project, workspace, task);
                Activity.Finish ();
            }
        }

        public override void OnActivityResult (int requestCode, int resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            if (requestCode == ProjectCreatedRequestCode) {
                if (resultCode == (int)Result.Ok) {
                    Activity.Finish();
                }
            }
        }

        public override void OnDestroyView ()
        {
            Dispose (true);
            base.OnDestroyView ();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                viewModel.ProjectList.OnIsLoadingChanged -= OnDataLoaded;
                viewModel.OnIsLoadingChanged -= OnDataLoaded;
                viewModel.Dispose ();
            }
            base.Dispose (disposing);
        }

        #region Option menu
        public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
        {
            base.OnCreateOptionsMenu (menu, inflater);
            inflater.Inflate (Resource.Menu.ProjectListToolbarMenu, menu);
            toolBar.SetOnMenuItemClickListener (this);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home) {
                Activity.OnBackPressed ();
            }
            return base.OnOptionsItemSelected (item);
        }

        public bool OnMenuItemClick (IMenuItem item)
        {
            switch (item.ItemId) {
            case Resource.Id.SortByClients:
                viewModel.ProjectList.SortBy = WorkspaceProjectsView.SortProjectsBy.Clients;
                return true;
            case Resource.Id.SortByProjects:
                viewModel.ProjectList.SortBy = WorkspaceProjectsView.SortProjectsBy.Projects;
                return true;
            }
            return false;
        }
        #endregion

        #region Workspace Tablayout
        public void OnTabSelected (TabLayout.Tab tab)
        {
            viewModel.ProjectList.CurrentWorkspaceIndex = tab.Position;
            EnsureCorrectState();
        }

        public void OnTabReselected (TabLayout.Tab tab)
        {
        }

        public void OnTabUnselected (TabLayout.Tab tab)
        {
        }
        #endregion
    }
}

