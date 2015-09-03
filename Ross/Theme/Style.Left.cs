using UIKit;

namespace Toggl.Ross.Theme
{
    public static partial class Style
    {
        public static class Left
        {
            public static void Button (UIButton v)
            {                
                v.SetTitleColor (UIColor.Black, UIControlState.Normal);
                v.Font = UIFont.FromName ("HelveticaNeue", 14f);
            }
        }
    }
}

