using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Widget;
using Praeclarum.Bind;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;

namespace Toggl.Joey.UI.Fragments
{
    public class CreateTagDialogFragment : BaseDialogFragment
    {
        private static readonly string WorkspaceIdArgument = "com.toggl.timer.workspace_id";
        private static readonly string TimeEntriesIdsArgument = "com.toggl.timer.time_entry_ids";

        private EditText nameEditText;
        private Button positiveButton;
        private CreateTagViewModel viewModel;
        private Binding binding;

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

        private IList<string> TimeEntryIds
        {
            get {
                return Arguments.GetStringArrayList (TimeEntriesIdsArgument);
            }
        }

        public CreateTagDialogFragment ()
        {
        }

        public CreateTagDialogFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public static CreateTagDialogFragment NewInstace (Guid workspaceId, IList<TimeEntryData> timeEntryList)
        {
            var fragment = new CreateTagDialogFragment ();

            var ids = timeEntryList.Select ( t => t.Id.ToString ()).ToList ();
            var args = new Bundle ();
            args.PutString (WorkspaceIdArgument, workspaceId.ToString ());
            args.PutStringArrayList (TimeEntriesIdsArgument, ids);
            fragment.Arguments = args;

            return fragment;
        }

        public async override void OnViewCreated (Android.Views.View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            var timeEntryList = await TimeEntryGroup.GetTimeEntryDataList (TimeEntryIds);

            viewModel = new CreateTagViewModel (WorkspaceId, timeEntryList);
            binding = Binding.Create (() => nameEditText.Text == viewModel.TagName);
            await viewModel.Init ();

            ValidateTagName ();
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            nameEditText = new EditText (Activity);
            nameEditText.SetHint (Resource.String.CreateTagDialogHint);
            nameEditText.InputType = InputTypes.TextFlagCapSentences;
            nameEditText.TextChanged += OnNameEditTextTextChanged;

            return new AlertDialog.Builder (Activity)
                   .SetTitle (Resource.String.CreateTagDialogTitle)
                   .SetView (nameEditText)
                   .SetPositiveButton (Resource.String.CreateTagDialogOk, OnPositiveButtonClicked)
                   .Create ();
        }

        public override void OnStart ()
        {
            base.OnStart ();
            positiveButton = ((AlertDialog)Dialog).GetButton ((int)DialogButtonType.Positive);
            ValidateTagName ();
        }

        public override void OnDestroy ()
        {
            binding.Unbind ();
            viewModel.Dispose ();
            base.OnDestroy ();
        }

        private void OnNameEditTextTextChanged (object sender, TextChangedEventArgs e)
        {
            ValidateTagName ();
        }

        private async void OnPositiveButtonClicked (object sender, DialogClickEventArgs e)
        {
            await viewModel.AssignTag (nameEditText.Text);
        }

        private void ValidateTagName ()
        {
            if (positiveButton == null || nameEditText == null) {
                return;
            }

            var valid = true;
            var name = nameEditText.Text;

            if (String.IsNullOrWhiteSpace (name)) {
                valid = false;
            }

            positiveButton.Enabled = valid;
        }
    }
}
