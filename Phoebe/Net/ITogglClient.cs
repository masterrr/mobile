using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.Json;
using System.Threading;

namespace Toggl.Phoebe.Net
{
    public interface ITogglClient
    {
        #region Generic CURD methods

        Task<T> Create<T> (T jsonObject)
        where T : CommonJson;

        Task<T> Get<T> (long id)
        where T : CommonJson;

        Task<List<T>> List<T> ()
        where T : CommonJson;

        Task<T> Update<T> (T jsonObject)
        where T : CommonJson;

        Task Delete<T> (T jsonObject)
        where T : CommonJson;

        Task Delete<T> (IEnumerable<T> jsonObjects)
        where T : CommonJson;

        #endregion

        Task<UserJson> GetUser (string username, string password);

        Task<UserJson> GetUser (string googleAccessToken);

        Task<List<ClientJson>> ListWorkspaceClients (long workspaceId);

        Task<List<ProjectJson>> ListWorkspaceProjects (long workspaceId);

        Task<List<WorkspaceUserJson>> ListWorkspaceUsers (long workspaceId);

        Task<List<TaskJson>> ListWorkspaceTasks (long workspaceId);

        Task<List<TaskJson>> ListProjectTasks (long projectId);

        Task<List<ProjectUserJson>> ListProjectUsers (long projectId);

        Task<List<TimeEntryJson>> ListTimeEntries (DateTime start, DateTime end, CancellationToken cancellationToken);

        Task<List<TimeEntryJson>> ListTimeEntries (DateTime start, DateTime end);

        Task<List<TimeEntryJson>> ListTimeEntries (DateTime end, int days, CancellationToken cancellationToken);

        Task<List<TimeEntryJson>> ListTimeEntries (DateTime end, int days);

        Task<UserRelatedJson> GetChanges (DateTime? since);

        Task CreateFeedback (FeedbackJson jsonObject);

    }
}
