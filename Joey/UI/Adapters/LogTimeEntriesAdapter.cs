using System;
using System.Collections.Specialized;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Joey.UI.Adapters
{
    public class LogTimeEntriesAdapter : RecycledDataViewAdapter<object>
    {
        public static readonly int ViewTypeLoaderPlaceholder = 0;
        public static readonly int ViewTypeContent = 1;
        public static readonly int ViewTypeDateHeader = ViewTypeContent + 1;

        private readonly Handler handler = new Handler ();
        private static readonly int ContinueThreshold = 1;
        private TimeEntriesCollectionView modelView;
        private DateTime lastTimeEntryContinuedTime;

        public LogTimeEntriesAdapter (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
        {
        }

        public LogTimeEntriesAdapter (RecyclerView owner, TimeEntriesCollectionView modelView) : base (owner, modelView)
        {
            this.modelView = modelView;
            lastTimeEntryContinuedTime = Time.UtcNow;
        }

        protected override void CollectionChanged (NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) {
                NotifyDataSetChanged();
            }

            if (e.Action == NotifyCollectionChangedAction.Add) {

                if (e.NewItems.Count == 0) {
                    return;
                }

                // First items are inserterd with a reset
                // to fix the top scroll position
                if (e.NewItems.Count == DataView.Count && e.NewStartingIndex == 0) {
                    NotifyDataSetChanged();
                    return;
                }

                if (e.NewItems.Count == 1) {

                    // If new TE is started,
                    // we should move the scroll to top position
                    Owner.SmoothScrollToPosition (0);

                    // After some investigation, this action breaks
                    // RecyclerView layout. Under investigation.
                    // For the moment, the NotifyItemRangeInserted will be
                    // replaced by the generic NotifyDataSetChanged.
                    NotifyItemInserted (e.NewStartingIndex);
                    // NotifyDataSetChanged ();
                } else {
                    NotifyItemRangeInserted (e.NewStartingIndex, e.NewItems.Count);
                    // NotifyDataSetChanged ();
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Replace) {
                NotifyItemChanged (e.NewStartingIndex);
            }

            if (e.Action == NotifyCollectionChangedAction.Remove) {
                NotifyItemRemoved (e.OldStartingIndex);
            }

            if (e.Action == NotifyCollectionChangedAction.Move) {
                NotifyItemMoved (e.OldStartingIndex, e.NewStartingIndex);
            }
        }

        private void OnContinueTimeEntry (RecyclerView.ViewHolder viewHolder)
        {
            // Don't continue a new TimeEntry before
            // x seconds has passed.
            if (Time.UtcNow < lastTimeEntryContinuedTime + TimeSpan.FromSeconds (ContinueThreshold)) {
                return;
            }
            lastTimeEntryContinuedTime = Time.UtcNow;

            modelView.ContinueTimeEntry (viewHolder.AdapterPosition);
        }

        protected override RecyclerView.ViewHolder GetViewHolder (ViewGroup parent, int viewType)
        {
            View view;
            RecyclerView.ViewHolder holder;

            if (viewType == ViewTypeDateHeader) {
                view = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.LogTimeEntryListSectionHeader, parent, false);
                holder = new HeaderListItemHolder (handler, view);
            } else {
                view = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.LogTimeEntryListItem, parent, false);
                holder = new TimeEntryListItemHolder (handler, this, view);
            }

            return holder;
        }

        protected override void BindHolder (RecyclerView.ViewHolder holder, int position)
        {
            if (GetItemViewType (position) == ViewTypeDateHeader) {
                var headerHolder = (HeaderListItemHolder)holder;
                headerHolder.Bind ((TimeEntriesCollectionView.IDateGroup) GetEntry (position));
            } else {
                var entryHolder = (TimeEntryListItemHolder)holder;
                entryHolder.Bind ((TimeEntryHolder) GetEntry (position));
            }
        }

        public override int GetItemViewType (int position)
        {
            // TODO: investigate positio > DataView.Count
            if (position >= DataView.Count) {
                return ViewTypeLoaderPlaceholder;
            }

            var obj = GetEntry (position);
            if (obj is TimeEntriesCollectionView.IDateGroup) {
                return ViewTypeDateHeader;
            }

            return ViewTypeContent;
        }

        public override void OnViewDetachedFromWindow (Java.Lang.Object holder)
        {
            if (holder is TimeEntryListItemHolder) {
                var mHolder = (TimeEntryListItemHolder)holder;
                mHolder.DataSource = null;
            } else if (holder is HeaderListItemHolder) {
                var mHolder = (HeaderListItemHolder)holder;
                mHolder.DisposeDataSource ();
            }
            base.OnViewDetachedFromWindow (holder);
        }

        [Shadow (ShadowAttribute.Mode.Top | ShadowAttribute.Mode.Bottom)]
        public class HeaderListItemHolder : RecycledBindableViewHolder<TimeEntriesCollectionView.IDateGroup>
        {
            private readonly Handler handler;

            public TextView DateGroupTitleTextView { get; private set; }

            public TextView DateGroupDurationTextView { get; private set; }

            public HeaderListItemHolder (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
            {
            }

            public HeaderListItemHolder (Handler handler, View root) : base (root)
            {
                this.handler = handler;
                DateGroupTitleTextView = root.FindViewById<TextView> (Resource.Id.DateGroupTitleTextView).SetFont (Font.RobotoMedium);
                DateGroupDurationTextView = root.FindViewById<TextView> (Resource.Id.DateGroupDurationTextView).SetFont (Font.Roboto);
            }

            protected override void Rebind ()
            {
                DateGroupTitleTextView.Text = GetRelativeDateString (DataSource.Date);
                RebindDuration ();
            }

            private void RebindDuration ()
            {
                if (DataSource == null || Handle == IntPtr.Zero) {
                    return;
                }

                var duration = DataSource.TotalDuration;
                DateGroupDurationTextView.Text = duration.ToString (@"hh\:mm\:ss");

                if (DataSource.IsRunning) {
                    handler.RemoveCallbacks (RebindDuration);
                    handler.PostDelayed (RebindDuration, 1000 - duration.Milliseconds);
                } else {
                    handler.RemoveCallbacks (RebindDuration);
                }
            }

            private static string GetRelativeDateString (DateTime dateTime)
            {
                var ctx = ServiceContainer.Resolve<Context> ();
                var ts = Time.Now.Date - dateTime.Date;
                switch (ts.Days) {
                case 0:
                    return ctx.Resources.GetString (Resource.String.Today);
                case 1:
                    return ctx.Resources.GetString (Resource.String.Yesterday);
                case -1:
                    return ctx.Resources.GetString (Resource.String.Tomorrow);
                default:
                    return dateTime.ToDeviceDateString ();
                }
            }
        }

        private class TimeEntryListItemHolder : RecyclerView.ViewHolder, View.IOnTouchListener
        {
            private readonly Handler handler;
            private readonly LogTimeEntriesAdapter owner;

            public TimeEntryHolder DataSource { get; set; }

            public View ColorView { get; private set; }

            public TextView ProjectTextView { get; private set; }

            public TextView ClientTextView { get; private set; }

            public TextView TaskTextView { get; private set; }

            public TextView DescriptionTextView { get; private set; }

            public NotificationImageView TagsView { get; private set; }

            public View BillableView { get; private set; }

            public TextView DurationTextView { get; private set; }

            public ImageButton ContinueImageButton { get; private set; }

            public TimeEntryListItemHolder (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
            {
            }

            public TimeEntryListItemHolder (Handler handler, LogTimeEntriesAdapter owner, View root) : base (root)
            {
                this.handler = handler;
                this.owner = owner;

                ColorView = root.FindViewById<View> (Resource.Id.ColorView);
                ProjectTextView = root.FindViewById<TextView> (Resource.Id.ProjectTextView).SetFont (Font.RobotoMedium);
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.RobotoMedium);
                TaskTextView = root.FindViewById<TextView> (Resource.Id.TaskTextView).SetFont (Font.RobotoMedium);
                DescriptionTextView = root.FindViewById<TextView> (Resource.Id.DescriptionTextView).SetFont (Font.Roboto);
                TagsView = root.FindViewById<NotificationImageView> (Resource.Id.TagsIcon);
                BillableView = root.FindViewById<View> (Resource.Id.BillableIcon);
                DurationTextView = root.FindViewById<TextView> (Resource.Id.DurationTextView).SetFont (Font.RobotoLight);
                ContinueImageButton = root.FindViewById<ImageButton> (Resource.Id.ContinueImageButton);
                ContinueImageButton.SetOnTouchListener (this);
            }

            public bool OnTouch (View v, MotionEvent e)
            {
                switch (e.Action) {
                case MotionEventActions.Up:
                    owner.OnContinueTimeEntry (this);
                    return false;
                }

                return false;
            }

            public void Bind (TimeEntryHolder datasource)
            {
                DataSource = datasource;

                if (DataSource == null || Handle == IntPtr.Zero) {
                    return;
                }

                var color = Color.Transparent;
                var ctx = ServiceContainer.Resolve<Context> ();

                if (!String.IsNullOrWhiteSpace (DataSource.ProjectName)) {
                    color = Color.ParseColor (ProjectModel.HexColors [DataSource.Color % ProjectModel.HexColors.Length]);
                    ProjectTextView.SetTextColor (color);
                    ProjectTextView.Text = DataSource.ProjectName;
                } else {
                    ProjectTextView.Text = ctx.GetString (Resource.String.RecentTimeEntryNoProject);
                    ProjectTextView.SetTextColor (ctx.Resources.GetColor (Resource.Color.dark_gray_text));
                }

                if (String.IsNullOrWhiteSpace (DataSource.ClientName)) {
                    ClientTextView.Text = String.Empty;
                    ClientTextView.Visibility = ViewStates.Gone;
                } else {
                    ClientTextView.Text = String.Format ("{0} • ", DataSource.ClientName);
                    ClientTextView.Visibility = ViewStates.Visible;
                }

                if (String.IsNullOrWhiteSpace (DataSource.TaskName)) {
                    TaskTextView.Text = String.Empty;
                    TaskTextView.Visibility = ViewStates.Gone;
                } else {
                    TaskTextView.Text = String.Format ("{0} • ", DataSource.TaskName);
                    TaskTextView.Visibility = ViewStates.Visible;
                }

                if (String.IsNullOrWhiteSpace (DataSource.Description)) {
                    DescriptionTextView.Text = ctx.GetString (Resource.String.RecentTimeEntryNoDescription);
                } else {
                    DescriptionTextView.Text = DataSource.Description;
                }

                BillableView.Visibility = DataSource.IsBillable ? ViewStates.Visible : ViewStates.Gone;


                var shape = ColorView.Background as GradientDrawable;
                if (shape != null) {
                    shape.SetColor (color);
                }

                RebindTags ();
                RebindDuration ();
            }

            private void RebindDuration ()
            {
                if (DataSource == null || Handle == IntPtr.Zero) {
                    return;
                }

                var duration = DataSource.TotalDuration;
                DurationTextView.Text = TimeEntryModel.GetFormattedDuration (duration);

                if (DataSource.State == TimeEntryState.Running) {
                    handler.RemoveCallbacks (RebindDuration);
                    handler.PostDelayed (RebindDuration, 1000 - duration.Milliseconds);
                } else {
                    handler.RemoveCallbacks (RebindDuration);
                }

                ShowStopButton ();
            }

            private void ShowStopButton ()
            {
                if (DataSource == null || Handle == IntPtr.Zero) {
                    return;
                }

                if (DataSource.State == TimeEntryState.Running) {
                    ContinueImageButton.SetImageResource (Resource.Drawable.IcStop);
                } else {
                    ContinueImageButton.SetImageResource (Resource.Drawable.IcPlayArrowGrey);
                }
            }

            private void RebindTags ()
            {
                // Protect against Java side being GCed
                if (Handle == IntPtr.Zero) {
                    return;
                }

                var numberOfTags = DataSource.NumberOfTags;
                TagsView.BubbleCount = numberOfTags;
                TagsView.Visibility = numberOfTags > 0 ? ViewStates.Visible : ViewStates.Gone;
            }
        }
    }
}
