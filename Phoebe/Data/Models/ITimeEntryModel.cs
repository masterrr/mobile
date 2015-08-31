using System;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Data.Models
{
    public interface ITimeEntryModel : IModel
    {
        TimeEntryData Data { get; set; }

        TimeEntryState State { get; set; }

        string Description { get; set; }

        DateTime StartTime { get; set; }

        DateTime? StopTime { get; set; }

        bool IsBillable { get; set; }

        UserModel User { get; set; }

        WorkspaceModel Workspace { get; set; }

        ProjectModel Project { get; set; }

        TaskModel Task { get; set; }

        TimeSpan GetDuration ();

        void SetDuration (TimeSpan value);

        Task StartAsync ();

        Task<TimeEntryModel> ContinueAsync ();

        Task StoreAsync ();

        Task StopAsync ();

        Task SaveAsync ();

        Task DeleteAsync ();

        Task MapTagsFromModel (TimeEntryModel model);

        Task MapMinorsFromModel (TimeEntryModel model);

        void Touch ();

        Task LoadAsync ();
    }
}

