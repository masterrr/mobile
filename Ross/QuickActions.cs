using System;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using UIKit;
using XPlatUtils;

namespace Toggl.Ross
{
    public static class QuickActionsIdentifier
    {
        public const string QuickStart = "com.toggl.timer.qstart";
    }

    public class QuickActions : IDisposable
    {
        private Subscription<AuthChangedMessage> subscriptionAuthChanged;
        private Subscription<SyncFinishedMessage> subscriptionSyncFinished;

        public QuickActions ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();

            if (subscriptionAuthChanged == null) {
                subscriptionAuthChanged = bus.Subscribe<AuthChangedMessage> (OnAuthChanged);
            }

            if (subscriptionSyncFinished == null) {
                subscriptionSyncFinished = bus.Subscribe<SyncFinishedMessage> (OnSyncFinished);
            }

            checkActions ();
        }

        private void OnSyncFinished (Message msg)
        {
            if (shouldHandleAfterSync) {
                HandleShortcut (scheduledShortcut);
            }
        }

        private void OnAuthChanged (AuthChangedMessage msg)
        {
            checkActions ();
        }

        private void checkActions()
        {
            var app = UIApplication.SharedApplication;

            var authManager = ServiceContainer.Resolve<AuthManager> ();

            if (authManager.IsAuthenticated) {
                if (app.ShortcutItems.Length == 0) {
                    var qstart = new UIMutableApplicationShortcutItem (QuickActionsIdentifier.QuickStart, "Start") {
                        LocalizedSubtitle = "Will start a new time entry",
                        Icon = UIApplicationShortcutIcon.FromType (UIApplicationShortcutIconType.Play)
                    };
                    app.ShortcutItems = new UIApplicationShortcutItem[] { qstart };
                }
            } else {
                app.ShortcutItems = new UIApplicationShortcutItem[] { };
            }
        }

        private bool shouldHandleAfterSync;
        private UIApplicationShortcutItem scheduledShortcut;

        public bool HandleShortcut (UIApplicationShortcutItem shortcut, bool shouldHandleAfterSync = false)
        {
            this.shouldHandleAfterSync = shouldHandleAfterSync;
            if (shortcut == null) {
                return true;
            }

            if (shouldHandleAfterSync) {
                scheduledShortcut = shortcut;
                return true;
            }

            if (shortcut.Type == QuickActionsIdentifier.QuickStart) {
                StartNewEntry ();
                return true;
            }

            return false;
        }

        private void StartNewEntry()
        {
            var timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            var newEntry = (TimeEntryModel) timeEntryManager.Draft;
            newEntry.StartAsync ();
        }

        public void Dispose()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();

            if (subscriptionAuthChanged != null) {
                bus.Unsubscribe (subscriptionAuthChanged);
                subscriptionAuthChanged = null;
            }

            if (subscriptionSyncFinished != null) {
                bus.Unsubscribe (subscriptionSyncFinished);
                subscriptionSyncFinished = null;
            }
        }
    }
}