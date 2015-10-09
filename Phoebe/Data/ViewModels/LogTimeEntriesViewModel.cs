using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    public class LogTimeEntriesViewModel : IViewModel<TimeEntryModel>
    {
        private ActiveTimeEntryManager timeEntryManager;

        public LogTimeEntriesViewModel ()
        {
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "TimeEntryList Screen";
            timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            timeEntryManager.PropertyChanged += OnActiveTimeEntryManagerPropertyChanged;
        }

        public async Task Init ()
        {
            IsLoading = true;

            await Model.LoadAsync ();

            if (Model.Workspace == null || Model.Workspace.Id == Guid.Empty) {
                Model = null;
            }

            IsLoading = false;
        }

        public void Dispose ()
        {
            if (timeEntryManager != null) {
                timeEntryManager.PropertyChanged -= OnActiveTimeEntryManagerPropertyChanged;
                timeEntryManager = null;
            }
            Model = null;
        }

        private void OnActiveTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyActive) {
                var data = timeEntryManager.Active;
                if (data != null) {
                    if (Model == null) {
                        Model = new TimeEntryModel (data);
                    } else {
                        Model.Data = data;
                    }
                }
            }
        }

        public TimeEntryModel Model { get; set; }

        public bool IsLoading  { get; set; }

        public bool IsProcessingAction { get; set; }

        public TimeEntriesCollectionView TimeEntryList
        {
            get {

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

        public async Task StartTimeEntry ()
        {
            // Protect from double clicks
            if (isProcessingAction) {
                return;
            }
        }

        public async Task StopTimeEntry ()
        {
            // Protect from double clicks
            if (isProcessingAction) {
                return;
            }

            await entry.StopAsync ();

            // Ping analytics
            ServiceContainer.Resolve<ITracker> ().SendTimerStopEvent (TimerStopSource.App);
        }

        private async void OnActionButtonClicked (object sender, EventArgs e)
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
                entry = new TimeEntryModel (new TimeEntryData (entry.Data));

                var showProjectSelection = false;

                try {
                    if (entry.State == TimeEntryState.New && entry.StopTime.HasValue) {
                        await entry.StoreAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppManual);
                    } else if (entry.State == TimeEntryState.Running) {

                    } else {
                        await entry.StartAsync ();

                        // Ping analytics
                        ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppNew);
                        OpenTimeEntryEdit (entry);
                    }
                } catch (Exception ex) {
                    var log = ServiceContainer.Resolve<ILogger> ();
                    log.Warning (LogTag, ex, "Failed to change time entry state.");
                }

                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Send (new UserTimeEntryStateChangeMessage (this, entry.Data));
            } finally {
                isProcessingAction = false;
            }
        }
    }
}
