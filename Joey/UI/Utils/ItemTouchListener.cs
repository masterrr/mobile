using System;
using Android.Support.V7.Widget;
using Android.Views;
using Java.Lang;
using Toggl.Joey.UI.Views;

namespace Toggl.Joey.UI.Utils
{
    public class ItemTouchListener : GestureDetector.SimpleOnGestureListener, RecyclerView.IOnItemTouchListener
    {
        public interface IItemTouchListener
        {
            void OnItemClick (RecyclerView parent, View clickedView, int position);

            void OnItemLongClick (RecyclerView parent, View clickedView, int position);

            bool CanClick (RecyclerView parent, int position);
        }

        private IItemTouchListener listener;
        private readonly RecyclerView recyclerView;
        private readonly SnappyLayout snappyLayout;
        private GestureDetector gestureDetector;
        private bool IsScrolling;

        public ItemTouchListener (RecyclerView recyclerView, IItemTouchListener listener, SnappyLayout snappy)
        {

            if (recyclerView == null || listener == null) {
                throw new IllegalArgumentException ("RecyclerView and Listener arguments can not be null");
            }

            IsScrolling = false;
            this.recyclerView = recyclerView;
            this.listener = listener;
            this.snappyLayout = snappy;
            gestureDetector = new GestureDetector (recyclerView.Context, this);
            recyclerView.AddOnScrollListener (new RecyclerViewScrollDetector (this));
        }

        private bool IsEnabled
        {
            get {
                var isEnabled = !recyclerView.GetItemAnimator().IsRunning || IsScrolling;
                if (Android.OS.Build.VERSION.SdkInt > Android.OS.BuildVersionCodes.JellyBeanMr1) {
                    isEnabled = ! (recyclerView.IsInLayout || recyclerView.GetItemAnimator().IsRunning || IsScrolling);
                }
                return isEnabled;
            }
        }

        public bool ScrolledTop;

        private float prevY;
        public bool OnInterceptTouchEvent (RecyclerView rv, MotionEvent e)
        {
            if (snappyLayout.ActiveChild == 1) {
                if (e.Action == MotionEventActions.Move && ScrolledTop && prevY < e.RawY) { // if scrolling up from top give stage to snappy.
                    snappyLayout.Activated = true;
                } else {
                    snappyLayout.Activated = false;
                }
            }

            prevY = e.Action == MotionEventActions.Up ? 0 : e.RawY;
            if (IsEnabled) {
                gestureDetector.OnTouchEvent (e);
            }
            return false;
        }

        public void OnTouchEvent (RecyclerView rv, MotionEvent e)
        {
        }

        public override void OnShowPress (MotionEvent e)
        {
            View view = GetChildViewUnder (e);
            if (view != null) {
                view.Pressed = true;
                view.Selected = true;
            }
        }

        public override void OnLongPress (MotionEvent e)
        {
            OnSingleTapUp (e);
        }

        public override bool OnSingleTapUp (MotionEvent e)
        {
            View view = GetChildViewUnder (e);
            if (view == null) { return false; }

            int position = recyclerView.GetChildLayoutPosition (view);
            if ( listener.CanClick (recyclerView, position)) {
                listener.OnItemClick (recyclerView, view, position);
                view.Pressed = false;
                view.Selected = false;
                return true;
            }
            return false;
        }

        public void OnRequestDisallowInterceptTouchEvent (bool p0)
        {
        }

        private View GetChildViewUnder (MotionEvent e)
        {
            var view = recyclerView.FindChildViewUnder (e.GetX (), e.GetY ());
            if (view == null) {
                return null;
            }

            // Find better way!
            var playBtn = view.FindViewById<Android.Widget.ImageButton> (Resource.Id.ContinueImageButton);
            if (playBtn != null) {
                return IsPointInsideView (e.GetX (), e.GetY (), playBtn) ? null : view;
            }
            return view;
        }

        private bool IsPointInsideView (float x, float y, View view)
        {
            var location = new int[2];
            view.GetLocationOnScreen (location);
            int viewX = location [0];

            //point is inside view bounds
            if (x > viewX && x < (viewX + view.Width)) {
                return true;
            }

            return false;
        }

        private class RecyclerViewScrollDetector : RecyclerView.OnScrollListener
        {
            private readonly ItemTouchListener touchListener;

            public RecyclerViewScrollDetector (ItemTouchListener touchListener)
            {
                this.touchListener = touchListener;
            }

            public override void OnScrollStateChanged (RecyclerView recyclerView, int newState)
            {
                base.OnScrollStateChanged (recyclerView, newState);
                touchListener.IsScrolling = newState != RecyclerView.ScrollStateIdle;
            }
        }
    }
}

