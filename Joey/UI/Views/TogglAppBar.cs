using System;
using Android.Content;
using Android.Graphics;
using Android.Support.Design.Widget;
using Android.Util;

namespace Toggl.Joey.UI.Views
{
    public class TogglAppBar : AppBarLayout
    {
        private AppBarLayout.Behavior mBehavior;
        private CoordinatorLayout xParent;
        private ToolbarChange mQueuedChange = ToolbarChange.None;
        private bool mAfterFirstDraw = false;

        public TogglAppBar (Context context) : base (context)
        {
        }

        public TogglAppBar (Context context, IAttributeSet attrs) : base (context, attrs)
        {
        }

        protected override void OnAttachedToWindow()
        {
            xParent = Android.Runtime.Extensions.JavaCast<CoordinatorLayout> (Parent);
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure (widthMeasureSpec, heightMeasureSpec);

            var lp = new CoordinatorLayout.LayoutParams (xParent.LayoutParameters);
            mBehavior = new AppBarLayout.Behavior ();
//            LayoutParameters = lp;
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            base.OnLayout (changed, l, t, r, b);
            if (r - l > 0 && b - t > 0 && mAfterFirstDraw && mQueuedChange != ToolbarChange.None) {
                AnalyzeQueuedChange();
            }
        }

        protected override void OnDraw (Canvas canvas)
        {
            base.OnDraw (canvas);
            if (!mAfterFirstDraw) {
                mAfterFirstDraw = true;
                if (mQueuedChange != ToolbarChange.None) {
                    AnalyzeQueuedChange();
                }
            }
        }

        private void AnalyzeQueuedChange()
        {
            switch (mQueuedChange) {
            case ToolbarChange.Collapse:
                PerformCollapsingWithoutAnimation();
                break;
            case ToolbarChange.CollapseWithAnimation:
                PerformCollapsingWithAnimation();
                break;
            case ToolbarChange.Expand:
                PerformExpandingWithoutAnimation();
                break;
            case ToolbarChange.ExpandWithAnimation:
                PerformExpandingWithAnimation();
                break;
            }

            mQueuedChange = ToolbarChange.None;
        }

        public void CollapseToolbar()
        {
            CollapseToolbar (false);
        }

        public void CollapseToolbar (bool withAnimation)
        {
            mQueuedChange = withAnimation ? ToolbarChange.CollapseWithAnimation : ToolbarChange.Collapse;
            RequestLayout();
        }

        public void ExpandToolbar()
        {
            ExpandToolbar (false);
        }

        public void ExpandToolbar (bool withAnimation)
        {
            mQueuedChange = withAnimation ? ToolbarChange.ExpandWithAnimation : ToolbarChange.Expand;
            RequestLayout();
        }

        public void PerformCollapsingWithoutAnimation()
        {
            if (xParent != null ) {
                mBehavior.OnNestedPreScroll ((CoordinatorLayout)xParent, this, null, 0, Height, new int[] {0, 0});
            }
        }

        public void PerformCollapsingWithAnimation()
        {
            if (xParent != null ) {
                mBehavior.OnNestedFling ((CoordinatorLayout)xParent, this, null, 0, Height, true);
            }
        }

        public void PerformExpandingWithoutAnimation()
        {
            if (xParent != null ) {
                mBehavior.SetTopAndBottomOffset (0);
            }
        }

        public void PerformExpandingWithAnimation()
        {
            if (xParent != null ) {
                mBehavior.OnNestedFling ((CoordinatorLayout)xParent, this, null, 0, -Height * 5, false);
            }
        }

        private enum ToolbarChange {
            Collapse,
            CollapseWithAnimation,
            Expand,
            ExpandWithAnimation,
            None
        }
    }

}

