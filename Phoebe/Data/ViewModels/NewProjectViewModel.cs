using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    public class NewProjectViewModel : IViewModel<ProjectModel>
    {
        private bool isLoading;
        private ProjectModel model;
        private WorkspaceModel workspaceModel;
        private Guid workspaceId;
        private List<TimeEntryData> timeEntryList;

        public NewProjectViewModel (List<TimeEntryData> timeEntryList)
        {
            this.timeEntryList = timeEntryList;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "New Project";
        }

        public void Dispose ()
        {
            workspaceModel = null;
            model = null;
        }

        public ProjectModel Model
        {
            get {
                return model;
            }
        }

        public event EventHandler OnIsLoadingChanged;

        public event EventHandler OnModelChanged;

        public bool IsLoading
        {
            get {
                return isLoading;
            }
            private set {

                if (isLoading  == value) {
                    return;
                }

                isLoading = value;

                if (OnIsLoadingChanged != null) {
                    OnIsLoadingChanged (this, EventArgs.Empty);
                }
            }
        }

        public async Task Init ()
        {
            IsLoading = true;

            try {
                var user = ServiceContainer.Resolve<AuthManager> ().User;
                if (user == null) {
                    model = null;
                    return;
                }

                workspaceId = timeEntryList[0].WorkspaceId;
                workspaceModel = new WorkspaceModel (workspaceId);
                await workspaceModel.LoadAsync ();

                model = new ProjectModel {
                    Workspace = workspaceModel,
                    IsActive = true,
                    IsPrivate = true
                };

                if (model.Workspace == null || model.Workspace.Id == Guid.Empty) {
                    model = null;
                }
            } catch (Exception ex) {
                model = null;
            } finally {
                model.PropertyChanged += OnModelChange;
                IsLoading = false;
            }
        }

        private void OnModelChange (object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (OnModelChanged != null) {
                OnModelChanged (this, EventArgs.Empty);
            }
        }

        public async Task SaveProjectModel ()
        {
            // Save new project.
            await model.SaveAsync();

            // Create an extra model for Project / User relationship
            var userData = ServiceContainer.Resolve<AuthManager> ().User;
            var userId = userData != null ? userData.Id : (Guid?)null;

            if (userId.HasValue) {
                var projectUserModel = new ProjectUserModel ();
                projectUserModel.Project = model;
                projectUserModel.User = new UserModel (userId.Value);
                await projectUserModel.SaveAsync ();
            }

            // Update entry list.
            var timeEntryGroup = new TimeEntryGroup (timeEntryList);
            timeEntryGroup.Project = model;
            timeEntryGroup.Workspace = workspaceModel;
            await timeEntryGroup.SaveAsync ();
        }

        public async Task<bool> ExistProjectWithName (string projectName)
        {
            var dataStore = ServiceContainer.Resolve<IDataStore> ();
            Guid clientId = (model.Client == null) ? Guid.Empty : model.Client.Id;
            var existWithName = await dataStore.Table<ProjectData>().ExistWithNameAsync (projectName, clientId);

            return existWithName;
        }
    }
}

