using System;
using Foundation;
using UIKit;
using Toggl.Phoebe;
using Toggl.Phoebe.Net;
using XPlatUtils;
using CoreGraphics;
using Toggl.Ross.Data;
using Toggl.Ross.Theme;

namespace Toggl.Ross.ViewControllers
{
    public class MainViewController : UINavigationController
    {
        private Subscription<AuthChangedMessage> subscriptionAuthChanged;
        private Subscription<TogglHttpResponseMessage> subscriptionTogglHttpResponse;
        private NavDelegate navDelegate;
        private UIScreenEdgePanGestureRecognizer interactiveEdgePanGestureRecognizer;
        private UIAlertView upgradeAlert;

        private UITapGestureRecognizer _tapGesture;
        private UIPanGestureRecognizer _panGesture;
        private CGPoint draggingPoint;

        private int menuOffset = 60;
        private LeftViewController menu;

        public bool MenuEnabled {
            get { 
                return _tapGesture.Enabled || _panGesture.Enabled;
            }
            set {
                _tapGesture.Enabled = _panGesture.Enabled = value;
            }
        }


        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.Apply (Style.Screen);
            NavigationBar.Apply (Style.NavigationBar);
            Delegate = navDelegate = new NavDelegate ();

            InteractivePopGestureRecognizer.ShouldBegin = GestureRecognizerShouldBegin;

            interactiveEdgePanGestureRecognizer = new UIScreenEdgePanGestureRecognizer (OnEdgePanGesture) {
                Edges = UIRectEdge.Left,
                ShouldBegin = GestureRecognizerShouldBegin,
            };
            View.AddGestureRecognizer (interactiveEdgePanGestureRecognizer);

            _tapGesture = new UITapGestureRecognizer (OnTapGesture) {
                ShouldReceiveTouch = delegate {
                    return true;
                },
                ShouldRecognizeSimultaneously = delegate {
                    return true;
                },
                CancelsTouchesInView = false
            };

            _panGesture = new UIPanGestureRecognizer (OnPanGesture) {
                ShouldReceiveTouch = delegate {
                    return true;
                },
                ShouldRecognizeSimultaneously = delegate {
                    return true;
                },
                CancelsTouchesInView = false
            };

            View.AddGestureRecognizer (_tapGesture);
            View.AddGestureRecognizer (_panGesture);
        }

        public nfloat Width {
            get {
                return View.Frame.Width;
            }
        }

        public nfloat CurrentX {
            get {
                return View.Frame.X;
            }
        }

        public nfloat MaxDraggingX {
            get {
                return Width - menuOffset;
            }
        }

        public nfloat MinDraggingX {
            get {
                return 0;
            }
        }

        private void OnPanGesture(UIPanGestureRecognizer recognizer) {
            if (!MenuEnabled) {
                return;
            }

            var translation = recognizer.TranslationInView (recognizer.View);
            var velocity = recognizer.TranslationInView (recognizer.View);
            var movement = translation.X - draggingPoint.X;

            if (recognizer.State == UIGestureRecognizerState.Began) {
                draggingPoint = translation;
            } else if (recognizer.State == UIGestureRecognizerState.Changed) { 
                var newX = CurrentX;
                newX += movement;
                if (newX > MinDraggingX || newX < MaxDraggingX) {
                    MoveToLocation (newX);
                }
                draggingPoint = translation;
            } else if (recognizer.State == UIGestureRecognizerState.Ended) {
                
            }
        }

        private void MoveToLocation(nfloat x) {
            var rect = View.Frame;
            rect.Y = 0;
            rect.X = x;
            this.View.Frame = rect;
        }

        private void OnTapGesture(UITapGestureRecognizer recognizer) 
        {
            if (!MenuEnabled) {
                return;
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionAuthChanged == null) {
                subscriptionAuthChanged = bus.Subscribe<AuthChangedMessage> (OnAuthChanged);
            }
            if (subscriptionTogglHttpResponse == null) {
                subscriptionTogglHttpResponse = bus.Subscribe<TogglHttpResponseMessage> (OnTogglHttpResponse);
            }

            ResetRootViewController ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            Application.MarkLaunched ();

            PrepareMenu ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionAuthChanged != null) {
                bus.Unsubscribe (subscriptionAuthChanged);
                subscriptionAuthChanged = null;
            }
            if (subscriptionTogglHttpResponse != null) {
                bus.Unsubscribe (subscriptionTogglHttpResponse);
                subscriptionTogglHttpResponse = null;
            }

            base.ViewWillDisappear (animated);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                if (subscriptionAuthChanged != null) {
                    bus.Unsubscribe (subscriptionAuthChanged);
                    subscriptionAuthChanged = null;
                }
                if (subscriptionTogglHttpResponse != null) {
                    bus.Unsubscribe (subscriptionTogglHttpResponse);
                    subscriptionTogglHttpResponse = null;
                }
            }
            base.Dispose (disposing);
        }

        private void OnAuthChanged (AuthChangedMessage msg)
        {
            ResetRootViewController ();
        }

        private void OnTogglHttpResponse (TogglHttpResponseMessage msg)
        {
            if (msg.StatusCode == System.Net.HttpStatusCode.Gone) {
                if (upgradeAlert == null) {
                    upgradeAlert = new UIAlertView (
                        "MainUpdateNeededTitle".Tr (),
                        "MainUpdateNeededMessage".Tr (),
                        null, "MainUpdateNeededOk".Tr ());
                    upgradeAlert.Clicked += (s, e) => UIApplication.SharedApplication.OpenUrl (new NSUrl (Build.AppStoreUrl));
                }
                upgradeAlert.Show ();
            }
        }

        private void ResetRootViewController ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            UIViewController vc = null;
            bool emptyStack = ViewControllers.Length < 1;
            if (authManager.IsAuthenticated && (emptyStack || ViewControllers [0] is WelcomeViewController)) {
                vc = new LogViewController ();
                MenuEnabled = true;
            } else if (emptyStack || ! (ViewControllers [0] is WelcomeViewController)) {
                vc = new WelcomeViewController ();
                MenuEnabled = false;
            }
            if (vc != null) {
                SetViewControllers (new [] { vc }, ViewControllers.Length > 0);
            }
        }

        private void PrepareMenu()
        {
            menu = new LeftViewController ();
            this.View.Window.InsertSubview (menu.View, 0);
        }

        private void OnEdgePanGesture ()
        {
            var progress = interactiveEdgePanGestureRecognizer.TranslationInView (View).X / View.Bounds.Width;
            progress = (float)Math.Min (1, Math.Max (0, progress));

            switch (interactiveEdgePanGestureRecognizer.State) {
            case UIGestureRecognizerState.Began:
                navDelegate.InteractiveTransition = new UIPercentDrivenInteractiveTransition ();
                PopViewController (true);
                break;
            case UIGestureRecognizerState.Changed:
                navDelegate.InteractiveTransition.UpdateInteractiveTransition (progress);
                break;
            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
                if (progress > 0.5) {
                    navDelegate.InteractiveTransition.FinishInteractiveTransition ();
                } else {
                    navDelegate.InteractiveTransition.CancelInteractiveTransition ();
                }
                navDelegate.InteractiveTransition = null;
                break;
            }
        }

        private bool GestureRecognizerShouldBegin (UIGestureRecognizer recognizer)
        {
            // Make sure we're not mid transition or have too few view controllers
            var transitionCoordinator = this.GetTransitionCoordinator ();
            if (transitionCoordinator != null && transitionCoordinator.IsAnimated) {
                return false;
            }
            if (ViewControllers.Length <= 1) {
                return false;
            }

            var fromViewController = ViewControllers [ViewControllers.Length - 1];
            var toViewController = ViewControllers [ViewControllers.Length - 2];

            var fromDurationViewController = fromViewController as DurationChangeViewController;

            if (fromDurationViewController != null && fromDurationViewController.PreviousControllerType == toViewController.GetType ()) {
                if (recognizer == interactiveEdgePanGestureRecognizer) {
                    return true;
                }
            } else if (recognizer == InteractivePopGestureRecognizer) {
                return true;
            }

            return false;
        }

        private class NavDelegate : UINavigationControllerDelegate
        {
            public UIPercentDrivenInteractiveTransition InteractiveTransition { get; set; }

            public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForOperation (UINavigationController navigationController, UINavigationControllerOperation operation, UIViewController fromViewController, UIViewController toViewController)
            {
                if (toViewController is DurationChangeViewController) {
                    var durationController = (DurationChangeViewController)toViewController;
                    durationController.PreviousControllerType = fromViewController.GetType ();
                    return new DurationChangeViewController.PushAnimator ();
                }
                if (fromViewController is DurationChangeViewController) {
                    var durationController = (DurationChangeViewController)fromViewController;
                    if (durationController.PreviousControllerType == toViewController.GetType ()) {
                        return new DurationChangeViewController.PopAnimator ();
                    }
                    durationController.PreviousControllerType = null;
                }
                return null;
            }

            public override IUIViewControllerInteractiveTransitioning GetInteractionControllerForAnimationController (UINavigationController navigationController, IUIViewControllerAnimatedTransitioning animationController)
            {
                return InteractiveTransition;
            }
        }
    }
}
