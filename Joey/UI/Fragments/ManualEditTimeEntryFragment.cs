using System;
using System.Collections.Generic;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Logging;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using Toggl.Phoebe;
using Toggl.Phoebe.Data.DataObjects;
using System.ComponentModel;
using Toggl.Phoebe.Data.Utils;

namespace Toggl.Joey.UI.Fragments
{
    public class ManualEditTimeEntryFragment : Fragment
    {
        private readonly Handler handler = new Handler ();
        private TimeEntryTagsView tagsView;
        private ITimeEntryModel model;
        private EditTimeEntryView viewModel;
        private ActiveTimeEntryManager timeEntryManager;
        private ITimeEntryModel backingActiveTimeEntry;
        private PropertyChangeTracker propertyTracker;
        private bool canRebind;
        private bool descriptionChanging;
        private bool autoCommitScheduled;

        public ManualEditTimeEntryFragment ()
        {
            Initialize();
        }

        public ManualEditTimeEntryFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
            Initialize();
        }

        private void Initialize()
        {
            propertyTracker = new PropertyChangeTracker ();

            if (timeEntryManager == null) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
                timeEntryManager.PropertyChanged += OnActiveTimeEntryManagerPropertyChanged;
            }

            TimeEntry = ActiveTimeEntry;
            canRebind = true;
        }

        private void OnActiveTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            TimeEntry = ActiveTimeEntry;
            if (args.PropertyName == ActiveTimeEntryManager.PropertyActive) {
                if (SyncModel ()) {
                    Rebind ();
                }
            }
        }

        private void ResetTrackedObservables ()
        {
            if (propertyTracker == null) {
                return;
            }

            propertyTracker.MarkAllStale ();

            var model = ActiveTimeEntry;
            if (model != null) {
                propertyTracker.Add (model, HandleTimeEntryPropertyChanged);
            }

            propertyTracker.ClearStale ();
        }

        private void HandleTimeEntryPropertyChanged (string prop)
        {
            if (prop == TimeEntryModel.PropertyState
                    || prop == TimeEntryModel.PropertyStartTime
                    || prop == TimeEntryModel.PropertyStopTime) {
                Rebind ();
            }
        }

        private bool SyncModel ()
        {
            var shouldRebind = true;

            var data = ActiveTimeEntryData;
            if (data != null) {
                if (backingActiveTimeEntry == null) {
                    backingActiveTimeEntry = new TimeEntryModel (data);
                } else {
                    backingActiveTimeEntry.Data = data;
                    shouldRebind = false;
                }
            }

            return shouldRebind;
        }

        private TimeEntryData ActiveTimeEntryData
        {
            get {
                if (timeEntryManager == null) {
                    return null;
                }
                return timeEntryManager.Active;
            }
        }

        private ITimeEntryModel ActiveTimeEntry
        {
            get {
                if (ActiveTimeEntryData == null) {
                    return null;
                }
                return backingActiveTimeEntry;
            }
        }

        protected bool CanRebind
        {
            get { return canRebind || TimeEntry == null; }
        }

        public ITimeEntryModel TimeEntry
        {
            get { return model; }
            set {
                DiscardDescriptionChanges ();

                if (tagsView != null && (value == null || value.Id != tagsView.TimeEntryId)) {
                    tagsView.Updated -= OnTimeEntryTagsUpdated;
                    tagsView = null;
                }

                model = value;

                if (model != null && tagsView == null) {
                    tagsView = new TimeEntryTagsView (model.Id);
                    tagsView.Updated += OnTimeEntryTagsUpdated;
                }

                Rebind ();
                RebindTags ();
            }
        }

        protected virtual void Rebind ()
        {
            ResetTrackedObservables ();

            if (TimeEntry == null || !canRebind) {
                return;
            }

            DateTime startTime;
            var useTimer = TimeEntry.StartTime == DateTime.MinValue;
            startTime = useTimer ? Time.Now : TimeEntry.StartTime.ToLocalTime ();

            StartTimeEditText.Text = startTime.ToDeviceTimeString ();

            // Only update DescriptionEditText when content differs, else the user is unable to edit it
            if (!descriptionChanging && DescriptionEditText.Text != TimeEntry.Description) {
                DescriptionEditText.Text = TimeEntry.Description;
                DescriptionEditText.SetSelection (DescriptionEditText.Text.Length);
            }
            DescriptionEditText.SetHint (useTimer
                                         ? Resource.String.CurrentTimeEntryEditDescriptionHint
                                         : Resource.String.CurrentTimeEntryEditDescriptionPastHint);

            if (TimeEntry.StopTime.HasValue) {
                StopTimeEditText.Text = TimeEntry.StopTime.Value.ToLocalTime ().ToDeviceTimeString ();
                StopTimeEditText.Visibility = ViewStates.Visible;
            } else {
                StopTimeEditText.Text = Time.Now.ToDeviceTimeString ();
                if (TimeEntry.StartTime == DateTime.MinValue || TimeEntry.State == TimeEntryState.Running) {
                    StopTimeEditText.Visibility = ViewStates.Invisible;
                    StopTimeEditLabel.Visibility = ViewStates.Invisible;
                } else {
                    StopTimeEditLabel.Visibility = ViewStates.Visible;
                    StopTimeEditText.Visibility = ViewStates.Visible;
                }
            }

            if (TimeEntry.Project != null) {
                ProjectEditText.Text = TimeEntry.Project.Name;
                if (TimeEntry.Project.Client != null) {
                    ProjectBit.SetAssistViewTitle (TimeEntry.Project.Client.Name);
                } else {
                    ProjectBit.DestroyAssistView ();
                }
            } else {
                ProjectEditText.Text = String.Empty;
                ProjectBit.DestroyAssistView ();
            }

            BillableCheckBox.Checked = !TimeEntry.IsBillable;
            if (TimeEntry.IsBillable) {
                BillableCheckBox.SetText (Resource.String.CurrentTimeEntryEditBillableChecked);
            } else {
                BillableCheckBox.SetText (Resource.String.CurrentTimeEntryEditBillableUnchecked);
            }
            if (TimeEntry.Workspace == null || !TimeEntry.Workspace.IsPremium) {
                BillableCheckBox.Visibility = ViewStates.Gone;
            } else {
                BillableCheckBox.Visibility = ViewStates.Visible;
            }
        }

        private void OnTimeEntryTagsUpdated (object sender, EventArgs args)
        {
            RebindTags ();
        }

        private void RebindTags()
        {
            if (TimeEntry == null || !canRebind) {
                return;
            }

            if (TagsBit == null) {
                return;
            }

            TagsBit.RebindTags (tagsView);
        }

        protected EditText StartTimeEditText { get; private set; }

        protected EditText StopTimeEditText { get; private set; }

        protected TextView StopTimeEditLabel { get; private set; }

        protected EditText DescriptionEditText { get; private set; }

        protected EditText ProjectEditText { get; private set; }

        protected CheckBox BillableCheckBox { get; private set; }

        protected ImageButton DeleteImageButton { get; private set; }

        protected TogglField ProjectBit { get; private set; }

        protected TogglField DescriptionBit { get; private set; }

        protected TogglTagsField TagsBit { get; private set; }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ManualEditTimeEntryFragment, container, false);

            StartTimeEditText = view.FindViewById<EditText> (Resource.Id.StartTimeEditText).SetFont (Font.Roboto);
            StopTimeEditText = view.FindViewById<EditText> (Resource.Id.StopTimeEditText).SetFont (Font.Roboto);
            StopTimeEditLabel = view.FindViewById<TextView> (Resource.Id.StopTimeEditLabel);

            DescriptionBit = view.FindViewById<TogglField> (Resource.Id.Description)
                             .DestroyAssistView ().DestroyArrow ().ShowTitle (false)
                             .SetName (Resource.String.CurrentTimeEntryEditDescriptionHint);
            DescriptionEditText = DescriptionBit.TextField;

            ProjectBit = view.FindViewById<TogglField> (Resource.Id.Project)
                         .ShowTitle (false).SimulateButton()
                         .SetName (Resource.String.CurrentTimeEntryEditProjectHint);
            ProjectEditText = ProjectBit.TextField;

            TagsBit = view.FindViewById<TogglTagsField> (Resource.Id.TagsBit).ShowTitle (false);

            BillableCheckBox = view.FindViewById<CheckBox> (Resource.Id.BillableCheckBox).SetFont (Font.RobotoLight);

            StartTimeEditText.Click += OnStartTimeEditTextClick;
            StopTimeEditText.Click += OnStopTimeEditTextClick;
            DescriptionEditText.TextChanged += OnDescriptionTextChanged;
            DescriptionEditText.EditorAction += OnDescriptionEditorAction;
            DescriptionEditText.FocusChange += OnDescriptionFocusChange;
            ProjectBit.Click += OnProjectSelected;
            ProjectEditText.Click += OnProjectSelected;
            TagsBit.FullClick += OnTagSelected;
            BillableCheckBox.CheckedChange += OnBillableCheckBoxCheckedChange;

            return view;
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            Activity.OnBackPressed ();

            return base.OnOptionsItemSelected (item);
        }

        private void OnDurationTextViewClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }
            new ChangeTimeEntryDurationDialogFragment (TimeEntry).Show (FragmentManager, "duration_dialog");
        }

        private void OnStartTimeEditTextClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }
            new ChangeTimeEntryStartTimeDialogFragment (TimeEntry).Show (FragmentManager, "time_dialog");
        }

        private void OnStopTimeEditTextClick (object sender, EventArgs e)
        {
            if (TimeEntry == null || TimeEntry.State == TimeEntryState.Running) {
                return;
            }
            new ChangeTimeEntryStopTimeDialogFragment (TimeEntry).Show (FragmentManager, "time_dialog");
        }

        private void OnDescriptionTextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            // This can be called when the fragment is being restored, so the previous value will be
            // set miraculously. So we need to make sure that this is indeed the user who is changing the
            // value by only acting when the OnStart has been called.
            if (!canRebind) {
                return;
            }

            // Mark description as changed
            descriptionChanging = TimeEntry != null && DescriptionEditText.Text != TimeEntry.Description;

            // Make sure that we're commiting 1 second after the user has stopped typing
            CancelDescriptionChangeAutoCommit ();
            if (descriptionChanging) {
                ScheduleDescriptionChangeAutoCommit ();
            }
        }

        private void OnDescriptionFocusChange (object sender, View.FocusChangeEventArgs e)
        {
            if (!e.HasFocus) {
                CommitDescriptionChanges ();
            }
        }

        private void OnDescriptionEditorAction (object sender, TextView.EditorActionEventArgs e)
        {
            if (e.ActionId == Android.Views.InputMethods.ImeAction.Done) {
                CommitDescriptionChanges ();
            }
            e.Handled = false;
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

        private void OnBillableCheckBoxCheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            var isBillable = !BillableCheckBox.Checked;
            if (TimeEntry.IsBillable != isBillable) {
                TimeEntry.IsBillable = isBillable;
                SaveTimeEntry ();
            }
        }

        private async void OnDeleteImageButtonClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            await TimeEntry.DeleteAsync ();
            Toast.MakeText (Activity, Resource.String.CurrentTimeEntryEditDeleteToast, ToastLength.Short).Show ();
        }

        private void AutoCommitDescriptionChanges ()
        {
            if (!autoCommitScheduled) {
                return;
            }
            autoCommitScheduled = false;
            CommitDescriptionChanges ();
        }

        private void ScheduleDescriptionChangeAutoCommit ()
        {
            if (autoCommitScheduled) {
                return;
            }

            autoCommitScheduled = true;
            handler.PostDelayed (AutoCommitDescriptionChanges, 1000);
        }

        private void CancelDescriptionChangeAutoCommit ()
        {
            if (!autoCommitScheduled) {
                return;
            }

            handler.RemoveCallbacks (AutoCommitDescriptionChanges);
            autoCommitScheduled = false;
        }

        private void CommitDescriptionChanges ()
        {
            if (TimeEntry != null && descriptionChanging) {
                if (string.IsNullOrEmpty (TimeEntry.Description) && string.IsNullOrEmpty (DescriptionEditText.Text)) {
                    return;
                }
                if (TimeEntry.Description != DescriptionEditText.Text) {
                    TimeEntry.Description = DescriptionEditText.Text;
                    SaveTimeEntry ();
                }
            }
            descriptionChanging = false;
            CancelDescriptionChangeAutoCommit ();
        }

        private void DiscardDescriptionChanges ()
        {
            descriptionChanging = false;
            CancelDescriptionChangeAutoCommit ();
        }

        private async void SaveTimeEntry ()
        {
            var entry = TimeEntry;
            if (entry == null) {
                return;
            }

            try {
                await entry.SaveAsync ().ConfigureAwait (false);
            } catch (Exception ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Warning (Tag, ex, "Failed to save model changes.");
            }
        }
    }
}
