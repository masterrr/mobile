using System;
using System.Collections.Generic;
using System.ComponentModel;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Fragments;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using XPlatUtils;
using Activity = Android.Support.V4.App.FragmentActivity;
using Fragment = Android.Support.V4.App.Fragment;

namespace Toggl.Joey.UI.Components
{
    public class TimerComponent
    {
        private static readonly string LogTag = "TimerComponent";
        private readonly Handler handler = new Handler ();
        private PropertyChangeTracker propertyTracker;
        private ActiveTimeEntryManager timeEntryManager;
        private ITimeEntryModel backingActiveTimeEntry;
        private float animateState;
        private bool canRebind;
        private bool compact;
        private bool hide = false;

        protected TextView DurationTextView { get; private set; }

        protected TextView ProjectTextView { get; private set; }

        protected TextView DescriptionTextView { get; private set; }

        protected TextView TimerTitleTextView { get; private set; }

        public View Root { get; private set; }

        public ImageButton AddManualEntry { get; private set; }

        private Activity activity;

        public event EventHandler ActiveEntryChanged;

        private void FindViews ()
        {
            DurationTextView = Root.FindViewById<TextView> (Resource.Id.DurationTextView).SetFont (Font.RobotoLight);
            TimerTitleTextView = Root.FindViewById<TextView> (Resource.Id.TimerTitleTextView);
            ProjectTextView = Root.FindViewById<TextView> (Resource.Id.ProjectTextView);
            DescriptionTextView = Root.FindViewById<TextView> (Resource.Id.DescriptionTextView).SetFont (Font.RobotoLight);
            AddManualEntry = Root.FindViewById<ImageButton> (Resource.Id.AddManuallyButton);
            DurationTextView.Click += OnDurationTextClicked;
        }

        public void OnCreate (Activity activity)
        {
            this.activity = activity;

            propertyTracker = new PropertyChangeTracker ();

            Root = LayoutInflater.From (activity).Inflate (Resource.Layout.TimerComponent, null);

            FindViews ();
        }

        public void OnDestroy (Activity activity)
        {
            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }
        }

        public void OnStart ()
        {
            // Hook up to time entry manager
            if (timeEntryManager == null) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
                timeEntryManager.PropertyChanged += OnActiveTimeEntryManagerPropertyChanged;
            }

            canRebind = true;
            SyncModel ();
            Rebind ();

            if (ActiveEntryChanged != null) {
                ActiveEntryChanged.Invoke (this, EventArgs.Empty); // Initial rendering
            }
        }

        public void OnStop ()
        {
            canRebind = false;

            if (timeEntryManager != null) {
                timeEntryManager.PropertyChanged -= OnActiveTimeEntryManagerPropertyChanged;
                timeEntryManager = null;
            }
        }

        private void OnActiveTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyActive || args.PropertyName == ActiveTimeEntryManager.PropertyRunning) {

                if (SyncModel ()) {
                    Rebind ();
                }
                if (ActiveEntryChanged != null) {
                    ActiveEntryChanged.Invoke (sender, args);
                }
            }
        }

        private bool SyncModel ()
        {
            var shouldRebind = true;

            var data = ActiveTimeEntryData;
            if (data != null) {
                if (backingActiveTimeEntry == null) {
                    backingActiveTimeEntry = (ITimeEntryModel)new TimeEntryModel (data);
                } else {
                    backingActiveTimeEntry.Data = data;
                    shouldRebind = false;
                }
            }

            return shouldRebind;
        }

        public bool IsRunning
        {
            get {
                return ActiveTimeEntry.State == TimeEntryState.Running;
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

        public ITimeEntryModel ActiveTimeEntry
        {
            get {
                if (ActiveTimeEntryData == null) {
                    return null;
                }
                return backingActiveTimeEntry;
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

        void OnDurationTextClicked (object sender, EventArgs e)
        {
            var currentEntry = ActiveTimeEntry;
            if (currentEntry == null) {
                return;
            }
            if (animateState == 1) {
                Root.PerformClick ();
                return;
            }
            new ChangeTimeEntryDurationDialogFragment (currentEntry).Show (activity.SupportFragmentManager, "duration_dialog");
        }

        private void Rebind ()
        {
            ResetTrackedObservables ();

            Root.Visibility = Hide ? ViewStates.Gone : ViewStates.Visible;

            var currentEntry = ActiveTimeEntry;
            if (!canRebind || currentEntry == null || Hide) {
                return;
            }

            var resources = activity.ApplicationContext.Resources;
            bool isRunning = currentEntry.State == TimeEntryState.Running;

            AddManualEntry.Visibility = isRunning ? ViewStates.Gone : ViewStates.Visible;
            TimerTitleTextView.Visibility = isRunning ? ViewStates.Gone : ViewStates.Visible;


            ProjectTextView.Visibility = isRunning ? ViewStates.Visible : ViewStates.Gone;
            DescriptionTextView.Visibility = isRunning ? ViewStates.Visible : ViewStates.Gone;
            DurationTextView.Visibility = isRunning ? ViewStates.Visible : ViewStates.Gone;
            if (isRunning) {
                DescriptionTextView.Text = currentEntry.Description.Length == 0 ?  resources.GetText (Resource.String.TimerComponentNoDescription) : currentEntry.Description;
                ProjectTextView.Text = currentEntry.Project != null ? currentEntry.Project.Name : resources.GetText (Resource.String.TimerComponentNoProject);
            }
            var duration = currentEntry.GetDuration ();
            DurationTextView.Text = TimeSpan.FromSeconds ((long)duration.TotalSeconds).ToString ();

            // Schedule next rebind:
            handler.RemoveCallbacks (Rebind);
            handler.PostDelayed (Rebind, 1000 - duration.Milliseconds);
        }

        public bool CompactView
        {
            get { return compact; }
            set {
                if (compact != value) {
                    compact = value;
                    Rebind ();
                }
            }
        }

        public bool Hide
        {
            get { return hide; }
            set {
                hide = value;
                Rebind();
            }
        }
    }
}