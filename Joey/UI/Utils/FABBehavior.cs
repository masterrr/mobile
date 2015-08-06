using System;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace Toggl.Joey.UI.Utils
{
    public class FABBehavior : FloatingActionButton.Behavior
    {
        private int minMarginBottom;
        private int shadowHeight;
        private bool editFormCollapsed;

        public FABBehavior (Context context) : base ()
        {
            minMarginBottom = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 16, context.Resources.DisplayMetrics);
            shadowHeight = context.Resources.GetDimensionPixelSize (Resource.Dimension.ToolbarDropShadowHeight);
            editFormCollapsed = false;
        }

        public FABBehavior (Context context, IAttributeSet attrs)
        {
        }

        public override bool LayoutDependsOn (CoordinatorLayout parent, FloatingActionButton child, View dependency)
        {
            return dependency.Id == Resource.Id.HomeAppBar || dependency is LinearLayout;
        }

        public override bool OnDependentViewChanged (CoordinatorLayout parent, FloatingActionButton child, View dependency)
        {
            if (dependency.Id == Resource.Id.HomeAppBar) {
                float progress = (float) (dependency.Bottom - 120) / (float) (dependency.Height - 120);
                float delta = child.Top - dependency.Height + (float) (child.Height / 2) + shadowHeight;
                ViewCompat.SetTranslationY (child, - (delta * progress));
            } else if (dependency.Id == Resource.Id.LogRecyclerView) {
                if (dependency.Top > dependency.Height /5) {
                    ViewCompat.SetTranslationY (child, - (child.Top - dependency.Top) - shadowHeight - (child.Height / 2));
                }
            } else if (dependency is LinearLayout && editFormCollapsed) {
                ViewCompat.SetTranslationY (child,  dependency.TranslationY - (child.Height / 2));
            }
            return true;
        }
    }
}
