using System;
using System.Linq.Expressions;
using Foundation;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using XPlatUtils;

namespace Toggl.Ross.Data
{
    public class SettingsStore : ISettingsStore
    {
        private const string PhoebeUserIdKey = "phoebeUserId";
        private const string PhoebeApiTokenKey = "phoebeApiToken";
        private const string PhoebeSyncLastRunKey = "phoebeSyncLastRun";
        private const string PhoebeUseDefaultTagKey = "phoebeUseDefaultTag";
        private const string PhoebeLastAppVersionKey = "phoebeLastAppVersion";
        private const string PhoebeExperimentIdKey = "phoebeExperimentId";
        private const string PhoebeLastReportZoomKey = "lastReportZoomKey";
        private const string PhoebeGroupedEntriesKey = "groupedEntriesKey";
        private const string RossInstallIdKey = "rossInstallId";
        private const string RossPreferredStartViewKey = "rossPreferredStartView";
        private const string RossChooseProjectForNewKey = "rossChooseProjectForNew";
        private const string RossReadDurOnlyNoticeKey = "rossReadDurOnlyNotice";
        private const string RossIgnoreSyncErrorsUntilKey = "rossIgnoreSyncErrorsUntil";

        private static string GetPropertyName<T> (Expression<Func<SettingsStore, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        protected Guid? GetGuid (string key)
        {
            var val = (string) (NSString)NSUserDefaults.StandardUserDefaults [key];
            if (String.IsNullOrEmpty (val)) {
                return null;
            }
            return Guid.Parse (val);
        }

        protected void SetGuid (string key, Guid? value)
        {
            if (value != null) {
                NSUserDefaults.StandardUserDefaults [key] = (NSString)value.Value.ToString ();
            } else {
                NSUserDefaults.StandardUserDefaults.RemoveObject (key);
            }
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        protected string GetString (string key)
        {
            return (string) (NSString)NSUserDefaults.StandardUserDefaults [key];
        }

        protected void SetString (string key, string value)
        {
            if (value != null) {
                NSUserDefaults.StandardUserDefaults [key] = (NSString)value;
            } else {
                NSUserDefaults.StandardUserDefaults.RemoveObject (key);
            }
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        protected int? GetInt (string key)
        {
            var raw = NSUserDefaults.StandardUserDefaults [key];
            if (raw == null) {
                return null;
            }
            return (int) (NSNumber)raw;
        }

        protected void SetInt (string key, int? value)
        {
            if (value != null) {
                NSUserDefaults.StandardUserDefaults [key] = (NSNumber)value.Value;
            } else {
                NSUserDefaults.StandardUserDefaults.RemoveObject (key);
            }
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        protected DateTime? GetDateTime (string key)
        {
            var raw = NSUserDefaults.StandardUserDefaults [key];
            if (raw == null) {
                return null;
            }
            return DateTime.FromBinary ((long) (NSNumber)raw);
        }

        protected void SetDateTime (string key, DateTime? value)
        {
            if (value != null) {
                NSUserDefaults.StandardUserDefaults [key] = (NSNumber)value.Value.ToBinary ();
            } else {
                NSUserDefaults.StandardUserDefaults.RemoveObject (key);
            }
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        protected void OnSettingChanged (string name)
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            bus.Send (new SettingChangedMessage (this, name));
        }

        public static readonly string PropertyUserId = GetPropertyName (s => s.UserId);

        public Guid? UserId
        {
            get { return GetGuid (PhoebeUserIdKey); }
            set {
                SetGuid (PhoebeUserIdKey, value);
                OnSettingChanged (PropertyUserId);
            }
        }

        public string InstallId
        {
            get {
                var val = GetString (RossInstallIdKey);
                if (String.IsNullOrEmpty (val)) {
                    val = Guid.NewGuid ().ToString ();
                    SetString (RossInstallIdKey, val);
                }
                return val;
            }
        }

        public static readonly string PropertyApiToken = GetPropertyName (s => s.ApiToken);

        public string ApiToken
        {
            get { return GetString (PhoebeApiTokenKey); }
            set {
                SetString (PhoebeApiTokenKey, value);
                OnSettingChanged (PropertyApiToken);
            }
        }

        public static readonly string PropertySyncLastRun = GetPropertyName (s => s.SyncLastRun);

        public DateTime? SyncLastRun
        {
            get { return GetDateTime (PhoebeSyncLastRunKey); }
            set {
                SetDateTime (PhoebeSyncLastRunKey, value);
                OnSettingChanged (PropertySyncLastRun);
            }
        }

        public static readonly string PropertyUseDefaultTag = GetPropertyName (s => s.UseDefaultTag);

        public bool UseDefaultTag
        {
            get { return (GetInt (PhoebeUseDefaultTagKey) ?? 1) == 1; }
            set {
                SetInt (PhoebeUseDefaultTagKey, value ? 1 : 0);
                OnSettingChanged (PropertyUseDefaultTag);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.DefaultMobileTag);
            }
        }

        public static readonly string PropertyGroupedTimeEntries = GetPropertyName (s => s.GroupedTimeEntries);

        public bool GroupedTimeEntries
        {
            get { return (GetInt (PhoebeGroupedEntriesKey) ?? 1) == 1; }
            set {
                SetInt (PhoebeGroupedEntriesKey, value ? 1 : 0);
                OnSettingChanged (PropertyGroupedTimeEntries);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.GroupedTimeEntries);
            }
        }

        public static readonly string PropertyLastAppVersion = GetPropertyName (s => s.LastAppVersion);

        public string LastAppVersion
        {
            get { return GetString (PhoebeLastAppVersionKey); }
            set { SetString (PhoebeLastAppVersionKey, value); }
        }

        public static readonly string PropertyExperimentId = GetPropertyName (s => s.ExperimentId);

        public string ExperimentId
        {
            get { return GetString (PhoebeExperimentIdKey); }
            set {
                SetString (PhoebeExperimentIdKey, value);
                OnSettingChanged (PropertyExperimentId);
            }
        }

        public static readonly string PropertyPreferredStartView = GetPropertyName (s => s.PreferredStartView);

        public string PreferredStartView
        {
            get { return GetString (RossPreferredStartViewKey); }
            set {
                SetString (RossPreferredStartViewKey, value);
                OnSettingChanged (PropertyPreferredStartView);
            }
        }

        public static readonly string PropertyChooseProjectForNew = GetPropertyName (s => s.ChooseProjectForNew);

        public bool ChooseProjectForNew
        {
            get { return (GetInt (RossChooseProjectForNewKey) ?? 1) == 1; }
            set {
                SetInt (RossChooseProjectForNewKey, value ? 1 : 0);
                OnSettingChanged (PropertyChooseProjectForNew);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.AskForProject);
            }
        }

        public static readonly string PropertyReadDurOnlyNotice = GetPropertyName (s => s.ReadDurOnlyNotice);

        public bool ReadDurOnlyNotice
        {
            get { return GetInt (RossReadDurOnlyNoticeKey) == 1; }
            set {
                SetInt (RossReadDurOnlyNoticeKey, value ? 1 : 0);
                OnSettingChanged (PropertyReadDurOnlyNotice);
            }
        }

        public static readonly string PropertyIgnoreSyncErrorsUntil = GetPropertyName (s => s.IgnoreSyncErrorsUntil);

        public DateTime? IgnoreSyncErrorsUntil
        {
            get { return GetDateTime (RossIgnoreSyncErrorsUntilKey); }
            set {
                SetDateTime (RossIgnoreSyncErrorsUntilKey, value);
                OnSettingChanged (PropertyIgnoreSyncErrorsUntil);
            }
        }

        public static readonly string PropertyLastReportZoomViewed = GetPropertyName (s => s.LastReportZoomViewed);

        public int? LastReportZoomViewed
        {
            get { return GetInt (PhoebeLastReportZoomKey); }
            set {
                SetInt (PhoebeLastReportZoomKey, value);
                OnSettingChanged (PropertyLastReportZoomViewed);
            }
        }
    }
}
