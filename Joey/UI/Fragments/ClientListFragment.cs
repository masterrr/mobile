using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Toggl.Joey.UI.Adapters;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using ActionBar = Android.Support.V7.App.ActionBar;
using Activity = Android.Support.V7.App.AppCompatActivity;
using Fragment = Android.Support.V4.App.Fragment;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace Toggl.Joey.UI.Fragments
{
    public class ClientListFragment : BaseDialogFragment
    {
        private RecyclerView recyclerView;
        private ClientListViewModel viewModel;
        private ClientListAdapter adapter;
        private Guid workspaceId;
        private NewProjectViewModel projectModel;

        public ClientListFragment ()
        {
        }

        public ClientListFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public ClientListFragment (Guid workspaceId, NewProjectViewModel projectModel)
        {
            this.workspaceId = workspaceId;
            this.projectModel = projectModel;
            Console.WriteLine ("workspaceId: {0}", workspaceId);
            viewModel = new ClientListViewModel (this.workspaceId);
        }

        public async override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            recyclerView = new RecyclerView (Activity);
            recyclerView.SetLayoutManager (new LinearLayoutManager (Activity));
            recyclerView.AddItemDecoration (new ShadowItemDecoration (Activity));
            recyclerView.AddItemDecoration (new DividerItemDecoration (Activity, DividerItemDecoration.VerticalList));

            if (viewModel == null) {
                viewModel = new ClientListViewModel (workspaceId);
            }

            adapter = new ClientListAdapter (recyclerView, viewModel.ClientList);
            adapter.HandleClientSelection = OnItemSelected;

            recyclerView.SetAdapter (adapter);

            viewModel.OnIsLoadingChanged += OnModelLoaded;
            await viewModel.Init ();

        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            return new AlertDialog.Builder (Activity)
                   .SetTitle (Resource.String.SelectClientTitle)
                   .SetNegativeButton (Resource.String.ChooseTimeEntryTagsDialogCancel, OnCancelButtonClicked)
                   .SetPositiveButton (Resource.String.ChooseTimeEntryTagsDialogOk, OnOkButtonClicked)
                   .SetView (recyclerView)
                   .Create ();
        }

        private void OnCancelButtonClicked (object sender, DialogClickEventArgs args)
        {
        }

        private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
        {
        }

        public override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);
        }

        private void OnModelLoaded (object sender, EventArgs e)
        {
            if (!viewModel.IsLoading) {
                if (viewModel.Model == null) {
                }
            }
        }

        private void OnItemSelected (object m)
        {
            var client = (WorkspaceClientsView.Client)m;
            if (client.IsNewClient) {
                new CreateClientDialogFragment (projectModel.Model).Show (FragmentManager, "new_client_dialog");
            } else {
                projectModel.Model.Client =  new ClientModel (((WorkspaceClientsView.Client)m).Data);
            }
            Dismiss ();
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
