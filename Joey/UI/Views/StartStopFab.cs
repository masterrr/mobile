using System;
using Android.Animation;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Util;
using Android.Views.Animations;

namespace Toggl.Joey.UI.Views
{
    public class StartStopFab : FloatingActionButton
    {
        private AnimatorSet switchAnimation;

        private FABButtonState action;

        private Drawable playDraw;
        private Drawable stopDraw;
        private Drawable saveDraw;

        private ColorStateList backgroundTintPlay;
        private ColorStateList backgroundTintStop;
        private ColorStateList backgroundTintSave;

        public StartStopFab (IntPtr handle, JniHandleOwnership transfer)
        : base (handle, transfer)
        {
        }

        public StartStopFab (Context context)
        : this (context, null)
        {
            Initialize (context, null);
        }

        public StartStopFab (Context context, IAttributeSet attrs)
        : base (context, attrs)
        {
            Initialize (context, attrs);
        }

        public StartStopFab (Context context, IAttributeSet attrs, int defStyle)
        : base (context, attrs, defStyle)
        {
            Initialize (context, attrs);
        }

        public FABButtonState ButtonAction
        {
            get {
                return action;
            }

            set {
                if (action == value) {
                    return;
                }
                action = value;

                if (action == FABButtonState.Start) {
                    Switch (playDraw, backgroundTintPlay);
                } else if (action == FABButtonState.Stop) {
                    Switch (stopDraw, backgroundTintStop);
                } else {
                    Switch (saveDraw, backgroundTintSave);
                }
            }
        }

        private void Initialize (Context context, IAttributeSet attrs)
        {
            playDraw = context.Resources.GetDrawable (Resource.Drawable.IcPlayArrowWhite);
            stopDraw = context.Resources.GetDrawable (Resource.Drawable.IcStopWhite);
            saveDraw = context.Resources.GetDrawable (Resource.Drawable.IcPlayArrowWhite);

            var states = new int[][] { new int[]{ } };
            var playColorArr = new int[] { context.Resources.GetColor (Resource.Color.bright_green)};
            var stopColorArr = new int[] { context.Resources.GetColor (Resource.Color.bright_red)};
            var saveColorArr = new int[] { context.Resources.GetColor (Resource.Color.gray)};

            backgroundTintPlay = new ColorStateList (states, playColorArr);
            backgroundTintStop = new ColorStateList (states, stopColorArr);
            backgroundTintSave = new ColorStateList (states, saveColorArr);

            Switch (playDraw, backgroundTintPlay);
        }

        private void Switch (Drawable src, ColorStateList tint)
        {
            const int ScaleDuration = 200;
            const int AlphaDuration = 150;
            const int AlphaInDelay = 50;
            const int InitialDelay = 100;

            if (switchAnimation != null) {
                switchAnimation.Cancel ();
                switchAnimation = null;
            }

            var currentSrc = Drawable;

            // Scaling down animation
            var circleAnimOutX = ObjectAnimator.OfFloat (this, "scaleX", 1, 0.1f);
            var circleAnimOutY = ObjectAnimator.OfFloat (this, "scaleY", 1, 0.1f);
            circleAnimOutX.SetDuration (ScaleDuration);
            circleAnimOutY.SetDuration (ScaleDuration);

            // Alpha out of the icon
            var iconAnimOut = ObjectAnimator.OfInt (currentSrc, "alpha", 255, 0);
            iconAnimOut.SetDuration (AlphaDuration);

            var outSet = new AnimatorSet ();
            outSet.PlayTogether (circleAnimOutX, circleAnimOutY, iconAnimOut);
            outSet.SetInterpolator (AnimationUtils.LoadInterpolator (Context,
                                    Android.Resource.Animation.AccelerateInterpolator));
            outSet.StartDelay = InitialDelay;
            outSet.AnimationEnd += (sender, e) => {
                BackgroundTintList = tint;
                SetImageDrawable (src);
                JumpDrawablesToCurrentState ();
                ((Animator)sender).RemoveAllListeners ();
            };

            // Scaling up animation
            var circleAnimInX = ObjectAnimator.OfFloat (this, "scaleX", 0.1f, 1);
            var circleAnimInY = ObjectAnimator.OfFloat (this, "scaleY", 0.1f, 1);
            circleAnimInX.SetDuration (ScaleDuration);
            circleAnimInY.SetDuration (ScaleDuration);

            // Alpha in of the icon
            src.Alpha = 0;
            var iconAnimIn = ObjectAnimator.OfInt (src, "alpha", 0, 255);
            iconAnimIn.SetDuration (AlphaDuration);
            iconAnimIn.StartDelay = AlphaInDelay;

            var inSet = new AnimatorSet ();
            inSet.PlayTogether (circleAnimInX, circleAnimInY, iconAnimIn);
            inSet.SetInterpolator (AnimationUtils.LoadInterpolator (Context,
                                   Android.Resource.Animation.DecelerateInterpolator));

            switchAnimation = new AnimatorSet ();
            switchAnimation.PlaySequentially (outSet, inSet);
            switchAnimation.Start ();
        }

    }
    public enum FABButtonState {
        Start,
        Stop,
        Save
    }
}
