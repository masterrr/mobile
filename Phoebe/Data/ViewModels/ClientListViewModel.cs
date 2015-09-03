using System;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    public class ClientListViewModel : IViewModel<ClientModel>
    {
        private bool isLoading;
        private ClientModel model;
        private WorkspaceClientsView clientList;
        private Guid workspaceId;

        public ClientListViewModel (Guid workspaceId)
        {
            this.workspaceId = workspaceId;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Select Project";
        }

        public async Task Init ()
        {
            IsLoading = true;

            model = new ClientModel (workspaceId);
            await model.LoadAsync ();

            if (model.Workspace == null || model.Workspace.Id == Guid.Empty) {
                model = null;
            }

            IsLoading = false;
        }

        public void Dispose ()
        {
            clientList.Dispose ();
            model = null;
        }

        public WorkspaceClientsView ClientList
        {
            get {
                if (clientList == null) {
                    clientList = new WorkspaceClientsView (workspaceId);
                }
                return clientList;
            }
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

        public async Task SaveModelAsync (ProjectModel project, WorkspaceModel workspace, TaskData task = null)
        {
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
