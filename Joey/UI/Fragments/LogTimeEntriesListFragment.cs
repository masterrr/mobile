using System;
using System.Collections.Generic;
using Android.Animation;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V7.Widget;
using Android.Support.V7.Widget.Helper;
using Android.Views;
using Android.Widget;
using Toggl.Joey.Data;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Adapters;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Joey.UI.Fragments
{
    public class LogTimeEntriesListFragment : Fragment, SwipeDismissCallback.IDismissListener, ItemTouchListener.IItemTouchListener
    {
        private static readonly int UndbarVisibleTime = 6000;
        private static readonly int UndbarScrollThreshold = 100;

        private RecyclerView recyclerView;
        private View emptyMessageView;
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private LogTimeEntriesAdapter logAdapter;
        private bool isUndoShowed;

        // Recycler setup
        private DividerItemDecoration dividerDecoration;
        private ShadowItemDecoration shadowDecoration;
        private ItemTouchListener itemTouchListener;
        private RecyclerViewScrollDetector scrollListener;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.LogTimeEntriesListFragment, container, false);
            view.FindViewById<TextView> (Resource.Id.EmptyTitleTextView).SetFont (Font.Roboto);
            view.FindViewById<TextView> (Resource.Id.EmptyTextTextView).SetFont (Font.RobotoLight);

            emptyMessageView = view.FindViewById<View> (Resource.Id.EmptyMessageView);
            emptyMessageView.Visibility = ViewStates.Gone;
            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.LogRecyclerView);
            recyclerView.SetLayoutManager (new LinearLayoutManager (Activity));

            return view;
        }

        public override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
        }

        public override void OnResume ()
        {
            EnsureAdapter ();
            base.OnResume ();
        }

        public override bool UserVisibleHint
        {
            get { return base.UserVisibleHint; }
            set {
                base.UserVisibleHint = value;
                EnsureAdapter ();
            }
        }

        private void EnsureAdapter ()
        {
            if (recyclerView.GetAdapter() == null) {
                var isGrouped = ServiceContainer.Resolve<SettingsStore> ().GroupedTimeEntries;
                if (isGrouped) {
                    logAdapter = new LogTimeEntriesAdapter (recyclerView, new GroupedTimeEntriesView());
                } else {
                    logAdapter = new LogTimeEntriesAdapter (recyclerView, new LogTimeEntriesView());
                }
                recyclerView.SetAdapter (logAdapter);
                SetupRecyclerView ();
            }
        }

        public override void OnDestroyView ()
        {
            // Protect against Java side being GCed
            if (Handle == IntPtr.Zero) {
                return;
            }

            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionSettingChanged != null) {
                bus.Unsubscribe (subscriptionSettingChanged);
                subscriptionSettingChanged = null;
            }

            ReleaseRecyclerView ();

            base.OnDestroyView ();
        }

        private void SetupRecyclerView ()
        {
            // Touch listeners.
            itemTouchListener = new ItemTouchListener (recyclerView, this);
            recyclerView.AddOnItemTouchListener (itemTouchListener);

            var touchCallback = new SwipeDismissCallback (ItemTouchHelper.Up | ItemTouchHelper.Down, ItemTouchHelper.Left, this);
            var touchHelper = new ItemTouchHelper (touchCallback);
            touchHelper.AttachToRecyclerView (recyclerView);

            // Decorations.
            dividerDecoration = new DividerItemDecoration (Activity, DividerItemDecoration.VerticalList);
            shadowDecoration = new ShadowItemDecoration (Activity);
            recyclerView.AddItemDecoration (dividerDecoration);
            recyclerView.AddItemDecoration (shadowDecoration);

            scrollListener = new RecyclerViewScrollDetector (this);
            recyclerView.AddOnScrollListener (scrollListener);
            recyclerView.GetItemAnimator ().SupportsChangeAnimations = false;
        }

        private void ReleaseRecyclerView ()
        {
            recyclerView.RemoveOnScrollListener (scrollListener);
            recyclerView.RemoveItemDecoration (shadowDecoration);
            recyclerView.RemoveItemDecoration (dividerDecoration);
            recyclerView.RemoveOnItemTouchListener (itemTouchListener);

            recyclerView.GetAdapter ().Dispose ();
            recyclerView.Dispose ();
            logAdapter = null;

            itemTouchListener.Dispose ();
            dividerDecoration.Dispose ();
            shadowDecoration.Dispose ();
            scrollListener.Dispose ();
        }

        private void OnSettingChanged (SettingChangedMessage msg)
        {
            // Protect against Java side being GCed
            if (Handle == IntPtr.Zero) {
                return;
            }

            if (msg.Name == SettingsStore.PropertyGroupedTimeEntries) {
                EnsureAdapter();
            }
        }

        #region IDismissListener implementation

        public bool CanDismiss (RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
        {
            var adapter = recyclerView.GetAdapter ();
            return adapter.GetItemViewType (viewHolder.LayoutPosition) == LogTimeEntriesAdapter.ViewTypeContent;
        }

        public void OnDismiss (RecyclerView.ViewHolder viewHolder, int position)
        {
            var undoAdapter = recyclerView.GetAdapter () as IUndoCapabilities;
            undoAdapter.RemoveItemWithUndo (viewHolder);
            ShowUndoBar ();
        }

        #endregion

        #region IRecyclerViewOnItemClickListener implementation

        public void OnItemClick (RecyclerView parent, View clickedView, int position)
        {
            var intent = new Intent (Activity, typeof (EditTimeEntryActivity));

            IList<string> guids = ((TimeEntryHolder)logAdapter.GetEntry (position)).TimeEntryGuids;
            intent.PutStringArrayListExtra (EditTimeEntryActivity.ExtraGroupedTimeEntriesGuids, guids);
            intent.PutExtra (EditTimeEntryActivity.IsGrouped, guids.Count > 1);

            StartActivity (intent);
        }

        public void OnItemLongClick (RecyclerView parent, View clickedView, int position)
        {
            OnItemClick (parent, clickedView, position);
        }

        public bool CanClick (RecyclerView view, int position)
        {
            var adapter = recyclerView.GetAdapter ();
            return adapter.GetItemViewType (position) == LogTimeEntriesAdapter.ViewTypeContent;
        }

        #endregion

        #region Undo bar

        private void ShowUndoBar ()
        {
            // Protect against Java side being GCed
            if (Handle == IntPtr.Zero) {
                return;
            }

            if (!UndoBarVisible) {
                UndoBarVisible = true;
            }
        }

        public void RemoveItemAndHideUndoBar ()
        {
            UndoBarVisible = false;

            // Remove item permanently
            var undoAdapter = recyclerView.GetAdapter () as IUndoCapabilities;
            if (undoAdapter != null) {
                undoAdapter.ConfirmItemRemove ();
            }
        }

        private void UndoBtnClicked (object sender, EventArgs e)
        {
            // Protect against Java side being GCed
            if (Handle == IntPtr.Zero) {
                return;
            }

            // Undo remove item.
            var undoAdapter = recyclerView.GetAdapter () as IUndoCapabilities;
            undoAdapter.RestoreItemFromUndo ();

            UndoBarVisible = false;
        }

        public bool UndoBarVisible
        {
            get {
                return isUndoShowed;
            } set {
                if (isUndoShowed == value) {
                    return;
                }
                isUndoShowed = value;
            }
        }

        #endregion

        private class RecyclerViewScrollDetector : RecyclerView.OnScrollListener
        {
            private readonly LogTimeEntriesListFragment owner;

            public RecyclerViewScrollDetector (LogTimeEntriesListFragment owner)
            {
                this.owner = owner;
                ScrollThreshold = UndbarScrollThreshold;
            }

            public int ScrollThreshold { get; set; }

            public RecyclerView.OnScrollListener OnScrollListener { get; set; }

            public override void OnScrolled (RecyclerView recyclerView, int dx, int dy)
            {
                if (OnScrollListener != null) {
                    OnScrollListener.OnScrolled (recyclerView, dx, dy);
                }

                var isSignificantDelta = Math.Abs (dy) > ScrollThreshold;
                if (isSignificantDelta) {
                    OnScrollMoved();
                }
            }

            public override void OnScrollStateChanged (RecyclerView recyclerView, int newState)
            {
                if (OnScrollListener != null) {
                    OnScrollListener.OnScrollStateChanged (recyclerView, newState);
                }
                base.OnScrollStateChanged (recyclerView, newState);
            }

            private void OnScrollMoved()
            {
                if (owner.UndoBarVisible) {
                    owner.RemoveItemAndHideUndoBar ();
                }
            }
        }
    }
}
