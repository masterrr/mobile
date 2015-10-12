using System;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.Models;
using XPlatUtils;

namespace Toggl.Joey.UI.Fragments
{
    public class ChangeTimeEntryStopTimeDialogFragment : BaseDateTimeDialogFragment
    {
        public ChangeTimeEntryStopTimeDialogFragment (ITimeEntryModel model) : base (model)
        {
        }

        public ChangeTimeEntryStopTimeDialogFragment ()
        {
        }

        public ChangeTimeEntryStopTimeDialogFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public override void OnStart ()
        {
            base.OnStart ();
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Change Stop Time";
        }

        protected override DateTime GetInitialDate ()
        {
            var date = Time.Now;
            if (Model != null && Model.StopTime.HasValue) {
                date = Model.StopTime.Value.ToLocalTime ().Date;
            }
            return date;
        }

        protected override DateTime GetInitialTime ()
        {
            var time = Time.Now;
            if (Model != null && Model.StopTime.HasValue) {
                time = Model.StopTime.Value.ToLocalTime ();
            }
            return time;
        }

        protected override async void UpdateDate (DateTime dateTime)
        {
            Model.StopTime = dateTime;
            if (Model.StartTime.IsMinValue()) {
                var duration = Model.StopTime - Time.UtcNow;
                Model.SetDuration ((TimeSpan)duration);
            }
            await Model.SaveAsync ();
        }

        protected override int DialogTitleId ()
        {
            return Resource.String.ChangeTimeEntryStopTimeDialogTitle;
        }
    }
}
