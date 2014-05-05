﻿using MonoTouch.UIKit;

namespace Toggl.Ross.Theme
{
    public static class Image
    {
        public static UIImage LoginBackground {
            get { return UIImage.FromBundle ("bg"); }
        }

        public static UIImage CircleStart {
            get { return UIImage.FromBundle ("circle-start"); }
        }

        public static UIImage CircleStartPressed {
            get { return UIImage.FromBundle ("circle-start-pressed"); }
        }

        public static UIImage CircleStop {
            get { return UIImage.FromBundle ("circle-stop"); }
        }

        public static UIImage CircleStopPressed {
            get { return UIImage.FromBundle ("circle-stop-pressed"); }
        }

        public static UIImage IconArrowRight {
            get { return UIImage.FromBundle ("icon-arrow-right"); }
        }

        public static UIImage IconBack {
            get { return UIImage.FromBundle ("icon-back"); }
        }

        public static UIImage IconBillable {
            get { return UIImage.FromBundle ("icon-billable"); }
        }

        public static UIImage IconDurationArrow {
            get { return UIImage.FromBundle ("icon-duration-arrow"); }
        }

        public static UIImage IconNav {
            get { return UIImage.FromBundle ("icon-nav"); }
        }

        public static UIImage IconRunning {
            get { return UIImage.FromBundle ("icon-running"); }
        }

        public static UIImage IconTag {
            get { return UIImage.FromBundle ("icon-tag"); }
        }

        public static UIImage IconTagBillable {
            get { return UIImage.FromBundle ("icon-tag-billable"); }
        }

        public static UIImage IconTickBlue {
            get { return UIImage.FromBundle ("icon-tick-blue"); }
        }

        public static UIImage Logo {
            get { return UIImage.FromBundle ("logo"); }
        }
    }
}
