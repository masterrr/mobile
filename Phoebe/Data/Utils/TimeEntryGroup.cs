using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using PropertyChanged;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using XPlatUtils;
using TTask = System.Threading.Tasks.Task;

namespace Toggl.Phoebe.Data.Utils
{
    /// <summary>
    // Wrapper to manage groups of TimeEntryData objects
    // The class presents a TimeEntryModel (the last time entry added) to work correclty with
    // the Views created but actually manage a list of TimeEntryData
    /// </summary>
    [DoNotNotify]
    public class TimeEntryGroup : ITimeEntryModel
    {
        private readonly List<TimeEntryData> dataObjects = new List<TimeEntryData> ();
        private TimeEntryModel model;

        public TimeEntryGroup (TimeEntryData data)
        {
            Add (data);
        }

        public TimeEntryGroup (IList<TimeEntryData> dataList)
        {
            Add (dataList);
        }

        public static async Task<IList<TimeEntryData>> GetTimeEntryDataList (IList<string> ids)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var list = new List<TimeEntryData> (ids.Count);

            foreach (var stringGuid in ids) {
                var guid = new Guid (stringGuid);
                var rows = await store.Table<TimeEntryData> ()
                           .QueryAsync (r => r.Id == guid && r.DeletedAt == null);
                var data = rows.FirstOrDefault ();
                list.Add (data);
            }
            return list;
        }

        public TimeEntryModel Model
        {
            get {
                if (model == null) {
                    model = (TimeEntryModel)dataObjects.Last();
                    model.PropertyChanged += (sender, e) => {
                        if (PropertyChanged != null) {
                            PropertyChanged.Invoke (sender, e);
                        }
                    };
                } else {
                    if (!model.Data.Matches (dataObjects.Last())) {
                        model.Data = dataObjects.Last();
                    }
                }
                return model;
            }
        }

        public IList<string> TimeEntryGuids
        {
            get {
                return dataObjects.AsEnumerable ().Select (r => r.Id.ToString ()).ToList ();
            }
        }

        public List<TimeEntryData> TimeEntryList
        {
            get {
                return dataObjects;
            }
        }

        public int Count
        {
            get {
                return dataObjects.Count;
            }
        }

        public TimeSpan Duration
        {
            get {
                TimeSpan duration = TimeSpan.Zero;
                foreach (var item in dataObjects) {
                    duration += TimeEntryModel.GetDuration (item, Time.UtcNow);
                }
                return duration;
            }
        }

        public DateTime LastStartTime
        {
            get {
                return dataObjects.Last().StartTime;
            }
        }

        public int DistinctDays
        {
            get {
                return dataObjects.GroupBy (e => e.StartTime.Date).Count();
            }
        }

        public void Add (IList<TimeEntryData> dataList)
        {
            dataObjects.AddRange (dataList);
        }

        public void Add (TimeEntryData data)
        {
            dataObjects.Add (data);
        }

        public void Update (TimeEntryData data)
        {
            dataObjects.UpdateData (data);
            Sort ();
        }

        public void UpdateIfPossible (TimeEntryData entry)
        {
            if (CanContain (entry)) {
                Add (entry);
            }
        }

        public void Remove (TimeEntryData entry)
        {
            if (dataObjects.Contains<TimeEntryData> (entry)) {
                dataObjects.Remove (entry);
            } else {
                dataObjects.RemoveAll (d => d.Id == entry.Id);
            }
        }

        public void Sort()
        {
            dataObjects.Sort ((a, b) => a.StartTime.CompareTo (b.StartTime));
        }

        public bool CanContain (TimeEntryData data)
        {
            return dataObjects.Last().IsGroupableWith (data);
        }

        public bool Contains (TimeEntryData entry, out TimeEntryData existingTimeEntry)
        {
            foreach (var item in dataObjects)
                if (item.Matches (entry)) {
                    existingTimeEntry = item;
                    return true;
                }

            existingTimeEntry = null;
            return false;
        }

        public void Dispose()
        {
            model = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public TimeSpan GetDuration ()
        {
            return Duration;
        }

        public void SetDuration (TimeSpan value)
        {
        }

        public Task StartAsync ()
        {
            return Model.StartAsync ();
        }

        public Task StoreAsync ()
        {
            return Model.StoreAsync ();
        }

        public Task StopAsync ()
        {
            return Model.StopAsync ();
        }

        public async Task DeleteAsync ()
        {
            var deleteTasks = new List<Task> ();
            foreach (var item in dataObjects) {
                var m = new TimeEntryModel (item);
                deleteTasks.Add (m.DeleteAsync ());
            }
            await TTask.WhenAll (deleteTasks);
            Dispose ();
        }

        public async Task SaveAsync ()
        {
            var dataStore = ServiceContainer.Resolve<IDataStore> ();
            var saveTasks = new List<Task> ();
            foreach (var item in dataObjects) {
                saveTasks.Add (dataStore.PutAsync (item));
            }
            await TTask.WhenAll (saveTasks);
        }

        public void Touch ()
        {
            for (int i = 0; i < dataObjects.Count; i++) {
                var newData = new TimeEntryData (dataObjects[i]);;
                Model<TimeEntryData>.MarkDirty (newData);
                dataObjects[i] = newData;
            }
        }

        public TimeEntryData Data
        {
            get {
                return TimeEntryList.Last ();
            }

            set {
                Model.Data = value;
            }
        }

        public Task<TimeEntryModel> ContinueAsync ()
        {
            return Model.ContinueAsync ();
        }

        public Task MapTagsFromModel (TimeEntryModel model)
        {
            return Model.MapTagsFromModel (model);
        }

        public Task MapMinorsFromModel (TimeEntryModel model)
        {
            return Model.MapMinorsFromModel (model);
        }

        public TimeEntryState State
        {
            get {
                return TimeEntryList.Last ().State;
            } set {
                Model.State = value;
            }
        }

        public DateTime StartTime
        {
            get {
                return TimeEntryList.FirstOrDefault ().StartTime;
            } set {
                if (TimeEntryList.Count == 1) {
                    Model.StartTime = value;
                } else {
                    var startModel = new TimeEntryModel (TimeEntryList.FirstOrDefault ());
                    startModel.StartTime = value;
                }
            }
        }

        public DateTime? StopTime
        {
            get {
                return Model.StopTime;
            } set {
                Model.StopTime = value;
            }
        }

        public bool IsBillable
        {
            get {
                return TimeEntryList.Last ().IsBillable;
            }

            set {
                foreach (var item in dataObjects) {
                    item.IsBillable = value;
                }
                Touch ();
            }
        }

        public UserModel User
        {
            get {
                return Model.User;
            }

            set {
                foreach (var item in dataObjects) {
                    item.UserId = value.Id;
                }
                Touch ();
            }
        }

        public WorkspaceModel Workspace
        {
            get {
                return Model.Workspace;
            } set {
                foreach (var item in dataObjects) {
                    item.WorkspaceId = value.Id;
                }
                Touch ();
            }
        }

        public string Description
        {
            get {
                return dataObjects.Last().Description;
            }

            set {
                if (string.IsNullOrEmpty (Description) && string.IsNullOrEmpty (value)) {
                    return;
                }

                if (Description != value) {
                    foreach (var item in dataObjects) {
                        item.Description = value;
                    }
                    Touch ();
                }
            }
        }

        public ProjectModel Project
        {
            get {
                return Model.Project;
            }

            set {
                foreach (var item in dataObjects) {
                    if (value != null) {
                        item.ProjectId = value.Id;
                    } else {
                        item.ProjectId = null;
                    }
                }
                Touch ();
            }
        }

        public Guid Id
        {
            get {
                return dataObjects.Last().Id;
            }
        }

        public TaskModel Task
        {
            get {
                return Model.Task;
            }

            set {
                foreach (var item in dataObjects) {
                    item.TaskId = value.Id;
                }
                Touch ();
            }
        }

        public TTask LoadAsync ()
        {
            return Model.LoadAsync ();
        }

        public Task<int> GetNumberOfTagsAsync ()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            return store.Table<TimeEntryTagData>()
                   .Where (t => t.TimeEntryId == Id)
                   .CountAsync ();
        }
    }
}

