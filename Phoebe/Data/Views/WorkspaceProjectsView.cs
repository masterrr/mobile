using System;
using System.Collections;
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
    public class WorkspaceProjectsView : ICollectionDataView<object>, IDisposable
    {
        private readonly List<ClientData> clientDataObjects = new List<ClientData> ();
        private readonly List<object> dataObjects = new List<object> ();
        private Workspace currentWorkspace;
        private UserData userData;
        private Subscription<DataChangeMessage> subscriptionDataChange;
        private bool isLoading;
        private bool hasMore;

        private int displayTaskForProjectPosition;

        private Project displayingTaskForProject;
        private Guid workspaceId;
        public Project DisplayingTaskForProject
        {
            get {
                return displayingTaskForProject;
            }
        }

        public bool SortByClients { private set; get; }

        public WorkspaceProjectsView (Guid workspaceId,  bool sortByClients = true)
        {
            SortByClients = sortByClients;
            this.workspaceId = workspaceId;
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);

            Reload ();
        }

        public void ShowTaskForProject (Project project, int position, out int collapsingCount)
        {
            collapsingCount = displayingTaskForProject == null ? 0 : displayingTaskForProject.Tasks.Count;

            if (displayingTaskForProject == project) {
                collapsingCount = 0;
                displayingTaskForProject = null;
            } else {
                displayingTaskForProject = project;
            }

            if (displayTaskForProjectPosition > position) {
                collapsingCount = 0;
            }

            displayTaskForProjectPosition = position;
            UpdateCollection ();
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
            } else if (msg.Data is WorkspaceData) {
                OnDataChange ((WorkspaceData)msg.Data, msg.Action);
            } else if (msg.Data is ProjectData) {
                OnDataChange ((ProjectData)msg.Data, msg.Action);
            } else if (msg.Data is TaskData) {
                OnDataChange ((TaskData)msg.Data, msg.Action);
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

        private void OnDataChange (WorkspaceData data, DataAction action)
        {
            var existingWorkspace = currentWorkspace;
            currentWorkspace = new Workspace (data);
            if (existingWorkspace == null || existingWorkspace.Data.Id != data.Id) {
                OnUpdated ();
            }
            currentWorkspace = new Workspace (data);
        }

        private void OnDataChange (ProjectData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue
                             || !data.IsActive;

            Project project;

            if (isExcluded) {
                if (FindProject (data.Id, out project)) {
                    currentWorkspace.Projects.Remove (project);
                    UpdateCollection ();
                }
            } else {
                data = new ProjectData (data);
                if (FindProject (data.Id, out project)) {
                    var existingData = project.Data;

                    var shouldSort = existingData.Name != data.Name
                                     || existingData.ClientId != data.ClientId;

                    if (shouldSort) {
                        SortProjects (currentWorkspace.Projects, clientDataObjects, SortByClients);
                    }
                    UpdateCollection();
                } else if (data.WorkspaceId == currentWorkspace.Data.Id) {
                    project = new Project (data);
                    currentWorkspace.Projects.Add (project);
                    SortProjects (currentWorkspace.Projects, clientDataObjects, SortByClients);
                    UpdateCollection();
                }
            }
        }

        private void OnDataChange (TaskData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue
                             || !data.IsActive;

            Project project;
            TaskData existingData;

            if (isExcluded) {
                if (FindTask (data.Id, out project, out existingData)) {
                    project.Tasks.Remove (existingData);
                    UpdateCollection ();
                }
            } else {
                data = new TaskData (data);

                if (FindTask (data.Id, out project, out existingData)) {
                    var shouldReparent = existingData.ProjectId != data.ProjectId;
                    var shouldSort = existingData.Name != data.Name
                                     || shouldReparent;

                    if (shouldReparent) {
                        project.Tasks.Remove (existingData);

                        if (FindProject (data.ProjectId, out project)) {
                            project.Tasks.Add (data);
                        }
                    } else {
                        project.Tasks.UpdateData (data);
                    }

                    if (shouldSort) {
                        SortTasks (project.Tasks);
                    }
                    UpdateCollection ();
                } else if (FindProject (data.ProjectId, out project)) {
                    project.Tasks.Add (data);
                    SortTasks (project.Tasks);
                    UpdateCollection ();
                }
            }
        }

        private void OnDataChange (ClientData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue;

            var existingData = clientDataObjects.FirstOrDefault (item => data.Matches (item));

            if (isExcluded) {
                if (existingData != null) {
                    clientDataObjects.Remove (existingData);
                }
            } else {
                data = new ClientData (data);

                if (existingData != null) {
                    clientDataObjects.UpdateData (data);

                    var shouldSort = data.Name != existingData.Name;
                    if (shouldSort) {
                        SortProjects (currentWorkspace.Projects, clientDataObjects, SortByClients);
                    }
                } else {
                    clientDataObjects.Add (data);
                }
            }
        }

        private bool FindProject (Guid id, out Project project)
        {
            foreach (var proj in currentWorkspace.Projects) {
                if (proj.Data != null && proj.Data.Id == id) {
                    project = proj;
                    return true;
                }
            }

            project = null;
            return false;
        }

        private bool FindTask (Guid id, out Project project, out TaskData existingData)
        {
            foreach (var proj in currentWorkspace.Projects) {
                foreach (var task in proj.Tasks) {
                    if (task.Id == id) {
                        project = proj;
                        existingData = task;
                        return true;
                    }
                }
            }

            project = null;
            existingData = null;
            return false;
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
                clientDataObjects.Clear ();

                var workspaceTask = store.Table<WorkspaceData> ()
                                    .QueryAsync (r => r.Id == workspaceId);
                var projectsTask = store.GetUserAccessibleProjects (userId ?? Guid.Empty);
                var tasksTask = store.Table<TaskData> ()
                                .QueryAsync (r => r.DeletedAt == null && r.IsActive == true && r.WorkspaceId == workspaceId);
                var clientsTask = store.Table<ClientData> ()
                                  .QueryAsync (r => r.DeletedAt == null && r.WorkspaceId == workspaceId);

                currentWorkspace = new Workspace (workspaceTask.Result.First());

                await Task.WhenAll (workspaceTask, projectsTask, tasksTask, clientsTask);

                var projects = projectsTask.Result.Where (r => r.WorkspaceId == currentWorkspace.Data.Id);

                var clients = clientsTask.Result;

                foreach (var client in clients) {
                    var cl = new Client (client);

                    var clientProjectList = new List<Project> ();
                    var clientProjects = projects.Where (r => r.ClientId == client.Id);

                    foreach (var projectData in clientProjects) {
                        var project = new Project (projectData);

                        var tasks = tasksTask.Result.Where (r => r.ProjectId == projectData.Id);
                        project.Tasks.AddRange (tasks);

                        cl.Projects.Add (project);
                    }
                    currentWorkspace.Clients.Add (cl);
                }

                clientDataObjects.AddRange (clients);
                SortClients (currentWorkspace.Clients);
                foreach (var client in currentWorkspace.Clients) {
                    SortProjects (client.Projects, new List<ClientData> (), false);
                    foreach (var project in client.Projects) {
                        SortTasks (project.Tasks);
                    }
                }
            } finally {
                IsLoading = false;
                UpdateCollection ();

                if (shouldSubscribe) {
                    subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
                }
            }
        }

        private static void SortProjects (List<Project> data, List<ClientData> clients, bool sortByClients)
        {
            data.Sort ((a, b) => {
                if (a.IsNoProject != b.IsNoProject) {
                    return a.IsNoProject ? -1 : 1;
                }

                var res = 0;

                if (!sortByClients) {
                    var aName = a.Data != null ? (a.Data.Name ?? String.Empty) : String.Empty;
                    var bName = b.Data != null ? (b.Data.Name ?? String.Empty) : String.Empty;
                    res = String.Compare (aName, bName, StringComparison.Ordinal);
                }

                // Try to order by client name when same project name
                if (res == 0) {
                    var aClient = a.Data != null ? clients.FirstOrDefault (r => r.Id == a.Data.ClientId) : null;
                    var bClient = b.Data != null ? clients.FirstOrDefault (r => r.Id == b.Data.ClientId) : null;

                    var aClientName = aClient != null ? aClient.Name ?? String.Empty : String.Empty;
                    var bClientName = bClient != null ? bClient.Name ?? String.Empty : String.Empty;

                    res = String.Compare (aClientName, bClientName, StringComparison.Ordinal);
                }

                return res;
            });
        }

        private static void SortClients (List<Client> data)
        {
            data.Sort ((a, b) => String.Compare (
                           a.Data.Name ?? String.Empty,
                           b.Data.Name ?? String.Empty,
                           StringComparison.Ordinal
                       ));
        }
        private static void SortTasks (List<TaskData> data)
        {
            data.Sort ((a, b) => String.Compare (
                           a.Name ?? String.Empty,
                           b.Name ?? String.Empty,
                           StringComparison.Ordinal
                       ));
        }

        public void LoadMore () {}

        private void UpdateCollection ()
        {
            dataObjects.Clear();

            if (currentWorkspace == null || currentWorkspace.Projects == null || currentWorkspace.Clients == null) {
                return;
            }
            foreach (var client in currentWorkspace.Clients) {
                if (client.Projects.Count == 0) {
                    continue;
                }
                dataObjects.Add (client);
                foreach (var project in client.Projects) {
                    dataObjects.Add (project);
                    if (displayingTaskForProject != null && project == displayingTaskForProject) {
                        foreach (var task in project.Tasks) {
                            dataObjects.Add (task);
                        }
                    }
                }
            }

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

        public static async Task<List<WorkspaceData>> GetWorkspaces() // TODO: Add sort mechanism.
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var workspacesTask = store.Table<WorkspaceData> ()
                                 .QueryAsync (r => r.DeletedAt == null);
            await Task.WhenAll (workspacesTask);
            return SortWorkspaces (workspacesTask.Result);
        }

        private static List<WorkspaceData> SortWorkspaces (List<WorkspaceData> data)
        {
            var user = ServiceContainer.Resolve<AuthManager> ().User;

            data.Sort ((a, b) => {
                if (user != null) {
                    if (a != null && a.Id == user.DefaultWorkspaceId) {
                        return -1;
                    }
                    if (b != null && b.Id == user.DefaultWorkspaceId) {
                        return 1;
                    }
                }
                var aName = a != null ? (a.Name ?? String.Empty) : String.Empty;
                var bName = b != null ? (b.Name ?? String.Empty) : String.Empty;
                return String.Compare (aName, bName, StringComparison.Ordinal);
            });
            return data;
        }

        public class Workspace
        {
            private WorkspaceData dataObject;
            private readonly List<Project> projects = new List<Project> ();
            private readonly List<Client> clients = new List<Client> ();

            public Workspace (WorkspaceData dataObject)
            {
                this.dataObject = dataObject;
                projects.Add (new Project (dataObject));
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

            public List<Project> Projects
            {
                get { return projects; }
            }

            public List<Client> Clients
            {
                get { return clients; }
            }
        }

        public class Client
        {
            private ClientData dataObject;
            private readonly Guid workspaceId;
            private readonly List<Project> projects = new List<Project> ();

            public Client (ClientData dataObject)
            {
                this.dataObject = dataObject;
                workspaceId = dataObject.WorkspaceId;
            }

            public Client (WorkspaceData workspaceData)
            {
                dataObject = null;
                workspaceId = workspaceData.Id;
            }

            public bool IsNoClient
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

            public List<Project> Projects
            {
                get { return projects; }
            }
        }

        public class Project
        {
            private ProjectData dataObject;
            private readonly List<TaskData> tasks = new List<TaskData> ();
            private readonly Guid workspaceId;

            public Project (ProjectData dataObject)
            {
                this.dataObject = dataObject;
                workspaceId = dataObject.WorkspaceId;
            }

            public Project (WorkspaceData workspaceData)
            {
                dataObject = null;
                workspaceId = workspaceData.Id;
            }

            public bool IsNoProject
            {
                get { return dataObject == null; }
            }

            public bool IsNewProject
            {
                get { return dataObject != null && dataObject.Id == Guid.Empty; }
            }

            public Guid WorkspaceId
            {
                get { return dataObject != null ? dataObject.WorkspaceId : workspaceId; }
            }

            public ProjectData Data
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

            public List<TaskData> Tasks
            {
                get { return tasks; }
            }
        }

    }
}