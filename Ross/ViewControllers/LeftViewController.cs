using System;
using System.Collections.Generic;
using UIKit;
using Toggl.Ross.Theme;
using Cirrious.FluentLayouts.Touch;

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

        private UIButton logButton;
        private UIButton reportsButton;
        private UIButton settingsButton;
        private UIButton feedbackButton;
        private UIButton signOutButton;

        public override void LoadView ()
        {
            base.LoadView ();

            //View = new UIView ();
            View.BackgroundColor = UIColor.White;

            logButton = new UIButton ();
            logButton.SetTitle ("Log", UIControlState.Normal);
                
            reportsButton = new UIButton ();
            reportsButton.SetTitle ("Reports", UIControlState.Normal);

            settingsButton = new UIButton ();
            settingsButton.SetTitle ("Settings", UIControlState.Normal);

            feedbackButton = new UIButton ();
            feedbackButton.SetTitle ("Feedback", UIControlState.Normal);
            
            signOutButton = new UIButton ();
            signOutButton.SetTitle ("Sign out", UIControlState.Normal);

            var buttons = new [] { logButton, reportsButton, settingsButton, feedbackButton, signOutButton };
            foreach (var button in buttons) {
                button.Apply (Style.Left.Button);
                View.AddSubview (button);
            }

            View.SubviewsDoNotTranslateAutoresizingMaskIntoConstraints();

            View.AddConstraints (MakeConstraints (View));
        }

        private static IEnumerable<FluentLayout> MakeConstraints (UIView container)
        {
            UIView prev = null;

            foreach (var view in container.Subviews) {

                var startTopMargin = 50.0f;
                var topMargin = 0f;
                var horizMargin = 0f;

                if (view is UIButton) {
                    topMargin = 7f;
                    horizMargin = 15f;
                }

                if (prev == null) {
                    yield return view.AtTopOf (container, topMargin + startTopMargin);
                } else {
                    yield return view.Below (prev, topMargin);
                }

                yield return view.AtLeftOf (container, horizMargin);
                // TODO: 20 == menu offset, refactor
                yield return view.AtRightOf (container, horizMargin + 20);

                prev = view;
            }
        }
    }
}

