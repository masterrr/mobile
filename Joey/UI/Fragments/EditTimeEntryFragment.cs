using System;
using System.Collections.Generic;
using Android.Content;
using Android.OS;
using Android.Views;
using Toggl.Joey.UI.Activities;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Views;
using Fragment = Android.Support.V4.App.Fragment;
using System.Threading.Tasks;

namespace Toggl.Joey.UI.Fragments
{
    public class EditTimeEntryFragment : BaseEditTimeEntryFragment
    {
        private static readonly string TimeEntryIdArgument = "com.toggl.timer.time_entry_id";
        private static readonly string UseDraftKey = "com.toggl.timer.draft_used";
        private EditTimeEntryView viewModel;

        private Guid TimeEntryId
        {
            get {
                var id = Guid.Empty;
                if (Arguments != null) {
                    Guid.TryParse (Arguments.GetString (TimeEntryIdArgument), out id);
                }
                return id;
            }
        }

        public EditTimeEntryFragment ()
        {
        }

        public EditTimeEntryFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public static EditTimeEntryFragment NewInstance (string timeEntryId)
        {
            var fragment = new EditTimeEntryFragment ();

            if (!string.IsNullOrEmpty (timeEntryId)) {
                var bundle = new Bundle ();
                bundle.PutString (TimeEntryIdArgument, timeEntryId);
                fragment.Arguments = bundle;
            }

            return fragment;
        }

        public override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            bool useDraft = false;
            if (savedInstanceState != null) {
                useDraft = savedInstanceState.GetBoolean (UseDraftKey, useDraft);
            }

            viewModel = new EditTimeEntryView (TimeEntryId);
            viewModel.OnIsLoadingChanged += OnModelLoaded;
            viewModel.Init (useDraft);

            // for manual entries, show Save btn
            HasOptionsMenu = (TimeEntryId == Guid.Empty);
        }

        public override void OnDestroyView ()
        {
            if (viewModel != null) {
                // TimeEntry property must be nullified to
                // stop event listeners on BaseEditTimeEntryFragment.
                TimeEntry = null;
                viewModel.OnIsLoadingChanged -= OnModelLoaded;
                viewModel.OnModelChanged -= OnModelChanged;
                viewModel.Dispose ();
            }
            base.OnDestroyView ();
        }

        private void OnModelLoaded (object sender, EventArgs e)
        {
            if (!viewModel.IsLoading) {
                if (viewModel != null) {
                    TimeEntry = viewModel.Model;
                    viewModel.OnModelChanged += OnModelChanged;
                    OnPressedProjectSelector += OnProjectSelected;
                    OnPressedTagSelector += OnTagSelected;
                } else {
                    Activity.Finish ();
                }
            }
        }

        private void OnModelChanged (object sender, EventArgs e)
        {
            TimeEntry = viewModel.Model;
        }

        private void OnProjectSelected (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            var intent = new Intent (Activity, typeof (ProjectListActivity));
            intent.PutStringArrayListExtra (ProjectListActivity.ExtraTimeEntriesIds, new List<string> {TimeEntry.Id.ToString ()});
            StartActivity (intent);
        }

        private void OnTagSelected (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }
            new ChooseTimeEntryTagsDialogFragment (TimeEntry.Workspace.Id, new List<TimeEntryData> {TimeEntry.Data}).Show (FragmentManager, "tags_dialog");
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            outState.PutBoolean (UseDraftKey, viewModel.IsDraft);
            base.OnSaveInstanceState (outState);
        }

        public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
        {
            menu.Add (Resource.String.NewProjectSaveButtonText).SetShowAsAction (ShowAsAction.Always);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home) {
                Activity.OnBackPressed ();
            } else {
                Task.Run (async () => {
                    await viewModel.StoreTimeEntryModel ();
                });
            }
            return base.OnOptionsItemSelected (item);
        }

        protected override void ResetModel ()
        {
            viewModel.ResetModel ();
        }
    }
}
