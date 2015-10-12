using System;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Logging;
using XPlatUtils;
using ActionBar = Android.Support.V7.App.ActionBar;
using Activity = Android.Support.V7.App.AppCompatActivity;
using Fragment = Android.Support.V4.App.Fragment;
using MeasureSpec = Android.Views.View.MeasureSpec;
using Toolbar = Android.Support.V7.Widget.Toolbar;
using Toggl.Phoebe.Analytics;
using Toggl.Joey.Data;

namespace Toggl.Joey.UI.Fragments
{
    public abstract class BaseEditTimeEntryFragment : Fragment
    {
        private readonly Handler handler = new Handler ();
        private PropertyChangeTracker propertyTracker;
        private ITimeEntryModel model;
        private TimeEntryTagsView tagsView;
        private bool canRebind;
        private bool descriptionChanging;
        private bool autoCommitScheduled;
        private bool manualMode;
        private bool isProcessingAction;
        private StartStopFab ActionFAB;

        public event EventHandler OnPressedProjectSelector;

        public event EventHandler OnPressedTagSelector;

        protected BaseEditTimeEntryFragment ()
        {
        }

        protected BaseEditTimeEntryFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
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

        protected bool CanRebind
        {
            get { return canRebind || model == null; }
        }

        protected abstract void ResetModel ();

        public override void OnStart ()
        {
            base.OnStart ();

            propertyTracker = new PropertyChangeTracker ();
            canRebind = true;

            Rebind ();
            RebindTags ();
        }

        public override void OnStop ()
        {
            base.OnStop ();

            canRebind = false;

            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }
        }

        public override void OnDestroyView ()
        {
            if (tagsView != null) {
                tagsView.Updated -= OnTimeEntryTagsUpdated;
                tagsView = null;
            }

            base.OnDestroyView ();
        }

        public override bool UserVisibleHint
        {
            get { return base.UserVisibleHint; }
            set {
                if (!value) {
                    CommitDescriptionChanges ();
                }
                base.UserVisibleHint = value;
            }
        }

        private void ResetTrackedObservables ()
        {
            if (propertyTracker == null) {
                return;
            }

            propertyTracker.MarkAllStale ();

            var entry = TimeEntry;
            if (entry != null) {
                propertyTracker.Add (entry, HandleTimeEntryPropertyChanged);

                if (entry.Project != null) {
                    propertyTracker.Add (entry.Project, HandleProjectPropertyChanged);

                    if (entry.Project.Client != null) {
                        propertyTracker.Add (entry.Project.Client, HandleClientPropertyChanged);
                    }
                }
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
            } else if (prop == TimeEntryModel.PropertyId) {
                ResetModel ();
            }
        }

        private void HandleProjectPropertyChanged (string prop)
        {
            if (prop == ProjectModel.PropertyClient
                    || prop == ProjectModel.PropertyName
                    || prop == ProjectModel.PropertyColor) {
                Rebind ();
            }
        }

        private void HandleClientPropertyChanged (string prop)
        {
            if (prop == ClientModel.PropertyName) {
                Rebind ();
            }
        }

        protected virtual void Rebind ()
        {
            ResetTrackedObservables ();

            if (TimeEntry == null || !canRebind) {
                return;
            }

            DateTime startTime;
            var useTimer = TimeEntry.StartTime.IsMinValue ();
            if (useTimer) {
                startTime = Time.Now;

                DurationTextView.Text = TimeSpan.Zero.ToString ();

                // Make sure that we display accurate time:
                handler.RemoveCallbacks (Rebind);
                handler.PostDelayed (Rebind, 5000);
            } else {
                startTime = TimeEntry.StartTime.ToLocalTime ();

                var duration = TimeEntry.GetDuration ();
                DurationTextView.Text = TimeSpan.FromSeconds ((long)duration.TotalSeconds).ToString ();

                if (TimeEntry.State == TimeEntryState.Running) {
                    handler.RemoveCallbacks (Rebind);
                    handler.PostDelayed (Rebind, 1000 - duration.Milliseconds);
                }
            }

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
                if (TimeEntry.State == TimeEntryState.Running) {
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
            if (TimeEntry.State == TimeEntryState.Running) {
                ActionFAB.ButtonAction = FABButtonState.Stop;
            } else {
                ActionFAB.ButtonAction = TimeEntry.StopTime.HasValue ? FABButtonState.Save : FABButtonState.Start;
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

        protected TextView DurationTextView { get; private set; }

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

        protected ActionBar Toolbar { get; private set; }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.EditTimeEntryFragment, container, false);
            var toolbar = view.FindViewById<Toolbar> (Resource.Id.EditTimeEntryFragmentToolbar);
            var activity = (Activity)Activity;

            activity.SetSupportActionBar (toolbar);
            Toolbar = activity.SupportActionBar;
            Toolbar.SetDisplayHomeAsUpEnabled (true);

            var durationLayout = inflater.Inflate (Resource.Layout.DurationTextView, null);
            DurationTextView = durationLayout.FindViewById<TextView> (Resource.Id.DurationTextViewTextView);

            Toolbar.SetCustomView (durationLayout, new ActionBar.LayoutParams ((int)GravityFlags.Center));
            Toolbar.SetDisplayShowCustomEnabled (true);
            Toolbar.SetDisplayShowTitleEnabled (false);

            HasOptionsMenu = false;

            ActionFAB = view.FindViewById<StartStopFab> (Resource.Id.StartStopBtn);
            StartTimeEditText = view.FindViewById<EditText> (Resource.Id.StartTimeEditText).SetFont (Font.Roboto);
            StopTimeEditText = view.FindViewById<EditText> (Resource.Id.StopTimeEditText).SetFont (Font.Roboto);
            StopTimeEditLabel = view.FindViewById<TextView> (Resource.Id.StopTimeEditLabel);

            DescriptionBit = view.FindViewById<TogglField> (Resource.Id.Description)
                             .DestroyAssistView().DestroyArrow()
                             .SetName (Resource.String.BaseEditTimeEntryFragmentDescription);
            DescriptionEditText = DescriptionBit.TextField;

            ProjectBit = view.FindViewById<TogglField> (Resource.Id.Project)
                         .SetName (Resource.String.BaseEditTimeEntryFragmentProject)
                         .SimulateButton();
            ProjectEditText = ProjectBit.TextField;

            TagsBit = view.FindViewById<TogglTagsField> (Resource.Id.TagsBit);

            BillableCheckBox = view.FindViewById<CheckBox> (Resource.Id.BillableCheckBox).SetFont (Font.RobotoLight);

            DurationTextView.Click += OnDurationTextViewClick;
            StartTimeEditText.Click += OnStartTimeEditTextClick;
            StopTimeEditText.Click += OnStopTimeEditTextClick;
            DescriptionEditText.TextChanged += OnDescriptionTextChanged;
            DescriptionEditText.EditorAction += OnDescriptionEditorAction;
            DescriptionEditText.FocusChange += OnDescriptionFocusChange;
            ProjectBit.Click += OnProjectEditTextClick;
            ProjectEditText.Click += OnProjectEditTextClick;
            TagsBit.FullClick += OnTagsEditTextClick;
            BillableCheckBox.CheckedChange += OnBillableCheckBoxCheckedChange;
            ActionFAB.Click += OnActionButtonClicked;

            return view;
        }

        public async void OnActionButtonClicked (object sender, EventArgs e)
        {
            // Protect from double clicks
            if (isProcessingAction) {
                return;
            }

            isProcessingAction = true;
            try {
                try {
                    if (TimeEntry.State == TimeEntryState.New && TimeEntry.StopTime.HasValue) {
                        await TimeEntry.StoreAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppManual);
                    } else if (TimeEntry.State == TimeEntryState.Running) {
                        await TimeEntry.StopAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStopEvent (TimerStopSource.App);
                    } else {
                        await TimeEntry.StartAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppNew);
                    }
                } catch (Exception ex) {
                }

                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Send (new UserTimeEntryStateChangeMessage (this, TimeEntry.Data));
            } finally {
                isProcessingAction = false;
                Activity.OnBackPressed ();
            }
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

        private void OnProjectEditTextClick (object sender, EventArgs e)
        {
            if (OnPressedProjectSelector != null) {
                OnPressedProjectSelector.Invoke (sender, e);
            }
        }

        private void OnTagsEditTextClick (object sender, EventArgs e)
        {
            if (OnPressedTagSelector != null) {
                OnPressedTagSelector.Invoke (sender, e);
            }
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
            ResetModel ();

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

    public class SimpleEditTimeEntryFragment : BaseEditTimeEntryFragment
    {
        public SimpleEditTimeEntryFragment ()
        {
        }

        public SimpleEditTimeEntryFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        protected override void ResetModel ()
        {
            // Need to be careful when updating model data as the logic in BaseEditTimeEntries uses
            // Id changes to detect deletions. This would result in recursive loop with this function.
        }
    }
}