using System;
using System.Collections.Generic;
using Cirrious.FluentLayouts.Touch;
using UIKit;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using XPlatUtils;
using Toggl.Ross.Data;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;

namespace Toggl.Ross.ViewControllers
{
    public class SettingsViewController : UIViewController
    {
        private LabelSwitchView askProjectView;
        private LabelSwitchView mobileTagView;
        private LabelSwitchView groupedView;
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private bool isResuming;

        public SettingsViewController ()
        {
            Title = "SettingsTitle".Tr ();
            EdgesForExtendedLayout = UIRectEdge.None;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                if (subscriptionSettingChanged != null) {
                    var bus = ServiceContainer.Resolve<MessageBus> ();
                    bus.Unsubscribe (subscriptionSettingChanged);
                    subscriptionSettingChanged = null;
                }
            }
            base.Dispose (disposing);
        }

        public override void LoadView ()
        {
            View = new UIView ().Apply (Style.Screen);

            Add (new SeparatorView ().Apply (Style.Settings.Separator));
            Add (askProjectView = new LabelSwitchView ().Apply (Style.Settings.RowBackground));
            askProjectView.Label.Apply (Style.Settings.SettingLabel);
            askProjectView.Label.Text = "SettingsAskProject".Tr ();
            askProjectView.Switch.ValueChanged += OnAskProjectViewValueChanged;

            Add (new SeparatorView ().Apply (Style.Settings.Separator));
            Add (new UILabel () { Text = "SettingsAskProjectDesc".Tr () } .Apply (Style.Settings.DescriptionLabel));

            Add (new SeparatorView ().Apply (Style.Settings.Separator));
            Add (mobileTagView = new LabelSwitchView ().Apply (Style.Settings.RowBackground));
            mobileTagView.Label.Apply (Style.Settings.SettingLabel);
            mobileTagView.Label.Text = "SettingsMobileTag".Tr ();
            mobileTagView.Switch.ValueChanged += OnMobileTagViewValueChanged;

            Add (new SeparatorView ().Apply (Style.Settings.Separator));
            Add (new UILabel () { Text = "SettingsMobileTagDesc".Tr () } .Apply (Style.Settings.DescriptionLabel));

            Add (new SeparatorView ().Apply (Style.Settings.Separator));
            Add (groupedView = new LabelSwitchView().Apply (Style.Settings.RowBackground));
            groupedView.Label.Apply (Style.Settings.SettingLabel);
            groupedView.Label.Text = "SettingsGrouped".Tr ();
            groupedView.Switch.ValueChanged += OnGroupedViewValueChanged;

            Add (new SeparatorView ().Apply (Style.Settings.Separator));
            Add (new UILabel () { Text = "SettingsGroupedDesc".Tr () } .Apply (Style.Settings.DescriptionLabel));

            View.SubviewsDoNotTranslateAutoresizingMaskIntoConstraints();
            View.AddConstraints (MakeConstraints (View));

            Rebind ();
        }

        private void BindAskProjectView (LabelSwitchView v)
        {
            v.Switch.On = SettingsStore.ChooseProjectForNew;
        }

        private void BindMobileTagView (LabelSwitchView v)
        {
            v.Switch.On = SettingsStore.UseDefaultTag;
        }

        private void BindGroupedView (LabelSwitchView v)
        {
            v.Switch.On = SettingsStore.GroupedTimeEntries;
        }

        private void Rebind ()
        {
            askProjectView.Apply (BindAskProjectView);
            mobileTagView.Apply (BindMobileTagView);
            groupedView.Apply (BindGroupedView);
        }

        private void OnAskProjectViewValueChanged (object sender, EventArgs e)
        {
            SettingsStore.ChooseProjectForNew = askProjectView.Switch.On;
        }

        private void OnMobileTagViewValueChanged (object sender, EventArgs e)
        {
            SettingsStore.UseDefaultTag = mobileTagView.Switch.On;
        }

        private void OnGroupedViewValueChanged (object sender, EventArgs e)
        {
            SettingsStore.GroupedTimeEntries = groupedView.Switch.On;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            if (subscriptionSettingChanged == null) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
            }

            if (isResuming) {
                Rebind ();
            }

            isResuming = true;
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Settings";
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);

            if (subscriptionSettingChanged != null) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Unsubscribe (subscriptionSettingChanged);
                subscriptionSettingChanged = null;
            }
        }

        private void OnSettingChanged (SettingChangedMessage msg)
        {
            Rebind ();
        }

        private SettingsStore SettingsStore
        {
            get { return ServiceContainer.Resolve<SettingsStore> (); }
        }

        private static IEnumerable<FluentLayout> MakeConstraints (UIView container)
        {
            UIView prev = null;

            foreach (var view in container.Subviews) {
                var topMargin = 0f;
                var horizMargin = 0f;

                if (view is UILabel) {
                    topMargin = 7f;
                    horizMargin = 15f;
                } else if (view is SeparatorView && ! (prev is LabelSwitchView)) {
                    topMargin = 20f;
                    horizMargin = 0f;
                }

                if (prev == null) {
                    yield return view.AtTopOf (container, topMargin);
                } else {
                    yield return view.Below (prev, topMargin);
                }

                yield return view.AtLeftOf (container, horizMargin);
                yield return view.AtRightOf (container, horizMargin);

                if (view is LabelSwitchView) {
                    yield return view.Height ().EqualTo (42f);
                } else if (view is SeparatorView) {
                    yield return view.Height ().EqualTo (1f);
                }

                prev = view;
            }
        }

        private class SeparatorView : UIView
        {
            public SeparatorView ()
            {
                TranslatesAutoresizingMaskIntoConstraints = false;
            }
        }
    }
}
