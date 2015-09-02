using System;
using Foundation;
using UIKit;

namespace Toggl.Ross.ViewControllers
{
    public sealed class LeftViewController : UIViewController
    {
        private UIPanGestureRecognizer _panGesture;
        private UITapGestureRecognizer _tapGesture;

        public UIViewController ContentViewController { get; private set; }
        public UIViewController MenuViewController { get; private set; }

        public int MenuWidth { get; set; }

        public bool Disabled {
            get {
                return _panGesture.Enabled || _tapGesture.Enabled;
            }
            set {
                _panGesture.Enabled = _tapGesture.Enabled = value;
            }
        }

        public LeftViewController () : base ("LeftViewController", null)
        {
        }

        public LeftViewController(UIViewController rootVC, UIViewController contentVC, UIViewController menuVC) {
            InitVC(contentVC, menuVC);

            rootVC.AddChildViewController(this);
            rootVC.View.AddSubview(View);
            DidMoveToParentViewController(rootVC);  
        }   
            
        private void InitVC(UIViewController contentVC, UIViewController menuVC) 
        {
            ContentViewController = contentVC;
            MenuViewController = menuVC;
        }
            
        public override void DidReceiveMemoryWarning ()
        {
            // Releases the view if it doesn't have a superview.
            base.DidReceiveMemoryWarning ();
            
            // Release any cached data, images, etc that aren't in use.
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            
            // Perform any additional setup after loading the view, typically from a nib.
        }
    }
}

