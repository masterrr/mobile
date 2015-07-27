using System;
using System.Collections.Specialized;
using System.Linq;
using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
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

            private void EnsureAdapter (bool forceRebind = false)
            {
                if (TableView.Source == null || forceRebind) {
                    TableView.Hidden = true;
                    if (TableView.Source != null) {
                        (TableView.Source as Source).Dispose ();
                    }
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
                    TableView.ReloadData ();
                    TableView.Hidden = false;
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
                if (msg.Name == SettingsStore.PropertyGroupedTimeEntries) {
                    EnsureAdapter (true);
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

        class Source : CollectionDataViewSource<object, IDateGroup, TimeEntryHolder>, IDisposable
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

                NSTimer.CreateRepeatingScheduledTimer (5.0f, delegate {
                    var syncManager = ServiceContainer.Resolve<ISyncManager> ();
                    syncManager.Run (SyncMode.Pull);
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

            public override void OnCollectionChange (object sender, NotifyCollectionChangedEventArgs e)
            {
                // this line is needed to update
                // the section list every time
                // the collection is updated.
                base.OnCollectionChange (sender, e);

                TableView.BeginUpdates ();

                if (e.Action == NotifyCollectionChangedAction.Add) {
                    for (int i = 0; i < e.NewItems.Count; i++) {
                        var index = e.NewStartingIndex + i;
                        var elementToAdd = dataView.Data.ElementAt (index);

                        if (elementToAdd is IDateGroup) {
                            var indexSet = GetSectionIndexFromItemIndex (index);
                            TableView.InsertSections (indexSet, UITableViewRowAnimation.Automatic);
                        } else {
                            var indexPath = GetRowPathFromItemIndex (index);
                            TableView.InsertRows (new [] {indexPath}, UITableViewRowAnimation.Automatic);
                        }
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Remove) {
                    if (e.OldItems[0] is IDateGroup) {
                        var indexSet = GetSectionIndexFromItemIndex (e.OldStartingIndex);
                        TableView.DeleteSections (indexSet, UITableViewRowAnimation.Automatic);
                    } else {
                        var indexPath = GetRowPathFromItemIndex (e.OldStartingIndex);
                        TableView.DeleteRows (new [] {indexPath}, UITableViewRowAnimation.Automatic);
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Replace) {
                    if (dataView.Data.ElementAt (e.NewStartingIndex) is IDateGroup) {
                        var indexSet = GetSectionIndexFromItemIndex (e.OldStartingIndex);
                        TableView.ReloadSections (indexSet, UITableViewRowAnimation.Automatic);
                    } else {
                        var indexPath = GetRowPathFromItemIndex (e.NewStartingIndex);
                        TableView.ReloadRows (new [] {indexPath}, UITableViewRowAnimation.Automatic);
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Move) {
                    var oldSectionSet = GetSectionIndexFromItemIndex (e.OldStartingIndex);
                    var newSectionSet = GetSectionIndexFromItemIndex (e.NewStartingIndex);
                    var oldSectionIndx = oldSectionSet.FirstIndex;
                    var newSectionIndx = newSectionSet.FirstIndex;

                    if (oldSectionIndx == newSectionIndx) {
                        TableView.ReloadSections (oldSectionSet, UITableViewRowAnimation.Automatic);
                    } else {
                        var oldSection = dataView.Data.ElementAt ((int)oldSectionIndx) as IDateGroup;
                        if (oldSection != null && oldSection.DataObjects.Any ()) {
                            TableView.ReloadSections (oldSectionSet, UITableViewRowAnimation.Automatic);
                        } else {
                            TableView.DeleteSections (oldSectionSet, UITableViewRowAnimation.Automatic);
                        }

                        var newSection = dataView.Data.ElementAt ((int)newSectionIndx) as IDateGroup;
                        if (newSection != null && newSection.DataObjects.Any ()) {
                            TableView.ReloadSections (newSectionSet, UITableViewRowAnimation.Automatic);
                        } else {
                            TableView.InsertSections (newSectionSet, UITableViewRowAnimation.Automatic);
                        }
                    }
                }

                TableView.EndUpdates ();

                if (e.Action == NotifyCollectionChangedAction.Reset) {
                    TableView.ReloadData ();
                }
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
                var index = GetItemIndexFromRowPath (indexPath);
                cell.ContinueCallback = OnContinue;
                cell.DeleteCallback = OnDelete;

                var data = (TimeEntryHolder)dataView.Data.ElementAt (index);
                cell.Bind (data);

                return cell;
            }

            public override UIView GetViewForHeader (UITableView tableView, nint section)
            {
                var view = (SectionHeaderView)tableView.DequeueReusableHeaderFooterView (SectionHeaderId);
                view.Bind (Sections.ElementAt ((int)section));
                return view;
            }

            public override nfloat GetHeightForHeader (UITableView tableView, nint section)
            {
                return EstimatedHeightForHeader (tableView, section);
            }

            public override nfloat EstimatedHeightForHeader (UITableView tableView, nint section)
            {
                return 42f;
            }

            public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
            {
                return false;
            }


            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var index = GetItemIndexFromRowPath (indexPath);
                var data = (TimeEntryHolder)dataView.Data.ElementAt (index);
                if (data != null) {
                    controller.NavigationController.PushViewController (
                        new EditTimeEntryViewController ((TimeEntryModel)data.TimeEntryData), true);
                } else {
                    tableView.DeselectRow (indexPath, true);
                }
            }

            private int GetHolderIndex (TimeEntryHolder holder)
            {
                return dataView.Data.TakeWhile ((x) => x != holder).Count ();
            }

            private void OnContinue (TimeEntryHolder holder)
            {
                DurationOnlyNoticeAlertView.TryShow ();
                dataView.ContinueTimeEntry (GetHolderIndex (holder));
                controller.TableView.ScrollRectToVisible (new CGRect (0, 0, 1, 1), true);
            }

            private void OnDelete (TimeEntryHolder holder)
            {
                DurationOnlyNoticeAlertView.TryShow ();
                dataView.RemoveItem (GetHolderIndex (holder));
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
                    TableView.Source = null;
                }
                base.Dispose (disposing);
            }

            protected override bool CompareDataSections (object data, IDateGroup section)
            {
                var dateGroup = data as IDateGroup;
                return dateGroup.Date == section.Date;
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

            protected override void OnContinue ()
            {
                if (DataSource == null) {
                    return;
                }

                if (ContinueCallback != null) {
                    ContinueCallback (DataSource);
                }

                // Ping analytics
                ServiceContainer.Resolve<ITracker>().SendTimerStartEvent (TimerStartSource.AppContinue);
            }

            protected override void OnDelete ()
            {
                if (DataSource == null) {
                    return;
                }

                if (DeleteCallback != null) {
                    DeleteCallback (DataSource);
                }
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
                if (DataSource == null) {
                    return;
                }

                rebindCounter++;

                var model = DataSource;
                var projectName = "LogCellNoProject".Tr ();
                var projectColor = Color.Gray;
                var clientName = String.Empty;

                if (model.ProjectName != null) {
                    projectName = model.ProjectName;
                }

                var colorId = DataSource.Color % ProjectModel.HexColors.Length;
                if (colorId > -1) {
                    projectColor = UIColor.Clear.FromHex (ProjectModel.HexColors [colorId]);
                }

                if (model.ClientName != null) {
                    clientName = model.ClientName;
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

                var taskHidden = String.IsNullOrWhiteSpace (model.TaskName);
                var description = model.Description;
                var descHidden = String.IsNullOrWhiteSpace (description);

                if (taskHidden && descHidden) {
                    description = "LogCellNoDescription".Tr ();
                    descHidden = false;
                }
                var taskDeskSepHidden = taskHidden || descHidden;

                if (taskLabel.Hidden != taskHidden || taskLabel.Text != model.TaskName) {
                    taskLabel.Hidden = taskHidden;
                    taskLabel.Text = model.TaskName;
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

                var duration = model.TotalDuration;
                durationLabel.Text = TimeEntryModel.GetFormattedDuration (duration);

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

            public Action<TimeEntryHolder> ContinueCallback { get; set; }
            public Action<TimeEntryHolder> DeleteCallback { get; set; }

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

                    DispatchQueue.MainQueue.DispatchAfter (TimeSpan.FromMilliseconds (60000 - duration.Seconds * 1000 - duration.Milliseconds), delegate {
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
