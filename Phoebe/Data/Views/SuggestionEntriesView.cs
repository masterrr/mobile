using System;
using System.Collections.Generic;
using System.Linq;
using Gma.DataStructures.StringSearch;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Views
{
    public class SuggestionEntriesView : IDataView<TimeEntryData>
    {
        private static readonly string Tag = "SuggestionEntriesView";
        public bool IsLoading { get; private set; }
        public bool HasMore { get; private set; }

        public event EventHandler Updated;

        public readonly List<TimeEntryData> TimeEntries = new List<TimeEntryData>();
        public readonly List<TimeEntryData> FilteredEntries = new List<TimeEntryData> ();

        private string currentFilterInfix = "";

        public const int StringMaxLength = 20;

        private ITrie<TimeEntryData> trie;

        public bool HasSuggestions
        {
            get { return FilteredEntries != null && FilteredEntries.Count > 0; }
        }

        public SuggestionEntriesView ()
        {
            Reload ();
        }

        private void ReinitTrie()
        {
            trie = new SuffixTrie<TimeEntryData> (3);
        }

        public IEnumerable<TimeEntryData> Data
        {
            get { return FilteredEntries; }
        }

        public long Count
        {
            get { return FilteredEntries.Count; }
        }

        public void LoadMore ()
        {
            Reload ();
        }

        public void Reload()
        {
            Load ();
        }

        private async void Load ()
        {
            if (IsLoading) {
                return;
            }

            IsLoading = true;

            ReinitTrie();

            try {
                var store = ServiceContainer.Resolve<IDataStore> ();
                var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
                var baseQuery = store.Table<TimeEntryData> ()
                                .OrderBy (r => r.StartTime, false)
                                .Where (r => r.State != TimeEntryState.New
                                        && r.DeletedAt == null
                                        && r.UserId == userId
                                        && r.Description != null);
                var entries = await baseQuery.QueryAsync ();
                TimeEntries.AddRange (entries.ToList());
                foreach (var entry in TimeEntries) {
                    var description = entry.Description;
                    trie.Add (description.Substring (0, Math.Min (description.Length, StringMaxLength)).ToLower(), entry);
                }

            } catch (Exception exc) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Error (Tag, exc, "Failed to fetch time entries");
            } finally {
                IsLoading = false;
                FilterByInfix (currentFilterInfix);
            }
        }

        public void FilterByInfix (string suff)
        {
            var lowerSuff = suff.ToLower ();
            if (!lowerSuff.Equals (currentFilterInfix)) {
                currentFilterInfix = lowerSuff;
            }

            FilteredEntries.Clear ();
            if (currentFilterInfix.Length < 3) {
                OnUpdated ();
                return;
            }

            var result = trie.Retrieve (currentFilterInfix);
            FilteredEntries.AddRange (result);
            OnUpdated ();
        }

        private void OnUpdated ()
        {
            var handler = Updated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
    }
}

