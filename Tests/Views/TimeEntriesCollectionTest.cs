using System.Threading.Tasks;
using Moq;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json.Converters;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Views
{
    public class TimeEntriesCollectionTest : CollectionDataViewTest
    {
        protected WorkspaceData workspace;
        protected UserData user;

        protected AuthManager AuthManager
        {
            get { return ServiceContainer.Resolve<AuthManager> (); }
        }

        public override void SetUp ()
        {
            base.SetUp ();

            RunAsync (async delegate {
                await CreateTestData ();

                ServiceContainer.Register<ISyncManager> (Mock.Of<ISyncManager> (
                            (mgr) => mgr.IsRunning == false));
                ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                            (store) => store.ApiToken == "test" &&
                            store.UserId == user.Id));
                ServiceContainer.Register<AuthManager> (new AuthManager ());
                ServiceContainer.Register<TimeEntryJsonConverter> (new TimeEntryJsonConverter ());
            });
        }

        protected async Task CreateTestData ()
        {
            workspace = await DataStore.PutAsync (new WorkspaceData {
                RemoteId = 1,
                Name = "Unit Testing",
            });

            user = await DataStore.PutAsync (new UserData {
                RemoteId = 2,
                Name = "Tester",
                DefaultWorkspaceId = workspace.Id,
            });

            var project = await DataStore.PutAsync (new ProjectData {
                RemoteId = 3,
                Name = "Ad design",
                WorkspaceId = workspace.Id,
            });

            await DataStore.PutAsync (new TimeEntryData {
                RemoteId = 1,
                Description = "Initial concept",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (09, 12),
                StopTime = MakeTime (10, 1),
                ProjectId = project.Id,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData {
                RemoteId = 2,
                Description = "Breakfast",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (10, 5),
                StopTime = MakeTime (10, 30),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData {
                RemoteId = 3,
                Description = "Programmers meeting",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (10, 35),
                StopTime = MakeTime (12, 21),
                ProjectId = project.Id,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData {
                RemoteId = 4,
                Description = "Design stuff",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (12, 25),
                StopTime = MakeTime (13, 57),
                ProjectId = project.Id,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData {
                RemoteId = 5,
                Description = "Bug fixed",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (14, 0),
                StopTime = MakeTime (14, 36),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });
        }
    }
}

