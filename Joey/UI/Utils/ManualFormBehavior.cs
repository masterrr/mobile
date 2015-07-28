using Android.Content;
using Android.Support.Design.Widget;
using Android.Util;
using Android.Views;
using Android.Support.V7.Widget;
using System;

namespace Toggl.Joey.UI.Utils
{
    public class ManualFormBehavior : CoordinatorLayout.Behavior
    {
        public ManualFormBehavior () : base ()
        {
        }

        public ManualFormBehavior (Context context, IAttributeSet attrs) : base (context, attrs)
        {

        }

        public override bool LayoutDependsOn (CoordinatorLayout parent, Java.Lang.Object child, View dependency)
        {
            Console.WriteLine ("dependency: {0}, isEqual: {1}", dependency, dependency is RecyclerView);

            return dependency is RecyclerView;
        }

        public override bool OnDependentViewChanged (CoordinatorLayout parent, Java.Lang.Object child, View dependency)
        {
            Console.WriteLine ("OnViewChanged");
            float translationY = Math.Min (0, dependency.TranslationY - dependency.Height);
            child.TranslationY = translationY;
            return true;
        }
    }
}

