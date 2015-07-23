using System;
using System.Collections.Generic;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
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
        private readonly int UndoBarDuration = 5;

        private RecyclerView recyclerView;
        private View emptyMessageView;
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private LogTimeEntriesAdapter logAdapter;
        private StartStopFab startStopBtn;
        private CoordinatorLayout coordinatorLayout;

        private TimeEntriesCollectionView collectionView;

        // Recycler setup
        private DividerItemDecoration dividerDecoration;
        private ShadowItemDecoration shadowDecoration;
        private ItemTouchListener itemTouchListener;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.LogTimeEntriesListFragment, container, false);
            view.FindViewById<TextView> (Resource.Id.EmptyTitleTextView).SetFont (Font.Roboto);
            view.FindViewById<TextView> (Resource.Id.EmptyTextTextView).SetFont (Font.RobotoLight);

            emptyMessageView = view.FindViewById<View> (Resource.Id.EmptyMessageView);
            emptyMessageView.Visibility = ViewStates.Gone;
            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.LogRecyclerView);
            recyclerView.SetLayoutManager (new LinearLayoutManager (Activity));
            startStopBtn = view.FindViewById<StartStopFab> (Resource.Id.StartStopBtn);
            coordinatorLayout = view.FindViewById<CoordinatorLayout> (Resource.Id.logCoordinatorLayout);

            startStopBtn.Click += (sender, e) => {
                var r = new Random ();
                startStopBtn.ButtonAction = r.Next (0,2);
            };

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
                collectionView = isGrouped ? (TimeEntriesCollectionView)new GroupedTimeEntriesView () : new LogTimeEntriesView ();
                logAdapter = new LogTimeEntriesAdapter (recyclerView, collectionView);
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

            recyclerView.GetItemAnimator ().SupportsChangeAnimations = false;
        }

        private void ReleaseRecyclerView ()
        {
            recyclerView.RemoveItemDecoration (shadowDecoration);
            recyclerView.RemoveItemDecoration (dividerDecoration);
            recyclerView.RemoveOnItemTouchListener (itemTouchListener);

            recyclerView.GetAdapter ().Dispose ();
            recyclerView.Dispose ();
            logAdapter = null;

            itemTouchListener.Dispose ();
            dividerDecoration.Dispose ();
            shadowDecoration.Dispose ();
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

        public async void OnDismiss (RecyclerView.ViewHolder viewHolder)
        {
            await collectionView.RemoveItemWithUndoAsync (viewHolder.AdapterPosition);
            Snackbar
            .Make (coordinatorLayout, Resources.GetString (Resource.String.UndoBarDeletedText), UndoBarDuration)
            .SetAction (Resources.GetString (Resource.String.UndoBarButtonText), async v => await collectionView.RestoreItemFromUndoAsync ())
            .Show ();
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
    }
}
