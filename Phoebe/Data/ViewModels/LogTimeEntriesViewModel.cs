using System.ComponentModel;
using System.Threading.Tasks;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class LogTimeEntriesViewModel : IVModel<TimeEntryModel>
    {
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private ActiveTimeEntryManager timeEntryManager;
        private TimeEntryModel model;

        public LogTimeEntriesViewModel ()
        {
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "TimeEntryList Screen";
            timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            timeEntryManager.PropertyChanged += OnActiveTimeEntryManagerPropertyChanged;

            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
        }

        public Task Init ()
        {
            IsLoading = true;

            SyncModel ();
            SyncCollectionView ();

            IsLoading = false;
        }

        public void Dispose ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionSettingChanged != null) {
                bus.Unsubscribe (subscriptionSettingChanged);
                subscriptionSettingChanged = null;
            }

            timeEntryManager.PropertyChanged -= OnActiveTimeEntryManagerPropertyChanged;
            timeEntryManager = null;

            model = null;
        }

        public bool IsLoading  { get; set; }

        public bool IsProcessingAction { get; set; }

        public bool IsTimeEntryRunning { get; set; }

        public bool IsGroupedMode { get; set; }

        public TimeEntriesCollectionView CollectionView { get; set; }

        public async Task StartStopTimeEntry ()
        {
            // Protect from double clicks
            if (IsProcessingAction) {
                return;
            }

            IsProcessingAction = true;
            if (!IsTimeEntryRunning) {
                await model.StartAsync ();
            } else {
                await model.StopAsync ();
            }

            IsTimeEntryRunning = !IsTimeEntryRunning;
            IsProcessingAction = false;
        }

        private void OnActiveTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyActive) {
                SyncModel ();
            }
        }

        private void OnSettingChanged (SettingChangedMessage msg)
        {
            // Implement a GetPropertyName
            if (msg.Name == "GroupedTimeEntries") {
                SyncCollectionView ();
            }
        }

        private void SyncModel ()
        {
            var data = timeEntryManager.Active;
            if (data != null) {
                if (model == null) {
                    model = new TimeEntryModel (data);
                } else {
                    model.Data = data;
                }
            }
        }

        private void SyncCollectionView ()
        {
            IsGroupedMode = ServiceContainer.Resolve<ISettingsStore> ().GroupedTimeEntries;
            CollectionView = IsGroupedMode ? (TimeEntriesCollectionView)new GroupedTimeEntriesView () : new LogTimeEntriesView ();
        }
    }
}
