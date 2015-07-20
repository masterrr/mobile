using System;
using Android.Animation;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;

namespace Toggl.Joey.UI.Views
{
    public class SnappyLayout : ViewGroup
    {
        private int activeChild;
        private float translateY;
        private float maxTranslateY;
        private float scrollThreshold;
        private float touchSlop;
        private float touchY;
        private float touchX;
        private bool isDragging;
        private bool activated = true;
        private bool lockFirstChild = false;
        private Animator scrollAnim;

        public SnappyLayout (Context ctx) : base (ctx)
        {
            Initialize ();
        }

        public SnappyLayout (IntPtr javaRef, JniHandleOwnership xfer) : base (javaRef, xfer)
        {
            Initialize ();
        }

        public SnappyLayout (Context ctx, IAttributeSet attrs) : base (ctx, attrs)
        {
            Initialize ();
        }

        public SnappyLayout (Context ctx, IAttributeSet attrs, int defStyle) : base (ctx, attrs, defStyle)
        {
            Initialize ();
        }

        private void Initialize()
        {
            var dm = Resources.DisplayMetrics;

            scrollThreshold = TypedValue.ApplyDimension (ComplexUnitType.Dip, 75, dm);

            var conf = ViewConfiguration.Get (Context);
            touchSlop = conf.ScaledTouchSlop;
        }

        private void ForEachChild (Action<View> act)
        {
            var count = ChildCount;
            for (var i = 0; i < count; i++) {
                act (GetChildAt (i));
            }
        }

        private void ForEachChild (Action<int, View> act)
        {
            var count = ChildCount;
            for (var i = 0; i < count; i++) {
                act (i, GetChildAt (i));
            }
        }

        private void UpdateChildrenTranslationY ()
        {
            translateY = Math.Min (0, Math.Max (maxTranslateY, translateY));

            ForEachChild (child => {

                if (child.Top == 0 && lockFirstChild) {

                } else {

                    child.TranslationY = translateY;
                }
            });

            if (OnTranslateChanged != null) {
                OnTranslateChanged (this, EventArgs.Empty);
            }
        }

        public int ActiveChild
        {
            get { return activeChild; }
            set {
                if (value == activeChild) {
                    return;
                }

                SetActiveChild (value);

                CancelAnimations ();
                translateY = -GetChildAt (activeChild).Top;
                UpdateChildrenTranslationY ();
            }
        }

        public float TranslateY
        {
            get { return translateY; }
        }

        public bool Activated
        {
            get {
                return activated;
            } set {
                activated = value;
            }
        }

        public bool LockFirst
        {
            get { return lockFirstChild; }
            set {
                lockFirstChild = value;
            }
        }

        private void SetActiveChild (int value)
        {
            if (value >= ChildCount) {
                throw new ArgumentOutOfRangeException ("value", "Invalid child index.");
            }

            activeChild = value;

            if (ActiveChildChanged != null) {
                ActiveChildChanged (this, EventArgs.Empty);
            }
        }

        public event EventHandler ActiveChildChanged;

        public event EventHandler OnTranslateChanged;

        private void CancelAnimations()
        {
            // Stop any current animations
            if (scrollAnim != null) {
                scrollAnim.Cancel ();
                scrollAnim = null;
            }
        }

        public override bool OnInterceptTouchEvent (MotionEvent ev)
        {
            if (!activated) {
                return false;
            }
            switch (ev.Action) {
            case MotionEventActions.Down:
                touchX = ev.RawX;
                touchY = ev.RawY;
                CancelAnimations ();
                break;
            case MotionEventActions.Cancel:
            case MotionEventActions.Up:
                EndTouch ();
                break;
            case MotionEventActions.Move:
                float dx = Math.Abs (ev.RawX - touchX);
                float dy = Math.Abs (ev.RawY - touchY);

                if (dy > dx && dy > touchSlop) {
                    isDragging = true;
                    touchY = ev.RawY;
                    return true;
                }
                break;
            }


            return false;
        }

        public override bool OnTouchEvent (MotionEvent ev)
        {
            switch (ev.Action) {
            case MotionEventActions.Down:
                return true;
            case MotionEventActions.Move:
                if (isDragging) {
                    var dy = ev.RawY - touchY;
                    touchY = ev.RawY;

                    translateY += dy;
                    UpdateChildrenTranslationY ();
                } else {
                    float dx = Math.Abs (ev.RawX - touchX);
                    float dy = Math.Abs (ev.RawY - touchY);

                    if (dy > dx && dy > touchSlop) {
                        isDragging = true;
                        touchY = ev.RawY;
                        return true;
                    }
                }
                return true;
            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                EndTouch ();
                return true;
            }

            return base.OnTouchEvent (ev);
        }

        private void EndTouch()
        {
            isDragging = false;

            if (ChildCount == 0) {
                translateY = 0;
            } else {
                // Determine new ActiveChild
                var active = GetChildAt (ActiveChild);
                var offset = -translateY - active.Top;

                if (offset < 0 && ActiveChild > 0) {
                    // Scroll to one of the previous children
                    var previous = GetChildAt (ActiveChild - 1);
                    // Adjust the scroll threshold to account for children smaller than that
                    var threshold = Math.Min (previous.Height / 2f, scrollThreshold);

                    if (-offset > threshold) {
                        // Determine which child is the new active one
                        ForEachChild ((i, child) => {
                            var y = -translateY;
                            if (child.Top <= y && y <= child.Bottom) {
                                SetActiveChild (i);
                                active = child;
                            }
                        });
                    }

                } else if (offset > 0 && ActiveChild + 1 < ChildCount) {
                    // Scroll to one of the following children
                    // Adjust the scroll threshold to account for children smaller than that
                    var threshold = Math.Min (active.Height / 2f, scrollThreshold);

                    if (offset > threshold) {
                        if (-translateY <= active.Bottom) {
                            // Just the next child
                            SetActiveChild (ActiveChild + 1);
                            active = GetChildAt (ActiveChild);
                        } else {
                            // Scroll many children at once
                            ForEachChild ((i, child) => {
                                var y = -translateY;
                                if (child.Top <= y && y <= child.Bottom) {
                                    SetActiveChild (i);
                                    active = child;
                                }
                            });
                        }
                    }
                }

                // Scroll to active
                var anim = ValueAnimator.OfFloat (translateY, -active.Top);
                anim.Update += OnScrollAnimationUpdate;
                anim.SetDuration (250);
                anim.Start ();
                scrollAnim = anim;

                UpdateChildrenTranslationY ();
            }
        }

        private void OnScrollAnimationUpdate (object sender, ValueAnimator.AnimatorUpdateEventArgs e)
        {
            translateY = (float)e.Animation.AnimatedValue;
            UpdateChildrenTranslationY ();
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            var widthSize = MeasureSpec.GetSize (widthMeasureSpec);
            var heightSize = MeasureSpec.GetSize (heightMeasureSpec);

            ForEachChild (child => {
                MeasureChildWithMargins (child, widthMeasureSpec, 0, heightMeasureSpec, 0);
            });

            SetMeasuredDimension (widthSize, heightSize);
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            var currentTop = 0;

            ForEachChild (child => {
                var childWidth = child.MeasuredWidth;
                var childHeight = child.MeasuredHeight;

                var left = 0;
                var top = currentTop;
                var right = childWidth;
                var bottom = currentTop + childHeight;

                var lp = child.LayoutParameters as SnappyLayout.LayoutParams;
                if (lp != null) {
                    left += lp.LeftMargin;
                    right += lp.LeftMargin;
                    top += lp.TopMargin;
                    bottom += lp.TopMargin;
                }

                child.Layout (left, top, right, bottom);
                maxTranslateY = -top;
                currentTop += childHeight;
                if (lp != null) {
                    currentTop += lp.TopMargin + lp.BottomMargin;
                }
            });
        }

        public override ViewGroup.LayoutParams GenerateLayoutParams (IAttributeSet attrs)
        {
            return new SnappyLayout.LayoutParams (Context, attrs);
        }

        protected override ViewGroup.LayoutParams GenerateDefaultLayoutParams ()
        {
            return new SnappyLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.MatchParent);
        }

        protected override ViewGroup.LayoutParams GenerateLayoutParams (ViewGroup.LayoutParams p)
        {
            return new SnappyLayout.LayoutParams (p);
        }

        protected override bool CheckLayoutParams (ViewGroup.LayoutParams p)
        {
            return p is SnappyLayout.LayoutParams;
        }

        public class LayoutParams : ViewGroup.MarginLayoutParams
        {
            public LayoutParams (Context ctx, IAttributeSet attrs) : base (ctx, attrs)
            {
            }

            public LayoutParams (int width, int height) : base (width, height)
            {
            }

            public LayoutParams (ViewGroup.LayoutParams source) : base (source)
            {
            }
        }
    }
}
