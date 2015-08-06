using System;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Util;
using Android.Views;

namespace Toggl.Joey.UI.Utils
{
    public class AppBarBehavior : AppBarLayout.Behavior
    {
        private int actionBarHeight;
        private int shadowHeight;
        public AppBarBehavior (Context context) : base ()
        {
            actionBarHeight = context.Resources.GetDimensionPixelSize (Resource.Dimension.abc_action_bar_default_height_material);
            shadowHeight = context.Resources.GetDimensionPixelSize (Resource.Dimension.ToolbarDropShadowHeight);
        }

        public AppBarBehavior (Context context, IAttributeSet attrs)
        {
        }

        public override void OnNestedScroll (CoordinatorLayout coordinatorLayout, AppBarLayout child, View target, int dxConsumed, int dyConsumed, int dxUnconsumed, int dyUnconsumed)
        {
            base.OnNestedScroll (coordinatorLayout, child, target, dxConsumed, dyConsumed, dxUnconsumed, dyUnconsumed);
        }

        public override bool OnStartNestedScroll (CoordinatorLayout coordinatorLayout, AppBarLayout child, View directTargetChild, View target, int nestedScrollAxes)
        {
            return base.OnStartNestedScroll (coordinatorLayout, child, directTargetChild, target, nestedScrollAxes);
        }

        public override void OnStopNestedScroll (CoordinatorLayout coordinatorLayout, AppBarLayout child, View target)
        {
            //This is not being detect on normal end :/
            base.OnStopNestedScroll (coordinatorLayout, child, target);
        }

        public override bool OnNestedFling (CoordinatorLayout coordinatorLayout, AppBarLayout child, View target, float velocityX, float velocityY, bool consumed)
        {
            if (IsMidway (child)) {
                Console.WriteLine ("Collapsing programmatically");
                if (Math.Abs (child.Top) >  (child.Height - actionBarHeight - shadowHeight) / 2) {
                    return base.OnNestedFling (coordinatorLayout, child, null, 0, 10000, true);
                } else {
                    return base.OnNestedFling (coordinatorLayout, child, null, 0, -10000, false);
                }
            } else {
                return base.OnNestedFling (coordinatorLayout, child, target, velocityX, velocityY, consumed);
            }
        }

        private bool IsMidway (AppBarLayout child)
        {
            return child.Top != 0 || Math.Abs (child.Top) != child.Height - actionBarHeight - shadowHeight;
        }
    }
}
