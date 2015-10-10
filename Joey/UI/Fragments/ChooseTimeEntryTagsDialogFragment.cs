using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Toggl.Joey.UI.Adapters;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;

namespace Toggl.Joey.UI.Fragments
{
    public class ChooseTimeEntryTagsDialogFragment : BaseDialogFragment
    {
        private static readonly string TimeEntriesIdsArgument = "com.toggl.timer.time_entries_ids";
        private static readonly string WorkspaceIdArgument = "com.toggl.timer.workspace_id";
        private ListView listView;
        private TagListViewModel viewModel;

        public ChooseTimeEntryTagsDialogFragment ()
        {
        }

        public ChooseTimeEntryTagsDialogFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public ChooseTimeEntryTagsDialogFragment (Guid workspaceId, IList<TimeEntryData> timeEntryList)
        {
            var ids = timeEntryList.Select ( t => t.Id.ToString ()).ToList ();

            var args = new Bundle ();
            args.PutString (WorkspaceIdArgument, workspaceId.ToString ());
            args.PutStringArrayList (TimeEntriesIdsArgument, ids);
            Arguments = args;

            viewModel = new TagListViewModel (workspaceId, timeEntryList);
        }

        private IList<string> TimeEntryIds
        {
            get {
                return Arguments != null ? Arguments.GetStringArrayList (TimeEntriesIdsArgument) : new List<string>();
            }
        }

        private Guid WorkspaceId
        {
            get {
                var id = Guid.Empty;
                if (Arguments != null) {
                    Guid.TryParse (Arguments.GetString (WorkspaceIdArgument), out id);
                }
                return id;
            }
        }

        public async override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            if (viewModel == null) {
                var timeEntryList = await TimeEntryGroup.GetTimeEntryDataList (TimeEntryIds);
                viewModel = new TagListViewModel (WorkspaceId, timeEntryList);
            }

            viewModel.OnIsLoadingChanged += OnModelLoaded;
            viewModel.Init ();

            if (viewModel.Model.Workspace == null || viewModel.Model.Workspace.Id == Guid.Empty) {
                Dismiss ();
            }
        }

        private void OnModelLoaded (object sender, EventArgs e)
        {
            if (!viewModel.IsLoading) {
                if (viewModel.Model != null) {
                    viewModel.TagListDataView.Updated += OnWorkspaceTagsUpdated;
                    SelectInitialTags ();
                } else {
                    Dismiss ();
                }
            }
        }

        private void OnWorkspaceTagsUpdated (object sender, EventArgs args)
        {
            if (!viewModel.TagListDataView.IsLoading) {
                SelectInitialTags ();
            }
        }

        public override void OnDestroy ()
        {
            if (viewModel != null) {
                viewModel.OnIsLoadingChanged -= OnModelLoaded;
                viewModel.TagListDataView.Updated -= OnWorkspaceTagsUpdated;
                viewModel.Dispose ();
                viewModel = null;
            }

            base.OnDestroy ();
        }

        public override void OnStart ()
        {
            base.OnStart ();
            SelectInitialTags ();
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var dia = new AlertDialog.Builder (Activity)
            .SetTitle (Resource.String.ChooseTimeEntryTagsDialogTitle)
            .SetAdapter (new TagsAdapter (viewModel.TagListDataView), (IDialogInterfaceOnClickListener)null)
            .SetNegativeButton (Resource.String.ChooseTimeEntryTagsDialogCancel, OnCancelButtonClicked)
            .SetPositiveButton (Resource.String.ChooseTimeEntryTagsDialogOk, OnOkButtonClicked)
            .Create ();

            listView = dia.ListView;
            listView.ItemsCanFocus = false;
            listView.ChoiceMode = ChoiceMode.Multiple;
            listView.ItemClick += OnItemClick;

            return dia;
        }

        private void OnItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            if (e.Id == TagsAdapter.CreateTagId) {
                // Commit changes the user has made thusfar
                viewModel.SaveChanges (SelectedTags);

                CreateTagDialogFragment.NewInstace (WorkspaceId, viewModel.TimeEntryList).Show (FragmentManager, "new_tag_dialog");
                Dismiss ();
            }
        }

        private void SelectInitialTags ()
        {
            if (listView == null) {
                return;
            }

            // Select tags
            listView.ClearChoices ();
            foreach (var index in viewModel.SelectedTagsIndex) {
                listView.SetItemChecked (index, true);
            }
        }

        private void OnCancelButtonClicked (object sender, DialogClickEventArgs args)
        {
        }

        private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
        {
            viewModel.SaveChanges (SelectedTags);
        }

        private List<TagData> SelectedTags
        {
            get {
                var selected = listView.CheckedItemPositions;
                return viewModel.TagListDataView.Data
                       .Where ((tag, idx) => selected.Get (idx, false))
                       .ToList ();
            }
        }

    }
}
