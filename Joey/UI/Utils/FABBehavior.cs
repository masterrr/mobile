using System;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Java.Interop;
using Toggl.Joey.UI.Fragments;
using Toggl.Joey.UI.Views;

namespace Toggl.Joey.UI.Utils
{
    public class FABBehavior : FloatingActionButton.Behavior
    {
        private int minMarginBottom;

        public FABBehavior (Context context) : base ()
        {
            minMarginBottom = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 16, context.Resources.DisplayMetrics);
        }

        public FABBehavior (Context context, IAttributeSet attrs)
        {
        }

        public override bool LayoutDependsOn (CoordinatorLayout parent, FloatingActionButton child, View dependency)
        {
            return dependency.Id == Resource.Id.HomeAppBar || dependency is Snackbar;
        }

        public override bool OnDependentViewChanged (CoordinatorLayout parent, FloatingActionButton child, View dependency)
        {
            var fab = child.JavaCast<StartStopFab> ();
            var currentInfoPaneY = ViewCompat.GetTranslationY (dependency);

            var newTransY = (int)Math.Max (0, Math.Abs (dependency.GetY ()) - currentInfoPaneY - minMarginBottom - fab.Height / 2);
            ViewCompat.SetTranslationY (fab, -newTransY);
            return true;
        }

    }
}
