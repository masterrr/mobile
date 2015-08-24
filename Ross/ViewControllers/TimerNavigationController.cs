using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using CoreFoundation;
using UIKit;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using XPlatUtils;
using Toggl.Ross.Data;
using Toggl.Ross.Theme;
using Toggl.Phoebe.Data.ViewModels;

namespace Toggl.Ross.ViewControllers
{
    public class TimerNavigationController
    {
        private const string DefaultDurationText = " 00:00:00 ";
        private readonly bool showRunning;
        private UIViewController parentController;
        private UIButton durationButton;
        private UIButton actionButton;
        private UIBarButtonItem navigationButton;

        private ITimeEntryModel currentTimeEntry;
        private readonly EditTimeEntryViewModel viewModel;

        private ActiveTimeEntryManager timeEntryManager;
        private PropertyChangeTracker propertyTracker;
        private bool isStarted;
        private int rebindCounter;
        private bool isActing;

        public TimerNavigationController (EditTimeEntryViewModel viewModel = null)
        {
            if (viewModel != null) {
                this.viewModel = viewModel;
                currentTimeEntry = viewModel.Model;
            }
            showRunning = currentTimeEntry == null;
        }

        void OnStateTimeChanged (object sender, EventArgs e)
        {
            Rebind ();
        }

        public void Attach (UIViewController parentController)
        {
            this.parentController = parentController;

            // Lazyily create views
            if (durationButton == null) {
                durationButton = new UIButton ().Apply (Style.NavTimer.DurationButton);
                durationButton.SetTitle (DefaultDurationText, UIControlState.Normal); // Dummy content to use for sizing of the label
                durationButton.SizeToFit ();
                durationButton.TouchUpInside += OnDurationButtonTouchUpInside;
            }

            if (navigationButton == null) {
                actionButton = new UIButton ().Apply (Style.NavTimer.StartButton);
                actionButton.SizeToFit ();
                actionButton.TouchUpInside += OnActionButtonTouchUpInside;
                navigationButton = new UIBarButtonItem (actionButton);
            }

            // Attach views
            var navigationItem = parentController.NavigationItem;
            navigationItem.TitleView = durationButton;
            navigationItem.RightBarButtonItem = navigationButton;
        }

        private void OnDurationButtonTouchUpInside (object sender, EventArgs e)
        {
            // Duration change for the grouped mode is disabled
            if (currentTimeEntry != null && !currentTimeEntry.Grouped && !ServiceContainer.Resolve<ISettingsStore> ().GroupedTimeEntries) {
                var controller = new DurationChangeViewController (currentTimeEntry);
                parentController.NavigationController.PushViewController (controller, true);
            }
        }

        private async void OnActionButtonTouchUpInside (object sender, EventArgs e)
        {
            if (isActing) {
                return;
            }
            isActing = true;

            try {
                if (currentTimeEntry != null && currentTimeEntry.State == TimeEntryState.Running) {
                    await currentTimeEntry.StopAsync ();
                    // Ping analytics
                    ServiceContainer.Resolve<ITracker>().SendTimerStopEvent (TimerStopSource.App);
                } else if (timeEntryManager != null) {
                    currentTimeEntry = (TimeEntryModel)timeEntryManager.Draft;
                    if (currentTimeEntry == null) {
                        return;
                    }

                    await currentTimeEntry.StartAsync ();

                    var controllers = new List<UIViewController> (parentController.NavigationController.ViewControllers);
                    controllers.Add (new EditTimeEntryViewController (currentTimeEntry.Data));
                    if (ServiceContainer.Resolve<SettingsStore> ().ChooseProjectForNew) {
                        controllers.Add (new ProjectSelectionViewController (currentTimeEntry.Data));
                    }
                    parentController.NavigationController.SetViewControllers (controllers.ToArray (), true);

                    // Ping analytics
                    ServiceContainer.Resolve<ITracker>().SendTimerStartEvent (TimerStartSource.AppNew);
                }
            } finally {
                isActing = false;
            }
        }

        private void Rebind ()
        {
            if (!isStarted) {
                return;
            }                               
            rebindCounter++;

            if (currentTimeEntry == null) {
                durationButton.SetTitle (DefaultDurationText, UIControlState.Normal);
                actionButton.Apply (Style.NavTimer.StartButton);
                actionButton.Hidden = false;
            } else {
                var duration = currentTimeEntry.GetDuration ();

                durationButton.SetTitle (duration.ToString (@"hh\:mm\:ss"), UIControlState.Normal);
                actionButton.Apply (Style.NavTimer.StopButton);
                actionButton.Hidden = currentTimeEntry.State != TimeEntryState.Running;

                var counter = rebindCounter;
                DispatchQueue.MainQueue.DispatchAfter (
                    TimeSpan.FromMilliseconds (1000 - duration.Milliseconds),
                delegate {
                    if (counter == rebindCounter) {
                        Rebind ();
                    }
                });
            }
        }

        private void OnTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyRunning) {
                ResetModelToRunning ();
            }
        }

        private async void ResetModelToRunning ()
        {
            if (timeEntryManager == null) {
                return;
            }

            if (timeEntryManager.Running != null) {
                if (currentTimeEntry == null) {
                    currentTimeEntry = await TimeEntryGroup.GetLoadedGroup(timeEntryManager.Running, DateTime.UtcNow);
                } else {
                    currentTimeEntry.Data = timeEntryManager.Running;
                }
            } else {
                currentTimeEntry = null;
            }

            Rebind ();
        }

        public void Start ()
        {
            propertyTracker = new PropertyChangeTracker ();

            // Start listening to timer changes
            if (showRunning) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
                timeEntryManager.PropertyChanged += OnTimeEntryManagerPropertyChanged;
                ResetModelToRunning ();
            }

            if (viewModel != null) {
                viewModel.OnStateTimeChanged += OnStateTimeChanged;
            }

            isStarted = true;
            Rebind ();
        }

        public void Stop ()
        {
            // Stop listening to timer changes
            isStarted = false;

            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }

            if (timeEntryManager != null) {
                timeEntryManager.PropertyChanged -= OnTimeEntryManagerPropertyChanged;
                timeEntryManager = null;
            }

            if (viewModel != null) {
                viewModel.OnStateTimeChanged -= OnStateTimeChanged;
            }


            if (showRunning) {
                currentTimeEntry = null;
            }
        }
    }
}
