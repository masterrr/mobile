using System;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data;
using NUnit.Framework;
using System.Linq;

namespace Toggl.Phoebe.Tests.Views
{
    public class LogTimeEntriesViewTest : TimeEntriesCollectionTest
    {
        [Test]
        public void TestUpdateQueue ()
        {
            RunAsync (async delegate {

                var view = new LogTimeEntriesView ();
                await WaitForLoaded (view);

                view.CollectionChanged += (sender, e) => {

                };

                await DataStore.PutAsync (new TimeEntryData () {
                    RemoteId = 6,
                    State = TimeEntryState.Finished,
                    StartTime = MakeTime (16, 0),
                    StopTime = MakeTime (16, 36),
                    WorkspaceId = workspace.Id,
                    UserId = user.Id,
                    DeletedAt = MakeTime (16, 39),
                });


                view.Dispose ();
            });
        }
    }
}

