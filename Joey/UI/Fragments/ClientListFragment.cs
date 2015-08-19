using System;
using System.Collections.Generic;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Util;
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
    public class ClientListFragment : Fragment
    {
        private ActionBar Toolbar;
        private RecyclerView recyclerView;
        private ClientListViewModel viewModel;
        private Guid workspaceId;

        public ClientListFragment ()
        {
        }

        public ClientListFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public ClientListFragment (Guid workspaceId)
        {
            this.workspaceId = workspaceId;
            viewModel = new ClientListViewModel (this.workspaceId);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ClientListFragment, container, false);

            var activity = (Activity)Activity;

            var toolbar = view.FindViewById<Toolbar> (Resource.Id.ClientListToolbar);
            activity.SetSupportActionBar (toolbar);

            Toolbar = activity.SupportActionBar;
            Toolbar.SetDisplayHomeAsUpEnabled (true);
            Toolbar.SetTitle (Resource.String.SelectClientTitle);

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.ClientListRecyclerView);
            recyclerView.SetLayoutManager (new LinearLayoutManager (Activity));
            recyclerView.AddItemDecoration (new ShadowItemDecoration (Activity));
            recyclerView.AddItemDecoration (new DividerItemDecoration (Activity, DividerItemDecoration.VerticalList));

            HasOptionsMenu = true;

            return view;
        }

        public async override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            if (viewModel == null) {
                var workspaceIdInList = ClientListActivity.GetIntentWorkspaceId (Activity.Intent);
                workspaceId = workspaceIdInList [0];
                if (workspaceId == null || workspaceId == Guid.Empty) {
                    Activity.Finish ();
                    return;
                }

                viewModel = new ClientListViewModel (workspaceId);
            }

            var adapter = new ClientListAdapter (recyclerView, viewModel.ClientList);
            adapter.HandleClientSelection = OnItemSelected;

            recyclerView.SetAdapter (adapter);

            viewModel.OnIsLoadingChanged += OnModelLoaded;
            await viewModel.Init ();
        }

        private void OnModelLoaded (object sender, EventArgs e)
        {
            if (!viewModel.IsLoading) {
                if (viewModel.Model == null) {
//                    Activity.Finish ();
                }
            }
        }

        private async void OnItemSelected (object m)
        {
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home) {
                Activity.OnBackPressed ();
            }
            return base.OnOptionsItemSelected (item);
        }

        public override void OnActivityResult (int requestCode, int resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);
            if (requestCode == NewProjectFragment.ClientSelectedRequestCode) {
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
                viewModel.OnIsLoadingChanged -= OnModelLoaded;
                viewModel.Dispose ();
            }
            base.Dispose (disposing);
        }
    }
}
