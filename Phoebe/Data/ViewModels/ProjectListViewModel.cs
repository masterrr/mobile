using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    public class ProjectListViewModel : IViewModel<ITimeEntryModel>
    {
        private bool isLoading;
        private ITimeEntryModel model;
        private IList<TimeEntryData> timeEntryList;
        private WorkspaceProjectsView projectList;
        private Guid workspaceId;
        private bool sortByClients;

        public ProjectListViewModel (IList<TimeEntryData> timeEntryList, Guid workspaceId, bool sortByClients = true)
        {
            this.sortByClients = sortByClients;
            this.timeEntryList = timeEntryList;
            this.workspaceId = workspaceId;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Select Project";
        }

        public async Task Init ()
        {
            IsLoading = true;

            // Create model.
            if (timeEntryList.Count > 1) {
                model = new TimeEntryGroup (timeEntryList);
            } else if (timeEntryList.Count == 1) {
                model = new TimeEntryModel (timeEntryList [0]);
            }

            await model.LoadAsync ();

            if (model.Workspace == null || model.Workspace.Id == Guid.Empty) {
                model = null;
            }

            IsLoading = false;
        }

        public void Dispose ()
        {
            projectList.Dispose ();
            model = null;
        }

        public WorkspaceProjectsView ProjectList
        {
            get {
                if (projectList == null) {
                    projectList = new WorkspaceProjectsView (workspaceId, sortByClients);
                }
                return projectList;
            }
        }

        public event EventHandler OnModelChanged;

        public ITimeEntryModel Model
        {
            get {
                return model;
            }

            private set {

                model = value;

                if (OnModelChanged != null) {
                    OnModelChanged (this, EventArgs.Empty);
                }
            }
        }

        public IList<TimeEntryData> TimeEntryList
        {
            get {
                return timeEntryList;
            }
        }

        public async Task SaveModelAsync (ProjectModel project, WorkspaceModel workspace, TaskData task = null)
        {
            model.Project = project;
            model.Workspace = workspace;
            if (task != null) {
                model.Task = new TaskModel (task);
            }
            await model.SaveAsync ();
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
    }
}
