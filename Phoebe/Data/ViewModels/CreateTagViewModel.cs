using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class CreateTagViewModel : IVModel<ITimeEntryModel>
    {
        private ITimeEntryModel model;
        private Guid workspaceId;
        private WorkspaceModel workspace;
        private readonly IList<TimeEntryData> timeEntryList;

        public CreateTagViewModel (Guid workspaceId, IList<TimeEntryData> timeEntryList)
        {
            this.workspaceId = workspaceId;
            this.timeEntryList = timeEntryList;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "New Tag Screen";
        }

        public void Dispose ()
        {
            workspace = null;
            model = null;
        }

        public bool IsLoading { get; set; }

        public string TagName { get; set; }

        public async Task Init ()
        {
            IsLoading = true;

            // Create workspace.
            workspace = new WorkspaceModel (workspaceId);

            // Create time entry
            if (timeEntryList.Count > 1) {
                model = new TimeEntryGroup (timeEntryList);
            } else if (timeEntryList.Count == 1) {
                model = new TimeEntryModel (timeEntryList [0]);
            }

            // Load models
            await Task.WhenAll (workspace.LoadAsync (), model.LoadAsync ());

            IsLoading = false;
        }

        public async Task AssignTag (string tagName)
        {
            if (model.Workspace == null) {
                return;
            }

            var store = ServiceContainer.Resolve<IDataStore>();
            var existing = await store.Table<TagData>()
                           .QueryAsync (r => r.WorkspaceId == workspace.Id && r.Name == tagName)
                           .ConfigureAwait (false);

            var checkRelation = true;
            TagModel tag;
            if (existing.Count > 0) {
                tag = new TagModel (existing [0]);
            } else {
                tag = new TagModel {
                    Name = tagName,
                    Workspace = workspace,
                };
                await tag.SaveAsync ().ConfigureAwait (false);

                checkRelation = false;
            }

            if (model != null) {
                var assignTag = true;

                if (checkRelation) {
                    // Check if the relation already exists before adding it
                    var relations = await store.Table<TimeEntryTagData> ()
                                    .CountAsync (r => r.TimeEntryId == model.Id && r.TagId == tag.Id && r.DeletedAt == null)
                                    .ConfigureAwait (false);
                    if (relations < 1) {
                        assignTag = false;
                    }
                }

                if (assignTag) {
                    foreach (var timeEntryData in timeEntryList) {
                        var relationModel = new TimeEntryTagModel {
                            TimeEntry = new TimeEntryModel (timeEntryData),
                            Tag = tag,
                        };
                        await relationModel.SaveAsync ().ConfigureAwait (false);
                    }

                    model.Touch ();
                    await model.SaveAsync ().ConfigureAwait (false);
                }
            }
        }
    }
}

