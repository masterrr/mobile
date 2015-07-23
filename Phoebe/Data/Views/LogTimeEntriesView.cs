using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Utils;

namespace Toggl.Phoebe.Data.Views
{
    /// <summary>
    /// </summary>
    public class LogTimeEntriesView : TimeEntriesCollectionView
    {
        private readonly List<DateGroup> dateGroups = new List<DateGroup> ();

        public LogTimeEntriesView ()
        {
            Tag = "LogTimeEntriesView";
        }

        protected async override Task AddOrUpdateEntryAsync (TimeEntryData entry)
        {
            int groupIndex;
            int newIndex;
            NotifyCollectionChangedAction groupAction;

            TimeEntryData existingEntry;
            DateGroup grp;
            bool isNewGroup;

            if (FindExistingEntry (entry, out grp, out existingEntry)) {
                if (entry.StartTime != existingEntry.StartTime) {
                    var date = entry.StartTime.ToLocalTime ().Date;
                    var oldIndex = GetTimeEntryIndex (existingEntry);

                    // Move TimeEntry to another DateGroup
                    if (grp.Date != date) {

                        // Remove entry from previous DateGroup: //TODO: remove dateGroup too?
                        grp.Remove (existingEntry);
                        groupIndex = GetDateGroupIndex (grp);
                        await UpdateCollectionAsync (grp, NotifyCollectionChangedAction.Replace, groupIndex);

                        // Move entry to new DateGroup
                        grp = GetGroupFor (entry, out isNewGroup);
                        grp.Add (entry);
                        Sort ();

                        newIndex = GetTimeEntryIndex (entry);
                        await UpdateCollectionAsync (entry, NotifyCollectionChangedAction.Move, newIndex, oldIndex);

                        // Update new container DateGroup
                        groupIndex = GetDateGroupIndex (grp);
                        groupAction = isNewGroup ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Replace;
                        await UpdateCollectionAsync (grp, groupAction, groupIndex);

                        return;
                    }

                    // Move TimeEntry inside DateGroup
                    grp.TimeEntryList.UpdateData (entry);
                    Sort ();

                    // Update group
                    groupIndex = GetDateGroupIndex (grp);
                    await UpdateCollectionAsync (grp, NotifyCollectionChangedAction.Replace, groupIndex);

                    newIndex = GetTimeEntryIndex (entry);
                    if (newIndex != oldIndex) {
                        // Move if index is differente.
                        await UpdateCollectionAsync (entry, NotifyCollectionChangedAction.Move, newIndex, oldIndex);
                    }

                    // Update in any condition
                    await UpdateCollectionAsync (entry, NotifyCollectionChangedAction.Replace, newIndex);

                } else {
                    // Update TimeEntry only
                    grp.TimeEntryList.UpdateData (entry);

                    // Update entry
                    newIndex = GetTimeEntryIndex (entry);
                    await UpdateCollectionAsync (entry, NotifyCollectionChangedAction.Replace, newIndex);
                }
            } else {

                // Add new TimeEntry
                grp = GetGroupFor (entry, out isNewGroup);
                grp.Add (entry);
                Sort ();

                // Update group
                groupIndex = GetDateGroupIndex (grp);
                groupAction = isNewGroup ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Replace;
                await UpdateCollectionAsync (grp, groupAction, groupIndex);

                // Add new TimeEntry
                newIndex = GetTimeEntryIndex (entry);
                await UpdateCollectionAsync (entry, NotifyCollectionChangedAction.Add, newIndex);
            }
        }

        protected async override Task RemoveEntryAsync (TimeEntryData entry)
        {
            int groupIndex;
            int entryIndex;
            NotifyCollectionChangedAction groupAction = NotifyCollectionChangedAction.Replace;
            DateGroup grp;
            TimeEntryData oldEntry;

            if (FindExistingEntry (entry, out grp, out oldEntry)) {
                entryIndex = GetTimeEntryIndex (oldEntry);
                groupIndex = GetDateGroupIndex (grp);
                grp.Remove (oldEntry);
                if (grp.TimeEntryList.Count == 0) {
                    dateGroups.Remove (grp);
                    groupAction = NotifyCollectionChangedAction.Remove;
                }

                // The order affects how the collection is updated.
                await UpdateCollectionAsync (entry, NotifyCollectionChangedAction.Remove, entryIndex);
                await UpdateCollectionAsync (grp, groupAction, groupIndex);
            }
        }

        protected override IList<IDateGroup> DateGroups
        {
            get { return dateGroups.ToList<IDateGroup> (); }
        }

        #region Undo
        protected async override Task AddTimeEntryHolderAsync (TimeEntryHolder holder)
        {
            await AddOrUpdateEntryAsync (holder.TimeEntryData);
        }

        protected async override Task RemoveTimeEntryHolderAsync (TimeEntryHolder holder)
        {
            await RemoveEntryAsync (holder.TimeEntryData);
        }
        #endregion

        #region Utils
        private bool FindExistingEntry (TimeEntryData dataObject, out DateGroup dateGroup, out TimeEntryData existingDataObject)
        {
            foreach (var grp in dateGroups) {
                foreach (var obj in grp.TimeEntryList) {
                    if (dataObject.Matches (obj)) {
                        dateGroup = grp;
                        existingDataObject = obj;
                        return true;
                    }
                }
            }

            dateGroup = null;
            existingDataObject = null;
            return false;
        }

        private int GetTimeEntryIndex (TimeEntryData dataObject)
        {
            int count = 0;
            foreach (var grp in dateGroups) {
                count++;
                // Iterate by entry list.
                foreach (var obj in grp.DataObjects) {
                    if (dataObject.Matches (obj)) {
                        return count;
                    }
                    count++;
                }
            }
            return -1;
        }

        private int GetDateGroupIndex (DateGroup dateGroup)
        {
            var count = 0;
            foreach (var grp in dateGroups) {
                if (grp.Date == dateGroup.Date) {
                    return count;
                }
                count += grp.TimeEntryList.Count + 1;
            }
            return -1;
        }

        private DateGroup GetGroupFor (TimeEntryData dataObject, out bool isNewGroup)
        {
            isNewGroup = false;
            var date = dataObject.StartTime.ToLocalTime ().Date;
            var grp = dateGroups.FirstOrDefault (g => g.Date == date);
            if (grp == null) {
                grp = new DateGroup (date);
                dateGroups.Add (grp);
                isNewGroup = true;
            }
            return grp;
        }

        private void Sort ()
        {
            foreach (var grp in dateGroups) {
                grp.Sort ();
            }
            dateGroups.Sort ((a, b) => b.Date.CompareTo (a.Date));
        }
        #endregion

        public class DateGroup : IDateGroup
        {
            private readonly DateTime date;
            private readonly List<TimeEntryData> dataObjects = new List<TimeEntryData> ();

            public DateGroup (DateTime date)
            {
                this.date = date.Date;
            }

            public void Dispose ()
            {
                dataObjects.Clear ();
            }

            public DateTime Date
            {
                get { return date; }
            }

            public bool IsRunning
            {
                get {
                    return dataObjects.Any (g => g.State == TimeEntryState.Running);
                }
            }

            public TimeSpan TotalDuration
            {
                get {
                    TimeSpan totalDuration = TimeSpan.Zero;
                    foreach (var item in dataObjects) {
                        totalDuration += Toggl.Phoebe.Data.Models.TimeEntryModel.GetDuration (item, Time.UtcNow);
                    }
                    return totalDuration;
                }
            }

            public IEnumerable<object> DataObjects
            {
                get {
                    return dataObjects;
                }
            }

            public List<TimeEntryData> TimeEntryList
            {
                get {
                    return dataObjects;
                }
            }

            public event EventHandler Updated;

            private void OnUpdated ()
            {
                var handler = Updated;
                if (handler != null) {
                    handler (this, EventArgs.Empty);
                }
            }

            public void Add (TimeEntryData dataObject)
            {
                dataObjects.Add (dataObject);
                OnUpdated ();
            }

            public void Remove (TimeEntryData dataObject)
            {
                dataObjects.Remove (dataObject);
                OnUpdated ();
            }

            public void Sort ()
            {
                dataObjects.Sort ((a, b) => b.StartTime.CompareTo (a.StartTime));
                OnUpdated ();
            }
        }
    }
}
