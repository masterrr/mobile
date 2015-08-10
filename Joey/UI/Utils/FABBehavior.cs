using System;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace Toggl.Joey.UI.Utils
{
    public class FABBehavior : CoordinatorLayout.Behavior
    {
        private int shadowHeight;

        public FABBehavior (Context context) : base ()
        {
            shadowHeight = context.Resources.GetDimensionPixelSize (Resource.Dimension.ToolbarDropShadowHeight);
        }

        public FABBehavior (Context context, IAttributeSet attrs)
        {
        }

        public override bool LayoutDependsOn (CoordinatorLayout parent, Java.Lang.Object child, View dependency)
        {
            return dependency.Id == Resource.Id.HomeAppBar || dependency is LinearLayout; // To catch SnackBar events.
        }

        public override bool OnDependentViewChanged (CoordinatorLayout parent, Java.Lang.Object child, View dependency)
        {
            var childView = ((View)child);
            if (dependency.Id == Resource.Id.HomeAppBar) {
                float progress = (float) (dependency.Bottom - 120) / (float) (dependency.Height - 120);
                float delta = childView.Top - dependency.Height + (float) (childView.Height / 2) + shadowHeight;
                ViewCompat.SetTranslationY (childView, - (delta * progress));
            } else if (dependency.Id == Resource.Id.LogRecyclerView) {
                if (dependency.Top > dependency.Height / 5) {
                    ViewCompat.SetTranslationY (childView, - (childView.Top - dependency.Top) - shadowHeight - (childView.Height / 2));
                }
            } else if (dependency is LinearLayout && Math.Abs (childView.TranslationY) < childView.Height) {
                ViewCompat.SetTranslationY (childView, dependency.TranslationY - (childView.Height / 2));
            }
            return true;
        }
    }
}
