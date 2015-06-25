using System;
using System.Collections.Generic;
using System.ComponentModel;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.Data;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Logging;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;

namespace Toggl.Joey.UI.Fragments
{
    public class ManualEditTimeEntryFragment : Fragment
    {
        private readonly Handler handler = new Handler ();
        private TimeEntryTagsView tagsView;
        private ActiveTimeEntryManager timeEntryManager;
        private ITimeEntryModel backingActiveTimeEntry;
        private PropertyChangeTracker propertyTracker;
        private bool canRebind;
        private bool descriptionChanging;
        private bool autoCommitScheduled;
        private bool isProcessingAction;

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

            canRebind = true;
        }

        private void OnActiveTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyActive) {
                SyncModel ();
                Rebind ();
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

            if (tagsView != null) {
                tagsView.Updated -= OnTimeEntryTagsUpdated;
                tagsView = null;
            }

            if (model != null && tagsView == null) {
                tagsView = new TimeEntryTagsView (model.Id);
                tagsView.Updated += OnTimeEntryTagsUpdated;
            }

            propertyTracker.ClearStale ();
        }

        private void HandleTimeEntryPropertyChanged (string prop)
        {
            if (prop == TimeEntryModel.PropertyProject
                    || prop == TimeEntryModel.PropertyState
                    || prop == TimeEntryModel.PropertyStartTime
                    || prop == TimeEntryModel.PropertyStopTime
                    || prop == TimeEntryModel.PropertyDescription
                    || prop == TimeEntryModel.PropertyIsBillable) {
                Rebind ();
            }
        }

        private void SyncModel ()
        {
            var data = ActiveTimeEntryData;
            if (data != null) {
                if (backingActiveTimeEntry == null) {
                    backingActiveTimeEntry = new TimeEntryModel (data);
                } else {
                    backingActiveTimeEntry.Data = data;
                }
            }
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
            get { return canRebind || ActiveTimeEntry == null; }
        }

        protected virtual void Rebind ()
        {
            ResetTrackedObservables ();
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null || !canRebind) {
                return;
            }

            DateTime startTime;
            var useTimer = currentEntry.StartTime == DateTime.MinValue;
            startTime = useTimer ? Time.Now : currentEntry.StartTime.ToLocalTime ();

            StartTimeEditText.Text = startTime.ToDeviceTimeString ();

            // Only update DescriptionEditText when content differs, else the user is unable to edit it
            if (!descriptionChanging && DescriptionEditText.Text != currentEntry.Description) {
                DescriptionEditText.Text = currentEntry.Description;
                DescriptionEditText.SetSelection (DescriptionEditText.Text.Length);
            }
            DescriptionEditText.SetHint (useTimer
                                         ? Resource.String.CurrentTimeEntryEditDescriptionHint
                                         : Resource.String.CurrentTimeEntryEditDescriptionPastHint);

            if (currentEntry.StopTime.HasValue) {
                StopTimeEditText.Text = currentEntry.StopTime.Value.ToLocalTime ().ToDeviceTimeString ();
                StopTimeEditText.Visibility = ViewStates.Visible;
            } else {
                StopTimeEditText.Text = Time.Now.ToDeviceTimeString ();
                if (currentEntry.StartTime == DateTime.MinValue || currentEntry.State == TimeEntryState.Running) {
                    StopTimeEditText.Visibility = ViewStates.Invisible;
                    StopTimeEditLabel.Visibility = ViewStates.Invisible;
                } else {
                    StopTimeEditLabel.Visibility = ViewStates.Visible;
                    StopTimeEditText.Visibility = ViewStates.Visible;
                }
            }

            if (currentEntry.Project != null) {
                ProjectEditText.Text = currentEntry.Project.Name;
                if (currentEntry.Project.Client != null) {
                    ProjectBit.SetAssistViewTitle (currentEntry.Project.Client.Name);
                } else {
                    ProjectBit.DestroyAssistView ();
                }
            } else {
                ProjectEditText.Text = String.Empty;
                ProjectBit.DestroyAssistView ();
            }

            BillableCheckBox.Checked = !currentEntry.IsBillable;
            if (currentEntry.IsBillable) {
                BillableCheckBox.SetText (Resource.String.CurrentTimeEntryEditBillableChecked);
            } else {
                BillableCheckBox.SetText (Resource.String.CurrentTimeEntryEditBillableUnchecked);
            }
            if (currentEntry.Workspace == null || !currentEntry.Workspace.IsPremium) {
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
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null || !canRebind) {
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

        protected Button DummyStartButton { get; private set; }

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


            DummyStartButton = view.FindViewById<Button> (Resource.Id.DummyStartStopButton);
            DummyStartButton.Click += OnStartButtonClicked;
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

        private async void  OnStartButtonClicked (object sender, EventArgs e)
        {
            // Protect from double clicks
            if (isProcessingAction) {
                return;
            }

            isProcessingAction = true;
            try {
                var entry = ActiveTimeEntry;
                if (entry == null) {
                    return;
                }

                // Make sure that we work on the copy of the entry to not affect the rest of the logic.
                entry = (ITimeEntryModel)new TimeEntryModel (new TimeEntryData (entry.Data));

                var showProjectSelection = false;

                try {
                    if (entry.State == TimeEntryState.New && entry.StopTime.HasValue) {
                        await entry.StoreAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppManual);
                    } else if (entry.State == TimeEntryState.Running) {
                        await entry.StopAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStopEvent (TimerStopSource.App);
                    } else {
                        await entry.StartAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppNew);
                    }
                } catch (Exception ex) { }

                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Send (new UserTimeEntryStateChangeMessage (this, entry.Data));
            } finally {
                isProcessingAction = false;
                Rebind();
            }
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            Activity.OnBackPressed ();

            return base.OnOptionsItemSelected (item);
        }

        private void OnStartTimeEditTextClick (object sender, EventArgs e)
        {
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null) {
                return;
            }
            new ChangeTimeEntryStartTimeDialogFragment (currentEntry).Show (FragmentManager, "time_dialog");
        }

        private void OnStopTimeEditTextClick (object sender, EventArgs e)
        {
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null || currentEntry.State == TimeEntryState.Running) {
                return;
            }
            new ChangeTimeEntryStopTimeDialogFragment (currentEntry).Show (FragmentManager, "time_dialog");
        }

        private void OnDescriptionTextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            // This can be called when the fragment is being restored, so the previous value will be
            // set miraculously. So we need to make sure that this is indeed the user who is changing the
            // value by only acting when the OnStart has been called.
            var currentEntry = ActiveTimeEntry;
            if (!canRebind) {
                return;
            }

            // Mark description as changed
            descriptionChanging = currentEntry != null && DescriptionEditText.Text != currentEntry.Description;

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
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null) {
                return;
            }

            var intent = new Intent (Activity, typeof (ProjectListActivity));
            intent.PutStringArrayListExtra (ProjectListActivity.ExtraTimeEntriesIds, new List<string> {currentEntry.Id.ToString ()});
            StartActivity (intent);
        }

        private void OnTagSelected (object sender, EventArgs e)
        {
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null) {
                return;
            }
            new ChooseTimeEntryTagsDialogFragment (currentEntry.Workspace.Id, new List<TimeEntryData> {currentEntry.Data}).Show (FragmentManager, "tags_dialog");
        }

        private void OnBillableCheckBoxCheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null) {
                return;
            }

            var isBillable = !BillableCheckBox.Checked;
            if (currentEntry.IsBillable != isBillable) {
                currentEntry.IsBillable = isBillable;
                SaveTimeEntry ();
            }
        }

        private async void OnDeleteImageButtonClick (object sender, EventArgs e)
        {
            var currentEntry = ActiveTimeEntry;

            if (currentEntry == null) {
                return;
            }

            await currentEntry.DeleteAsync ();
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
            var currentEntry = ActiveTimeEntry;

            if (currentEntry != null && descriptionChanging) {
                if (string.IsNullOrEmpty (currentEntry.Description) && string.IsNullOrEmpty (DescriptionEditText.Text)) {
                    return;
                }
                if (currentEntry.Description != DescriptionEditText.Text) {
                    currentEntry.Description = DescriptionEditText.Text;
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
            var entry = ActiveTimeEntry;
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
