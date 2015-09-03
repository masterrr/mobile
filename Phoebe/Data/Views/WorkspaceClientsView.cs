using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Views
{
    public class WorkspaceClientsView : ICollectionDataView<object>, IDisposable
    {
        private readonly List<WorkspaceClientsView.Client> dataObjects = new List<WorkspaceClientsView.Client> ();
        private UserData userData;
        private Workspace currentWorkspace;
        private Subscription<DataChangeMessage> subscriptionDataChange;
        private bool isLoading;
        private bool hasMore;
        private Guid workspaceId;

        public WorkspaceClientsView (Guid workspaceId)
        {
            this.workspaceId = workspaceId;
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);

            Reload ();
        }

        public void Dispose ()
        {
            if (subscriptionDataChange != null) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
            }
        }

        private void OnDataChange (DataChangeMessage msg)
        {
            if (msg.Data is UserData) {
                OnDataChange ((UserData)msg.Data);
            } else if (msg.Data is ClientData) {
                OnDataChange ((ClientData)msg.Data, msg.Action);
            }
        }

        private void OnDataChange (UserData data)
        {
            var existingData = userData;

            userData = data;
            if (existingData == null || existingData.DefaultWorkspaceId != data.DefaultWorkspaceId) {
                OnUpdated ();
            }

            userData = data;
        }

        private void OnDataChange (ClientData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue;

            var existingData = dataObjects.FirstOrDefault (item => data.Matches (item));

            if (isExcluded) {
                if (existingData != null) {
                    dataObjects.Remove (existingData);
                }
            } else {
                var client = new Client (data);

                dataObjects.Add (client);
            }
        }

        private void OnUpdated ()
        {
            DispatchCollectionEvent (CollectionEventBuilder.GetEvent (NotifyCollectionChangedAction.Reset, -1, -1));
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private void DispatchCollectionEvent (NotifyCollectionChangedEventArgs args)
        {
            var handler = CollectionChanged;
            if (handler != null) {
                handler (this, args);
            }
        }

        public async void Reload ()
        {
            if (IsLoading) {
                return;
            }

            var bus = ServiceContainer.Resolve<MessageBus> ();
            var shouldSubscribe = subscriptionDataChange != null;

            if (subscriptionDataChange != null) {
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
                shouldSubscribe = true;
            }

            try {
                var store = ServiceContainer.Resolve<IDataStore> ();

                userData = ServiceContainer.Resolve<AuthManager> ().User;
                var userId = userData != null ? userData.Id : (Guid?)null;

                IsLoading = true;
                dataObjects.Clear ();

                var clientsTask = store.Table<ClientData> ()
                                  .QueryAsync (r => r.DeletedAt == null && r.WorkspaceId == workspaceId);

                await clientsTask;
                var clients = clientsTask.Result;
                dataObjects.Add (new Client (workspaceId));

                foreach (var clientData in clients) {
                    var client = new Client (clientData);
                    dataObjects.Add (client);
                }

                SortClients (dataObjects);
            } finally {
                IsLoading = false;
                UpdateCollection ();

                if (shouldSubscribe) {
                    subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
                }
            }
        }

        private static void SortClients (List<Client> data)
        {
            data.Sort ((a, b) => {
                if (a.IsNewClient != b.IsNewClient) {
                    return a.IsNewClient ? -1 : 1;
                }
                return String.Compare (
                           a.Data.Name ?? String.Empty,
                           b.Data.Name ?? String.Empty,StringComparison.OrdinalIgnoreCase
                       );
            });
        }

        public void LoadMore () {}

        private void UpdateCollection ()
        {
            OnUpdated ();
        }

        public IEnumerable<object> Data
        {
            get {
                return dataObjects;
            }
        }

        public int Count
        {
            get {
                return dataObjects.Count;
            }
        }

        public event EventHandler OnHasMoreChanged;

        public bool HasMore
        {
            get {
                return hasMore;
            }
            private set {
                hasMore = value;
                if (OnHasMoreChanged != null) {
                    OnHasMoreChanged (this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler OnIsLoadingChanged;

        public bool IsLoading
        {
            get {
                return isLoading;
            }
            private set {
                isLoading = value;
                if (OnIsLoadingChanged != null) {
                    OnIsLoadingChanged (this, EventArgs.Empty);
                }
            }
        }

        public class Client
        {
            private ClientData dataObject;
            private readonly Guid workspaceId;

            public Client (ClientData dataObject)
            {
                this.dataObject = dataObject;
                workspaceId = dataObject.WorkspaceId;
            }

            public Client (Guid wsId)
            {
                dataObject = null;
                workspaceId = wsId;
            }

            public bool IsNewClient
            {
                get { return dataObject == null; }
            }

            public Guid WorkspaceId
            {
                get { return dataObject != null ? dataObject.WorkspaceId : workspaceId; }
            }

            public ClientData Data
            {
                get { return dataObject; }
                set {
                    if (value == null) {
                        throw new ArgumentNullException ("value");
                    }

                    if (dataObject.Id != value.Id) {
                        throw new ArgumentException ("Cannot change Id of the project.", "value");
                    }
                    dataObject = value;
                }
            }
        }

        public class Workspace
        {
            private WorkspaceData dataObject;
            private readonly List<Client> clients = new List<Client> ();

            public Workspace (WorkspaceData dataObject)
            {
                this.dataObject = dataObject;
            }

            public WorkspaceData Data
            {
                get { return dataObject; }
                set {
                    if (value == null) {
                        throw new ArgumentNullException ("value");
                    }
                    if (dataObject.Id != value.Id) {
                        throw new ArgumentException ("Cannot change Id of the workspace.", "value");
                    }
                    dataObject = value;
                }
            }

            public List<Client> Clients
            {
                get { return clients; }
            }
        }
    }
}