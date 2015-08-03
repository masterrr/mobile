using System;
using System.Collections.Generic;
using UIKit;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using XPlatUtils;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Ross.ViewControllers
{
    public class DurationChangeViewController : UIViewController
    {
        private readonly ITimeEntryModel model;
        private DurationView durationView;
        private UIBarButtonItem barButtonItem;
        private bool isSaving;

        public DurationChangeViewController (ITimeEntryModel model)
        {
            this.model = model;
        }

        public override void LoadView ()
        {
            View = new UIImageView () {
                BackgroundColor = Color.Black,
                ContentMode = UIViewContentMode.Bottom,
            };
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Configure navigation item
            NavigationItem.TitleView = durationView = new DurationView () {
                Hint = model != null ? model.GetDuration () : TimeSpan.Zero,
            } .Apply (Style.DurationEdit.DurationView);
            durationView.DurationChanged += (s, e) => Rebind ();
            durationView.SizeToFit ();

            barButtonItem = new UIBarButtonItem (
                model == null ? "DurationAdd".Tr () : "DurationSet".Tr (),
                UIBarButtonItemStyle.Plain,
                OnNavigationBarRightClicked
            ).Apply (Style.NavLabelButton);

            Rebind ();
        }

        private void Rebind ()
        {
            var isValid = durationView.EnteredDuration != Duration.Zero && durationView.EnteredDuration.IsValid;
            NavigationItem.RightBarButtonItem = isValid ? barButtonItem : null;
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            durationView.BecomeFirstResponder ();

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Change Duration";
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            durationView.ResignFirstResponder ();
        }

        private async void OnNavigationBarRightClicked (object sender, EventArgs args)
        {
            if (isSaving) {
                return;
            }

            try {
                isSaving = true;
                var duration = TimeSpan.Zero;

                var entered = durationView.EnteredDuration;
                if (model == null || model.State == TimeEntryState.New) {
                    duration = new TimeSpan (entered.Hours, entered.Minutes, 0);
                } else {
                    duration = model.GetDuration ();
                    // Keep the current seconds and milliseconds
                    duration = new TimeSpan (0, entered.Hours, entered.Minutes, duration.Seconds, duration.Milliseconds);
                }

                if (model == null) {
                    var m = await TimeEntryModel.CreateFinishedAsync (duration);
                    var controller = new EditTimeEntryViewController (new List<TimeEntryData> { m });

                    // Replace self with edit controller on the stack
                    var vcs = NavigationController.ViewControllers;
                    vcs [vcs.Length - 1] = controller;
                    NavigationController.SetViewControllers (vcs, true);

                    // Ping analytics
                    ServiceContainer.Resolve<ITracker>().SendTimerStartEvent (TimerStartSource.AppManual);
                } else {
                    model.SetDuration (duration);
                    await model.SaveAsync ();
                    NavigationController.PopViewController (true);
                }
            } finally {
                isSaving = false;
            }
        }

        public Type PreviousControllerType { get; set; }

        private static UIImage GetBlurred (UIView view)
        {
            UIGraphics.BeginImageContextWithOptions (view.Bounds.Size, true, view.Window.Screen.Scale);

            var frame = view.Frame;
            frame.Y = 0;
            view.DrawViewHierarchy (frame, false);

            var snapshotImage = UIGraphics.GetImageFromCurrentImageContext ();
            snapshotImage = snapshotImage.ApplyDarkEffect ();

            UIGraphics.EndImageContext ();

            return snapshotImage;
        }

        public class PushAnimator : UIViewControllerAnimatedTransitioning
        {
            public override void AnimateTransition (IUIViewControllerContextTransitioning transitionContext)
            {
                var toController = (DurationChangeViewController)transitionContext.GetViewControllerForKey (UITransitionContext.ToViewControllerKey);
                var fromController = transitionContext.GetViewControllerForKey (UITransitionContext.FromViewControllerKey);
                var container = transitionContext.ContainerView;

                var imageView = (UIImageView)toController.View;
                imageView.Image = GetBlurred (fromController.View);

                toController.View.Frame = transitionContext.GetFinalFrameForViewController (toController);
                toController.View.Alpha = 0;
                container.InsertSubviewAbove (toController.View, fromController.View);

                UIView.Animate (
                    TransitionDuration (transitionContext),
                delegate {
                    toController.View.Alpha = 1;
                },
                delegate {
                    if (!transitionContext.TransitionWasCancelled) {
                        fromController.View.RemoveFromSuperview ();
                    }
                    transitionContext.CompleteTransition (!transitionContext.TransitionWasCancelled);
                }
                );
            }

            public override double TransitionDuration (IUIViewControllerContextTransitioning transitionContext)
            {
                return 0.4f;
            }
        }

        public class PopAnimator : UIViewControllerAnimatedTransitioning
        {
            public override void AnimateTransition (IUIViewControllerContextTransitioning transitionContext)
            {
                var toController = transitionContext.GetViewControllerForKey (UITransitionContext.ToViewControllerKey);
                var fromController = (DurationChangeViewController)transitionContext.GetViewControllerForKey (UITransitionContext.FromViewControllerKey);
                var container = transitionContext.ContainerView;

                toController.View.Frame = transitionContext.GetFinalFrameForViewController (toController);
                container.InsertSubviewBelow (toController.View, fromController.View);
                fromController.View.Alpha = 1;

                UIView.Animate (
                    TransitionDuration (transitionContext),
                delegate {
                    fromController.View.Alpha = 0;
                },
                delegate {
                    if (!transitionContext.TransitionWasCancelled) {
                        fromController.View.RemoveFromSuperview ();
                    }
                    transitionContext.CompleteTransition (!transitionContext.TransitionWasCancelled);
                }
                );
            }

            public override double TransitionDuration (IUIViewControllerContextTransitioning transitionContext)
            {
                return 0.4f;
            }
        }
    }
}
