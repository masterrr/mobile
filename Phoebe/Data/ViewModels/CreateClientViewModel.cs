using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    public class CreateClientViewModel : IViewModel<ClientModel>
    {
        private ClientModel model;
        private bool isLoading;
        private Guid workspaceId;
        private WorkspaceModel workspaceModel;

        public CreateClientViewModel (Guid workspaceId)
        {
            this.workspaceId = workspaceId;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "New Client Screen";
        }

        public async void Init ()
        {
            try {
                var user = ServiceContainer.Resolve<AuthManager> ().User;
                if (user == null) {
                    model = null;
                    return;
                }


                workspaceModel = new WorkspaceModel (workspaceId);
                await workspaceModel.LoadAsync ();

                model = new ClientModel {
                    Workspace = workspaceModel
                };

                if (model.Workspace == null || model.Workspace.Id == Guid.Empty) {
                    model = null;
                }
            } catch (Exception ex) {
                model = null;
            } finally {
                IsLoading = false;
            }
        }

        public async Task AssignClient (string clientName, ProjectModel project)
        {
            var store = ServiceContainer.Resolve<IDataStore>();
            var existing = await store.Table<ClientData>()
                           .QueryAsync (r => r.WorkspaceId == workspaceId && r.Name == clientName)
                           .ConfigureAwait (false);

            if (existing.Count > 0) {
                model = new ClientModel (existing [0]);
            } else {
                model.Name = clientName;
            }
            await model.SaveAsync ();
            project.Client = model;
        }

        public void Dispose ()
        {
        }

        public event EventHandler OnModelChanged;

        public ClientModel Model
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

        public event EventHandler OnIsLoadingChanged;

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
    }
}

