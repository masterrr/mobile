using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Data
{
    public static class DataExtensions
    {
        /// <summary>
        /// Checks if the two objects share the same type and primary key.
        /// </summary>
        /// <param name="data">Data object.</param>
        /// <param name="other">Other data object.</param>
        public static bool Matches (this CommonData data, object other)
        {
            if (data == other) {
                return true;
            }
            if (data == null || other == null) {
                return false;
            }
            if (data.GetType () != other.GetType ()) {
                return false;
            }
            return data.Id == ((CommonData)other).Id;
        }

        public static bool UpdateData<T> (this IList<T> list, T data)
        where T : CommonData
        {
            var updateCount = 0;

            for (var idx = 0; idx < list.Count; idx++) {
                if (data.Matches (list [idx])) {
                    list [idx] = data;
                    updateCount++;
                }
            }

            return updateCount > 0;
        }

        public static string ToIdString (this CommonData data)
        {
            var id = data.RemoteId.HasValue ? data.RemoteId.ToString () : data.Id.ToString ();
            return String.Concat (data.GetType ().Name, "#", id);
        }

        public static async Task<CommonData> PutDataAsync (this IDataStore ds, CommonData data)
        {
            var type = data.GetType ();

            if (type == typeof (ClientData)) {
                return await ds.PutAsync ((ClientData)data).ConfigureAwait (false);
            } else if (type == typeof (ProjectData)) {
                return await ds.PutAsync ((ProjectData)data).ConfigureAwait (false);
            } else if (type == typeof (ProjectUserData)) {
                return await ds.PutAsync ((ProjectUserData)data).ConfigureAwait (false);
            } else if (type == typeof (TagData)) {
                return await ds.PutAsync ((TagData)data).ConfigureAwait (false);
            } else if (type == typeof (TaskData)) {
                return await ds.PutAsync ((TaskData)data).ConfigureAwait (false);
            } else if (type == typeof (TimeEntryData)) {
                return await ds.PutAsync ((TimeEntryData)data).ConfigureAwait (false);
            } else if (type == typeof (UserData)) {
                return await ds.PutAsync ((UserData)data).ConfigureAwait (false);
            } else if (type == typeof (WorkspaceData)) {
                return await ds.PutAsync ((WorkspaceData)data).ConfigureAwait (false);
            } else if (type == typeof (WorkspaceUserData)) {
                return await ds.PutAsync ((WorkspaceUserData)data).ConfigureAwait (false);
            }
            throw new InvalidOperationException (String.Format ("Unknown type of {0}", type));
        }

        public static async Task<bool> ExistWithNameAsync ( this IDataQuery<ClientData> query, string name)
        {
            var rows = await query.QueryAsync (r => r.Name == name).ConfigureAwait (false);
            return rows.Count != 0;
        }

        public static async Task<bool> ExistWithNameAsync ( this IDataQuery<ProjectData> query, string projectName, Guid clientId)
        {
            List<ProjectData> existingProjects;
            if ( clientId != Guid.Empty) {
                existingProjects = await query.QueryAsync (r => r.Name == projectName && r.ClientId == clientId).ConfigureAwait (false);
            } else {
                existingProjects = await query.QueryAsync (r => r.Name == projectName && r.ClientId == null).ConfigureAwait (false);
            }
            return existingProjects.Count != 0;
        }

        public static bool IsGroupableWith (this TimeEntryData data, TimeEntryData other)
        {
            return data.ProjectId == other.ProjectId &&
                   string.Equals (data.Description, other.Description, StringComparison.Ordinal) &&
                   data.TaskId == other.TaskId &&
                   data.UserId == other.UserId &&
                   data.WorkspaceId == other.WorkspaceId &&
                   data.RemoteId != other.RemoteId;
        }
    }
}
