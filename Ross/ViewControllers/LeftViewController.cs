using System;
using Foundation;
using UIKit;

namespace Toggl.Ross.ViewControllers
{
    public sealed class LeftViewController : UIViewController
    {
        public LeftViewController () : base ()
        {
        }

        private void CloseMenu() {
            Console.WriteLine("Close menu");
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

            this.View.BackgroundColor = UIColor.Red;

            // Perform any additional setup after loading the view, typically from a nib.
        }
    }
}

