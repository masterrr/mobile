using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Net;
using Toggl.Ross.Data;
using Toggl.Ross.DataSources;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using UIKit;
using XPlatUtils;

namespace Toggl.Ross.ViewControllers
{
    public class LogViewController : SyncStatusViewController
    {
        private NavigationMenuController navMenuController;

        public LogViewController () : base (new ContentController ())
        {
            navMenuController = new NavigationMenuController ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Log";
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            navMenuController.Attach (this);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                if (navMenuController != null) {
                    navMenuController.Detach ();
                    navMenuController = null;
                }
            }

            base.Dispose (disposing);
        }

        private class ContentController : BaseTimerTableViewController
        {
            private UIView emptyView;
            private TableViewRefreshView headerView; 

            private Subscription<SettingChangedMessage> subscriptionSettingChanged;


            public ContentController () : base (UITableViewStyle.Plain)
            {
            }

            private void EnsureAdapter(bool forceRebind = false) 
            {
                if (TableView.Source == null || forceRebind) {
                    var isGrouped = ServiceContainer.Resolve<ISettingsStore> ().GroupedTimeEntries;
                    var collectionView = isGrouped ? (TimeEntriesCollectionView)new GroupedTimeEntriesView () : new LogTimeEntriesView ();
                    var source = new Source (this, collectionView);
                    if (emptyView != null) {
                        source.EmptyView = emptyView;
                    }
                    if (headerView != null) {
                        source.HeaderView = headerView;
                    }
                    source.Attach ();
                }
            }


            public override void ViewDidLoad ()
            {
                base.ViewDidLoad ();

                EdgesForExtendedLayout = UIRectEdge.None;

                emptyView = new SimpleEmptyView () {
                    Title = "LogEmptyTitle".Tr (),
                    Message = "LogEmptyMessage".Tr (),
                };

                headerView = new TableViewRefreshView ();

                EnsureAdapter ();

                RefreshControl = headerView;
                headerView.AdaptToTableView (TableView);

                var bus = ServiceContainer.Resolve<MessageBus> ();
                subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
            }

            private void OnSettingChanged (SettingChangedMessage msg)
            {
                // Protect against Java side being GCed
                if (Handle == IntPtr.Zero) {
                    return;
                }

                if (msg.Name == SettingsStore.PropertyGroupedTimeEntries) {
                    EnsureAdapter(forceRebind: true);
                }
            }

            public override void ViewDidLayoutSubviews ()
            {
                base.ViewDidLayoutSubviews ();

                emptyView.Frame = new CGRect (25f, (View.Frame.Size.Height - 200f) / 2, View.Frame.Size.Width - 50f, 200f);
            }

            protected override void Dispose (bool disposing)
            {
                base.Dispose (disposing);
                if (disposing) {
                    var bus = ServiceContainer.Resolve<MessageBus> ();
                    if (subscriptionSettingChanged != null) {
                        bus.Unsubscribe (subscriptionSettingChanged);
                        subscriptionSettingChanged = null;
                    }
                }
            }
        }

        class Source : CollectionDataViewSource<object, IDateGroup>, IDisposable
        {
            readonly static NSString EntryCellId = new NSString ("EntryCellId");
            readonly static NSString SectionHeaderId = new NSString ("SectionHeaderId");
            readonly ContentController controller;
            readonly TimeEntriesCollectionView dataView;
            private Subscription<SyncFinishedMessage> subscriptionSyncFinished;
            public UIRefreshControl HeaderView { get; set; }

            public Source (ContentController controller, TimeEntriesCollectionView dataView) : base (controller.TableView, dataView)
            {
                this.controller = controller;
                this.dataView = dataView;

                NSTimer.CreateRepeatingScheduledTimer(5.0f, delegate {
                    var syncManager = ServiceContainer.Resolve<ISyncManager> ();
                    syncManager.Run(SyncMode.Pull);
                });

                controller.TableView.RegisterClassForCellReuse (typeof (TimeEntryCell), EntryCellId);
                controller.TableView.RegisterClassForHeaderFooterViewReuse (typeof (SectionHeaderView), SectionHeaderId);
            }

            public override void Attach ()
            {
                base.Attach ();

                var bus = ServiceContainer.Resolve<MessageBus> ();
                subscriptionSyncFinished = bus.Subscribe<SyncFinishedMessage> (OnSyncFinished);

                if (HeaderView != null) {
                    HeaderView.ValueChanged += (sender, e) => ServiceContainer.Resolve<ISyncManager> ().Run();
                    dataView.CollectionChanged += (sender, e) => HeaderView.EndRefreshing ();
                }
            }



            private NSIndexPath StartingIndexPath(int globalIndex) {
                int r = -1, s = -1, g = -1;

                foreach (var obj in dataView.Data) {
                    if (g++ == globalIndex) {
                        return NSIndexPath.FromRowSection (r, s);
                    }
                    if (obj is IDateGroup) {
                        s++;
                        r = -1;
                    } else {
                        r++;
                    }
                }
                return null;
            }

            static bool initialLoad = true;

            public override void OnCollectionChange (object sender, NotifyCollectionChangedEventArgs e)
            {
                TableView.BeginUpdates ();
                if (e.Action == NotifyCollectionChangedAction.Add) {
                    var d = dataView.Data.ToList ();
                    var modified = d.Skip (e.NewStartingIndex).Take (e.NewItems.Count).ToList ();

                    var sectionSlice = modified.OfType<IDateGroup> ().ToList ();
                    if (sectionSlice.Count > 0) {
                        var sectionIndx = d.OfType<IDateGroup> ().ToList ().IndexOf (sectionSlice.First ());
                        var h = sectionIndx + sectionSlice.Count ();

                        NSMutableIndexSet set = new NSMutableIndexSet ();

                        for (int i = sectionIndx; i < h; i++) {
                            set.Add ((nuint)i);
                        }
                        
                        TableView.InsertSections (set, UITableViewRowAnimation.Top);
                    } else if (modified.OfType<TimeEntryHolder> ().Count () == modified.Count) {
                        var indexPath = StartingIndexPath (e.NewStartingIndex);
                        var row = indexPath.Row;
                        var section = indexPath.Section;

                        var newItemsCount = e.NewItems.Count;

                        var paths = new NSIndexPath[newItemsCount];

                        for (int i = 0; i < newItemsCount; i++) {
                            paths [i] = NSIndexPath.FromRowSection (row + i, section);
                        }

                        TableView.InsertRows (paths, UITableViewRowAnimation.Left);
  
                    } else {
                        TableView.ReloadData ();
                    }
                
                } else if (e.Action == NotifyCollectionChangedAction.Remove) {
                    TableView.DeleteRows (new NSIndexPath[] { StartingIndexPath (e.OldStartingIndex) }, UITableViewRowAnimation.Automatic);
                } else if (e.Action == NotifyCollectionChangedAction.Replace) {
                    TableView.ReloadRows (new NSIndexPath[] { StartingIndexPath (e.OldStartingIndex) }, UITableViewRowAnimation.Automatic);

                }

//                    var index = e.NewStartingIndex;
//
//                    var actualIndexPath = StartingIndexPath (index);
//                    var row = actualIndexPath.Row;
//                    var section = actualIndexPath.Section;
//
//                    var newItemsCount = e.NewItems.Count;
//                    int sectionDelta = 0;
//
//                    IDateGroup trackingGroup = null;
//
//                    List<NSIndexPath> paths = new List<NSIndexPath> ();
//                    NSMutableIndexSet set = new NSMutableIndexSet ();
//                    for (int i = 0; i < newItemsCount-1; i++) {
//                        var data = dataView.Data.ElementAt (index + i);
//                        if (data is IDateGroup) {
//                            set.Add((nuint)(section+sectionDelta));
//                            sectionDelta++;
//                        }
//                        paths.Add(NSIndexPath.FromRowSection(row+i, section+sectionDelta));
//                    }
//                    TableView.InsertSections (set, UITableViewRowAnimation.Left);
//                    TableView.InsertRows (new NSIndexPath[] { StartingIndexPath (e.NewStartingIndex) }, UITableViewRowAnimation.Left);
//                }

                TableView.EndUpdates ();

            }

            private void OnSyncFinished (SyncFinishedMessage msg)
            {
                HeaderView.EndRefreshing ();
            }
                
            public override nfloat EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
            {
                return 60f;
            }

            public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return EstimatedHeight (tableView, indexPath);
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = (TimeEntryCell)tableView.DequeueReusableCell (EntryCellId, indexPath);
                cell.ContinueCallback = OnContinue;

                var row = GetRow (indexPath);

                var data = row as TimeEntryHolder;

                cell.Bind ((TimeEntryModel)data.TimeEntryData);

                return cell;
            }
                
            protected object GetRow(NSIndexPath indexPath) {
                return GetRows(GetSection(indexPath)).ElementAt(indexPath.Row);
            }

            protected IDateGroup GetSection(nint section) {
                return GetSections ().ElementAt ((int)section);
            }

            protected IDateGroup GetSection(NSIndexPath indexPath) {
                return GetSections ().ElementAt (indexPath.Section);
            }

            protected override IEnumerable<IDateGroup> GetSections ()
            {
                return dataView.Data.OfType<IDateGroup> ();
            }

            protected override IEnumerable<object> GetRows (IDateGroup section)
            {
                return dataView.Data.SkipWhile(s => s != section).Skip(1).TakeWhile(r => r is TimeEntryHolder);
            }

            public override nfloat EstimatedHeightForHeader (UITableView tableView, nint section)
            {
                return 42f;
            }

            public override UIView GetViewForHeader (UITableView tableView, nint section)
            {
                var view = (SectionHeaderView)tableView.DequeueReusableHeaderFooterView (SectionHeaderId);
                view.Bind (GetSection (section));
                return view;
            }
                
            public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
            {
                return false;
            }
                

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
//                var data = GetRow (indexPath);
//                if (data != null) {
//                    controller.NavigationController.PushViewController (
//                        new EditTimeEntryViewController ((TimeEntryModel)data), true);
//                } else {
//                    tableView.DeselectRow (indexPath, true);
//                }
            }

            private void OnContinue (TimeEntryModel model)
            {
                DurationOnlyNoticeAlertView.TryShow ();
                controller.TableView.ScrollRectToVisible (new CGRect (0, 0, 1, 1), true);
            }

            protected override void Update ()
            {
                CATransaction.Begin ();
                CATransaction.CompletionBlock = delegate {
                    TableView.ReloadData ();
                };
                base.Update ();
                CATransaction.Commit();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing) {
                    if (subscriptionSyncFinished != null) {
                        var bus = ServiceContainer.Resolve<MessageBus> ();
                        bus.Unsubscribe (subscriptionSyncFinished);
                        subscriptionSyncFinished = null;
                    }
                }
                base.Dispose (disposing);
            }
        }

        class TimeEntryCell : SwipableTimeEntryTableViewCell
        {
            private const float HorizPadding = 15f;
            private readonly UIView textContentView;
            private readonly UILabel projectLabel;
            private readonly UILabel clientLabel;
            private readonly UILabel taskLabel;
            private readonly UILabel descriptionLabel;
            private readonly UIImageView taskSeparatorImageView;
            private readonly UIImageView billableTagsImageView;
            private readonly UILabel durationLabel;
            private readonly UIImageView runningImageView;
            private TimeEntryTagsView tagsView;
            private nint rebindCounter;

            public TimeEntryCell (IntPtr ptr) : base (ptr)
            {
                textContentView = new UIView ();
                projectLabel = new UILabel ().Apply (Style.Log.CellProjectLabel);
                clientLabel = new UILabel ().Apply (Style.Log.CellClientLabel);
                taskLabel = new UILabel ().Apply (Style.Log.CellTaskLabel);
                descriptionLabel = new UILabel ().Apply (Style.Log.CellDescriptionLabel);
                taskSeparatorImageView = new UIImageView ().Apply (Style.Log.CellTaskDescriptionSeparator);
                billableTagsImageView = new UIImageView ();
                durationLabel = new UILabel ().Apply (Style.Log.CellDurationLabel);
                runningImageView = new UIImageView ().Apply (Style.Log.CellRunningIndicator);

                textContentView.AddSubviews (
                    projectLabel, clientLabel,
                    taskLabel, descriptionLabel,
                    taskSeparatorImageView
                );

                var maskLayer = new CAGradientLayer () {
                    AnchorPoint = CGPoint.Empty,
                    StartPoint = new CGPoint (0.0f, 0.0f),
                    EndPoint = new CGPoint (1.0f, 0.0f),
                    Colors = new [] {
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 0).CGColor,
                    },
                    Locations = new [] {
                        NSNumber.FromFloat (0f),
                        NSNumber.FromFloat (0.9f),
                        NSNumber.FromFloat (1f),
                    },
                };
                textContentView.Layer.Mask = maskLayer;

                ActualContentView.AddSubviews (
                    textContentView,
                    billableTagsImageView,
                    durationLabel,
                    runningImageView
                );
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing) {
                    if (tagsView != null) {
                        tagsView.Updated -= OnTagsUpdated;
                        tagsView = null;
                    }
                }

                base.Dispose (disposing);
            }

            protected override void OnDataSourceChanged ()
            {
                if (tagsView != null && (DataSource == null || DataSource.Id == tagsView.TimeEntryId)) {
                    tagsView.Updated -= OnTagsUpdated;
                    tagsView = null;
                }

                if (DataSource != null) {
                    tagsView = new TimeEntryTagsView (DataSource.Id);
                    tagsView.Updated += OnTagsUpdated;
                }

                base.OnDataSourceChanged ();
            }

            private void OnTagsUpdated (object sender, EventArgs args)
            {
                RebindTags ();
            }

            protected override async void OnContinue ()
            {
                if (DataSource == null) {
                    return;
                }
                await DataSource.ContinueAsync ();
                if (ContinueCallback != null) {
                    ContinueCallback (DataSource);
                }

                // Ping analytics
                ServiceContainer.Resolve<ITracker>().SendTimerStartEvent (TimerStartSource.AppContinue);
            }

            protected override async void OnDelete ()
            {
                if (DataSource == null) {
                    return;
                }
                await DataSource.DeleteAsync ();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                var contentFrame = ContentView.Frame;

                const float durationLabelWidth = 80f;
                durationLabel.Frame = new CGRect (
                    x: contentFrame.Width - durationLabelWidth - HorizPadding,
                    y: 0,
                    width: durationLabelWidth,
                    height: contentFrame.Height
                );

                const float billableTagsHeight = 20f;
                const float billableTagsWidth = 20f;
                billableTagsImageView.Frame = new CGRect (
                    y: (contentFrame.Height - billableTagsHeight) / 2,
                    height: billableTagsHeight,
                    x: durationLabel.Frame.X - billableTagsWidth,
                    width: billableTagsWidth
                );

                var runningHeight = runningImageView.Image.Size.Height;
                var runningWidth = runningImageView.Image.Size.Width;
                runningImageView.Frame = new CGRect (
                    y: (contentFrame.Height - runningHeight) / 2,
                    height: runningHeight,
                    x: contentFrame.Width - (HorizPadding + runningWidth) / 2,
                    width: runningWidth
                );

                textContentView.Frame = new CGRect (
                    x: 0, y: 0,
                    width: billableTagsImageView.Frame.X - 2f,
                    height: contentFrame.Height
                );
                textContentView.Layer.Mask.Bounds = textContentView.Frame;

                var bounds = GetBoundingRect (projectLabel);
                projectLabel.Frame = new CGRect (
                    x: HorizPadding,
                    y: contentFrame.Height / 2 - bounds.Height,
                    width: bounds.Width,
                    height: bounds.Height
                );

                const float clientLeftMargin = 7.5f;
                bounds = GetBoundingRect (clientLabel);
                clientLabel.Frame = new CGRect (
                    x: projectLabel.Frame.X + projectLabel.Frame.Width + clientLeftMargin,
                    y: (float)Math.Floor (projectLabel.Frame.Y + projectLabel.Font.Ascender - clientLabel.Font.Ascender),
                    width: bounds.Width,
                    height: bounds.Height
                );

                const float secondLineTopMargin = 3f;
                nfloat offsetX = HorizPadding + 1f;
                if (!taskLabel.Hidden) {
                    bounds = GetBoundingRect (taskLabel);
                    taskLabel.Frame = new CGRect (
                        x: offsetX,
                        y: contentFrame.Height / 2 + secondLineTopMargin,
                        width: bounds.Width,
                        height: bounds.Height
                    );
                    offsetX += taskLabel.Frame.Width + 4f;

                    if (!taskSeparatorImageView.Hidden) {
                        const float separatorOffsetY = -2f;
                        var imageSize = taskSeparatorImageView.Image != null ? taskSeparatorImageView.Image.Size : CGSize.Empty;
                        taskSeparatorImageView.Frame = new CGRect (
                            x: offsetX,
                            y: taskLabel.Frame.Y + taskLabel.Font.Ascender - imageSize.Height + separatorOffsetY,
                            width: imageSize.Width,
                            height: imageSize.Height
                        );

                        offsetX += taskSeparatorImageView.Frame.Width + 4f;
                    }

                    if (!descriptionLabel.Hidden) {
                        bounds = GetBoundingRect (descriptionLabel);
                        descriptionLabel.Frame = new CGRect (
                            x: offsetX,
                            y: (float)Math.Floor (taskLabel.Frame.Y + taskLabel.Font.Ascender - descriptionLabel.Font.Ascender),
                            width: bounds.Width,
                            height: bounds.Height
                        );

                        offsetX += descriptionLabel.Frame.Width + 4f;
                    }
                } else if (!descriptionLabel.Hidden) {
                    bounds = GetBoundingRect (descriptionLabel);
                    descriptionLabel.Frame = new CGRect (
                        x: offsetX,
                        y: contentFrame.Height / 2 + secondLineTopMargin,
                        width: bounds.Width,
                        height: bounds.Height
                    );
                }
            }

            private static CGRect GetBoundingRect (UILabel view)
            {
                var attrs = new UIStringAttributes () {
                    Font = view.Font,
                };
                var rect = ((NSString) (view.Text ?? String.Empty)).GetBoundingRect (
                               new CGSize (Single.MaxValue, Single.MaxValue),
                               NSStringDrawingOptions.UsesLineFragmentOrigin,
                               attrs, null);
                rect.Height = (float)Math.Ceiling (rect.Height);
                return rect;
            }

            protected override void Rebind ()
            {
                ResetTrackedObservables ();

                if (DataSource == null) {
                    return;
                }

                rebindCounter++;

                var model = DataSource;
                var projectName = "LogCellNoProject".Tr ();
                var projectColor = Color.Gray;
                var clientName = String.Empty;

                if (model.Project != null) {
                    projectName = model.Project.Name;
                    projectColor = UIColor.Clear.FromHex (model.Project.GetHexColor ());

                    if (model.Project.Client != null) {
                        clientName = model.Project.Client.Name;
                    }
                }

                projectLabel.TextColor = projectColor;
                if (projectLabel.Text != projectName) {
                    projectLabel.Text = projectName;
                    SetNeedsLayout ();
                }
                if (clientLabel.Text != clientName) {
                    clientLabel.Text = clientName;
                    SetNeedsLayout ();
                }

                var taskName = model.Task != null ? model.Task.Name : String.Empty;
                var taskHidden = String.IsNullOrWhiteSpace (taskName);
                var description = model.Description;
                var descHidden = String.IsNullOrWhiteSpace (description);

                if (taskHidden && descHidden) {
                    description = "LogCellNoDescription".Tr ();
                    descHidden = false;
                }
                var taskDeskSepHidden = taskHidden || descHidden;

                if (taskLabel.Hidden != taskHidden || taskLabel.Text != taskName) {
                    taskLabel.Hidden = taskHidden;
                    taskLabel.Text = taskName;
                    SetNeedsLayout ();
                }
                if (descriptionLabel.Hidden != descHidden || descriptionLabel.Text != description) {
                    descriptionLabel.Hidden = descHidden;
                    descriptionLabel.Text = description;
                    SetNeedsLayout ();
                }
                if (taskSeparatorImageView.Hidden != taskDeskSepHidden) {
                    taskSeparatorImageView.Hidden = taskDeskSepHidden;
                    SetNeedsLayout ();
                }

                RebindTags ();

                var duration = model.GetDuration ();
                durationLabel.Text = TimeEntryModel.GetFormattedDuration (model.Data);

                runningImageView.Hidden = model.State != TimeEntryState.Running;

                if (model.State == TimeEntryState.Running) {
                    // Schedule rebind
                    var counter = rebindCounter;
                    DispatchQueue.MainQueue.DispatchAfter (
                        TimeSpan.FromMilliseconds (1000 - duration.Milliseconds),
                    delegate {
                        if (counter == rebindCounter) {
                            Rebind ();
                        }
                    });
                }

                LayoutIfNeeded ();
            }

            private void RebindTags ()
            {
                var model = DataSource;
                if (model == null || tagsView == null) {
                    return;
                }

                var hasTags = tagsView.HasNonDefault;
                var isBillable = model.IsBillable;
                if (hasTags && isBillable) {
                    billableTagsImageView.Apply (Style.Log.BillableAndTaggedEntry);
                } else if (hasTags) {
                    billableTagsImageView.Apply (Style.Log.TaggedEntry);
                } else if (isBillable) {
                    billableTagsImageView.Apply (Style.Log.BillableEntry);
                } else {
                    billableTagsImageView.Apply (Style.Log.PlainEntry);
                }
            }

            protected override void ResetTrackedObservables ()
            {
                Tracker.MarkAllStale ();

                if (DataSource != null) {
                    Tracker.Add (DataSource, HandleTimeEntryPropertyChanged);

                    if (DataSource.Project != null) {
                        Tracker.Add (DataSource.Project, HandleProjectPropertyChanged);

                        if (DataSource.Project.Client != null) {
                            Tracker.Add (DataSource.Project.Client, HandleClientPropertyChanged);
                        }
                    }

                    if (DataSource.Task != null) {
                        Tracker.Add (DataSource.Task, HandleTaskPropertyChanged);
                    }
                }

                Tracker.ClearStale ();
            }

            private void HandleTimeEntryPropertyChanged (string prop)
            {
                if (prop == TimeEntryModel.PropertyProject
                        || prop == TimeEntryModel.PropertyTask
                        || prop == TimeEntryModel.PropertyStartTime
                        || prop == TimeEntryModel.PropertyStopTime
                        || prop == TimeEntryModel.PropertyState
                        || prop == TimeEntryModel.PropertyIsBillable
                        || prop == TimeEntryModel.PropertyDescription) {
                    Rebind ();
                }
            }

            private void HandleProjectPropertyChanged (string prop)
            {
                if (prop == ProjectModel.PropertyClient
                        || prop == ProjectModel.PropertyName
                        || prop == ProjectModel.PropertyColor) {
                    Rebind ();
                }
            }

            private void HandleClientPropertyChanged (string prop)
            {
                if (prop == ClientModel.PropertyName) {
                    Rebind ();
                }
            }

            private void HandleTaskPropertyChanged (string prop)
            {
                if (prop == TaskModel.PropertyName) {
                    Rebind ();
                }
            }

            public Action<TimeEntryModel> ContinueCallback { get; set; }
        }

        class SectionHeaderView : UITableViewHeaderFooterView
        {
            private const float HorizSpacing = 15f;
            private readonly UILabel dateLabel;
            private readonly UILabel totalDurationLabel;

            private IDateGroup groupedData;

            private int rebindCounter;

            public SectionHeaderView (IntPtr ptr) : base (ptr)
            {
                dateLabel = new UILabel ().Apply (Style.Log.HeaderDateLabel);
                ContentView.AddSubview (dateLabel);

                totalDurationLabel = new UILabel ().Apply (Style.Log.HeaderDurationLabel);
                ContentView.AddSubview (totalDurationLabel);

                BackgroundView = new UIView ().Apply (Style.Log.HeaderBackgroundView);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                var contentFrame = ContentView.Frame;

                dateLabel.Frame = new CGRect (
                    x: HorizSpacing,
                    y: 0,
                    width: (contentFrame.Width - 3 * HorizSpacing) / 2,
                    height: contentFrame.Height
                );

                totalDurationLabel.Frame = new CGRect (
                    x: (contentFrame.Width - 3 * HorizSpacing) / 2 + 2 * HorizSpacing,
                    y: 0,
                    width: (contentFrame.Width - 3 * HorizSpacing) / 2,
                    height: contentFrame.Height
                );
            }
                

            public void Bind (IDateGroup data)
            {
                this.groupedData = data;

                Rebind ();
            }

            private void Rebind ()
            {
                RebindDuration ();
            }

            private void RebindDuration ()
            {
                rebindCounter++;
                var duration = groupedData.TotalDuration;

                dateLabel.Text = groupedData.Date.ToLocalizedDateString ();
                totalDurationLabel.Text = FormatDuration (duration);

                if (groupedData.IsRunning) {
                    var counter = rebindCounter;

                    DispatchQueue.MainQueue.DispatchAfter(TimeSpan.FromMilliseconds (60000 - duration.Seconds * 1000 - duration.Milliseconds), delegate {
                        if (counter == rebindCounter) {
                            RebindDuration ();
                        }
                    });

                }
            }

            private string FormatDuration (TimeSpan duration)
            {
                if (duration.TotalHours >= 1f) {
                    return String.Format (
                               "LogHeaderDurationHoursMinutes".Tr (),
                               (int)duration.TotalHours,
                               duration.Minutes
                           );
                }

                if (duration.Minutes > 0) {
                    return String.Format (
                               "LogHeaderDurationMinutes".Tr (),
                               duration.Minutes
                           );
                }

                return String.Empty;
            }
        }
    }
}
