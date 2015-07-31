using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Net;

namespace Toggl.Phoebe.Tests.Net
{
    public class TogglClientMock : ITogglClient
    {
        #region ITogglClient implementation

        public Task<T> Create<T> (T jsonObject) where T : CommonJson
        {
            throw new NotImplementedException ();
        }

        public Task<T> Get<T> (long id) where T : CommonJson
        {
            throw new NotImplementedException ();
        }

        public Task<List<T>> List<T> () where T : CommonJson
        {
            throw new NotImplementedException ();
        }

        public Task<T> Update<T> (T jsonObject) where T : CommonJson
        {
            throw new NotImplementedException ();
        }

        public Task Delete<T> (T jsonObject) where T : CommonJson
        {
            throw new NotImplementedException ();
        }

        public Task Delete<T> (IEnumerable<T> jsonObjects) where T : CommonJson
        {
            throw new NotImplementedException ();
        }

        public Task<UserJson> GetUser (string username, string password)
        {
            throw new NotImplementedException ();
        }

        public Task<UserJson> GetUser (string googleAccessToken)
        {
            throw new NotImplementedException ();
        }

        public Task<List<ClientJson>> ListWorkspaceClients (long workspaceId)
        {
            throw new NotImplementedException ();
        }

        public Task<List<ProjectJson>> ListWorkspaceProjects (long workspaceId)
        {
            throw new NotImplementedException ();
        }

        public Task<List<WorkspaceUserJson>> ListWorkspaceUsers (long workspaceId)
        {
            throw new NotImplementedException ();
        }

        public Task<List<TaskJson>> ListWorkspaceTasks (long workspaceId)
        {
            throw new NotImplementedException ();
        }

        public Task<List<TaskJson>> ListProjectTasks (long projectId)
        {
            throw new NotImplementedException ();
        }

        public Task<List<ProjectUserJson>> ListProjectUsers (long projectId)
        {
            throw new NotImplementedException ();
        }

        public Task<List<TimeEntryJson>> ListTimeEntries (DateTime start, DateTime end)
        {
            return SimulateListTimeEntries (start, end);
        }

        public Task<List<TimeEntryJson>> ListTimeEntries (DateTime end, int days)
        {
            var startDate = end.Date;
            var endDate = startDate.Date.AddDays (-days);
            return SimulateListTimeEntries (startDate, endDate);
        }

        public Task<UserRelatedJson> GetChanges (DateTime? since)
        {
            throw new NotImplementedException ();
        }

        public Task CreateFeedback (FeedbackJson jsonObject)
        {
            throw new NotImplementedException ();
        }

        #endregion

        private async Task<List<TimeEntryJson>> SimulateListTimeEntries (DateTime start, DateTime end)
        {
            const int workspaceId = 1;
            const int userId = 2;
            const int projectId = 3;

            var random = new Random ();
            var itemNumber = random.Next (10, 50);
            var dates = GetRandomDatesFromInterval (itemNumber, start, end);
            var entries = new List<TimeEntryJson> ();

            for (int i = 0; i < itemNumber; i++) {
                var startTime = dates [i];
                var t = new TimeEntryJson {
                    Id = 1000 + i,
                    ModifiedAt = startTime,
                    IsBillable = false,
                    DurationOnly = false,
                    Tags = new List<string> {"mobile"},
                    CreatedWith = "TestMock",
                    Description = "Remote Entry " + i,
                    StartTime = startTime,
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    ProjectId = projectId,
                    Duration = random.Next (50, 18000) // Duration from 50s to 5h
                };
                entries.Add (t);
            }

            // Delay a random time.
            await Task.Delay (random.Next (500, 2000));
            return entries;
        }

        private List<DateTime> GetRandomDatesFromInterval (int numberOfDates, DateTime start, DateTime end)
        {
            var random = new Random ();
            var dates = new List<DateTime> ();
            int year;
            int month;
            int day;

            for (int i = 0; i < numberOfDates; i++) {
                year = start.Year == end.Year ? start.Year : random.Next (end.Year, start.Year);
                month = start.Month == end.Month ? start.Month : random.Next (end.Month, start.Month);
                if (start.Month == end.Month) {
                    day = start.Day == end.Day ? start.Day : random.Next (end.Day, start.Day);
                } else {
                    day = month == start.Month ? random.Next (start.Day, 30) : random.Next (1, 30);
                }
                var date = new DateTime (year, month, day, random.Next (0, 24), random.Next (0, 60), random.Next (0, 60));
                dates.Add (date);
            }
            dates.Sort ((a, b) => b.CompareTo (a));
            return dates;
        }

        protected DateTime MakeTime (int hour, int minute, int second = 0)
        {
            return Time.UtcNow.Date
                   .AddHours (hour)
                   .AddMinutes (minute)
                   .AddSeconds (second);
        }
    }
}

