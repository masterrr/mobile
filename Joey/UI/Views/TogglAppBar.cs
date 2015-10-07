using System;
using Android.Content;
using Android.Graphics;
using Android.Support.Design.Widget;
using Android.Util;
using Toggl.Joey.UI.Utils;

namespace Toggl.Joey.UI.Views
{
    public class TogglAppBar : AppBarLayout
    {
        private AppBarLayout.Behavior mBehavior;
        private CoordinatorLayout xParent;

        public TogglAppBar (Context context) : base (context)
        {
        }

        public TogglAppBar (Context context, IAttributeSet attrs) : base (context, attrs)
        {
        }

        private void EnsureDependables ()
        {
            if (mBehavior == null) {
                var lp = (CoordinatorLayout.LayoutParams)LayoutParameters;
                mBehavior = (AppBarLayout.Behavior)lp.Behavior;
            }
            if (xParent == null) {
                xParent = Android.Runtime.Extensions.JavaCast<CoordinatorLayout> (Parent);
            }
        }

        public bool Collapsed
        {
            get {
                return Top != 0;
            }
        }

        public void Toggle()
        {
            if (Collapsed) {
                Expand();
            } else {
                Collapse();
            }
        }

        public void Collapse (bool animate = true)
        {
            EnsureDependables ();
            if (xParent != null && mBehavior != null) {
                if (animate) {
                    mBehavior.OnNestedFling ((CoordinatorLayout)xParent, this, null, 0, 10000, true);
                } else {
                    mBehavior.OnNestedPreScroll ((CoordinatorLayout)xParent, this, null, 0, Height, new int[] {0, 0});
                }
            }
        }

        public void Expand()
        {
            EnsureDependables ();
            if (xParent != null && mBehavior != null) {
                mBehavior.OnNestedFling ((CoordinatorLayout)xParent, this, null, 0, -Height*5, false);
            }
        }
    }
}
