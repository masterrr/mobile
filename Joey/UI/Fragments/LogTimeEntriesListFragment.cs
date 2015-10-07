using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.Widget;
using Android.Support.V7.Widget.Helper;
using Android.Views;
using Android.Widget;
using Toggl.Joey.Data;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Adapters;
using Toggl.Joey.UI.Components;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace Toggl.Joey.UI.Fragments
{
    public class LogTimeEntriesListFragment : Fragment, SwipeDismissCallback.IDismissListener, ItemTouchListener.IItemTouchListener, AppBarLayout.IOnOffsetChangedListener
    {
        private RecyclerView recyclerView;
        private View emptyMessageView;
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private LogTimeEntriesAdapter logAdapter;
        private CoordinatorLayout coordinatorLayout;
        private TimeEntriesCollectionView collectionView;
        private FrameLayout manualEntry;
        private bool isEditShowed;
        private HomeScreenEditFragment manualEditFragment;
        private StartStopFab startStopBtn;
        private TogglAppBar appBar;
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
            manualEntry = view.FindViewById<FrameLayout> (Resource.Id.EditFormView);
            appBar = view.FindViewById<TogglAppBar> (Resource.Id.HomeAppBar);
            SetupCoordinatorViews ();

            return view;
        }

        public override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
            if (manualEditFragment == null) {
                manualEditFragment = new HomeScreenEditFragment ((MainDrawerActivity)Activity);
                FragmentTransaction transaction = ChildFragmentManager.BeginTransaction();
                transaction.Add (Resource.Id.EditFormView, manualEditFragment).Commit();
                EditFormVisible = true;
            }
            appBar.AddOnOffsetChangedListener (this);
            startStopBtn.Click += OnActionClick;
            timerComponent.ToggleFormButton.Click += OnToggleClick;
            timerComponent.Root.Click += OnTimerComponentClick;
            manualEditFragment.FABStateChange += OnFABChange;
            timerComponent.CompactView = true;
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

            manualEditFragment.FABStateChange -= OnFABChange;
            startStopBtn.Click -= OnActionClick;
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

        #region appbar-manualform behavior

        private void SetupCoordinatorViews ()
        {
            var fabLayoutParams = new CoordinatorLayout.LayoutParams (startStopBtn.LayoutParameters);
            fabLayoutParams.AnchorId = recyclerView.Id;
            fabLayoutParams.AnchorGravity = (int) (GravityFlags.Bottom | GravityFlags.End | GravityFlags.Right);
            fabLayoutParams.Behavior = new FABBehavior (Activity);
            startStopBtn.LayoutParameters = fabLayoutParams;

            var appBarLayoutParamaters = new CoordinatorLayout.LayoutParams (appBar.LayoutParameters);
            appBarLayoutParamaters.Behavior = new AppBarLayout.Behavior ();
            appBar.LayoutParameters = appBarLayoutParamaters;
            appBar.Collapse();
        }

        private void OnFABChange (object sender, EventArgs e)
        {
            startStopBtn.ButtonAction = manualEditFragment.EntryState;
        }

        private void OnActionClick (object sender, EventArgs e)
        {
            manualEditFragment.RequestAction();
        }

        private void OnTimerComponentClick (object sender, EventArgs e)
        {
            appBar.Expand();
            ViewCompat.SetNestedScrollingEnabled (recyclerView, true);
        }

        private void OnToggleClick (object sender, EventArgs e)
        {
            ViewCompat.SetNestedScrollingEnabled (recyclerView, appBar.Collapsed);
            appBar.Toggle();
        }

        public bool EditFormVisible
        {
            get {
                return isEditShowed;
            } set {
                if (isEditShowed == value) {
                    return;
                }
                isEditShowed = value;
                var activity = (MainDrawerActivity)Activity;
                activity.ToolbarMode = isEditShowed ? MainDrawerActivity.ToolbarModes.DurationOnly : MainDrawerActivity.ToolbarModes.Compact;
            }
        }

        public void OnOffsetChanged (AppBarLayout layout, int verticalOffset)
        {
            float progress = (float)Math.Abs (verticalOffset) / (float) appBar.TotalScrollRange;
            timerComponent.AnimateState = progress;
            manualEntry.Alpha = 1 - progress;
            manualEntry.TranslationY = -verticalOffset;
            if (progress == 1) {
                ViewCompat.SetNestedScrollingEnabled (recyclerView, false);
            }
        }

        private TimerComponent timerComponent
        {
            get {
                var activity = (MainDrawerActivity)Activity;
                return activity.Timer;
            }
        }

        #endregion

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
            var duration = TimeEntriesCollectionView.UndoSecondsInterval * 1000;

            await collectionView.RemoveItemWithUndoAsync (viewHolder.AdapterPosition);
            var snackBar = Snackbar
                           .Make (coordinatorLayout, Resources.GetString (Resource.String.UndoBarDeletedText), duration)
                           .SetAction (Resources.GetString (Resource.String.UndoBarButtonText),
                                       async v => await collectionView.RestoreItemFromUndoAsync ());
            ChangeSnackBarColor (snackBar);
            snackBar.Show ();
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

        // Temporal hack to change the
        // action color in snack bar
        private void ChangeSnackBarColor (Snackbar snack)
        {
            var group = (ViewGroup) snack.View;
            for (int i = 0; i < group.ChildCount; i++) {
                View v = group.GetChildAt (i);
                var textView = v as TextView;
                if (textView != null) {
                    TextView t = textView;
                    if (t.Text == Resources.GetString (Resource.String.UndoBarButtonText)) {
                        t.SetTextColor (Resources.GetColor (Resource.Color.material_green));
                    }
                }
            }
        }
    }
}
