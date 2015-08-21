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

        public void Collapse()
        {
            EnsureDependables ();
            if (xParent != null && mBehavior != null) {
                mBehavior.OnNestedFling ((CoordinatorLayout)xParent, this, null, 0, Height, true);
            }
        }

        public void Expand()
        {
            EnsureDependables ();
            if (xParent != null && mBehavior != null) {
                mBehavior.OnNestedFling ((CoordinatorLayout)xParent, this, null, 0, -Height * 5, false);
            }
        }
    }
}