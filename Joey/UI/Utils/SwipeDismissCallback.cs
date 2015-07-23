using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V7.Widget;
using Android.Support.V7.Widget.Helper;
using Android.Util;
using XPlatUtils;

namespace Toggl.Joey.UI.Utils
{
    public class SwipeDismissCallback : ItemTouchHelper.SimpleCallback
    {
        public interface IDismissListener
        {
            bool CanDismiss (RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder);

            void OnDismiss (RecyclerView.ViewHolder viewHolder);
        }

        private const float minThreshold = 20;
        private IDismissListener listener;
        private int leftBorderWidth;
        private Drawable backgroundShape;
        private Context ctx;
        private string deleteText;
        private Paint labelPaint;
        private Rect rect = new Rect();

        private Context Ctx
        {
            get {
                if (ctx == null) {
                    ctx = ServiceContainer.Resolve<Context> ();
                }
                return ctx;
            }
        }

        private string DeleteText
        {
            get {
                if (string.IsNullOrEmpty (deleteText)) {
                    deleteText = ctx.Resources.GetString (Resource.String.SwipeDeleteQuestion);
                }
                return deleteText;
            }
        }

        private Drawable BackgroundShape
        {
            get {
                if (backgroundShape == null) {
                    backgroundShape = Ctx.Resources.GetDrawable (Resource.Drawable.swipe_background_shape);
                }
                return backgroundShape;
            }
        }

        private Paint LabelPaint
        {
            get {
                if (labelPaint == null) {
                    var labelFontSize = TypedValue.ApplyDimension (ComplexUnitType.Sp, 14, ctx.Resources.DisplayMetrics);
                    labelPaint = new Paint {
                        Color = Color.White,
                        TextSize = labelFontSize,
                        AntiAlias = true,
                    };
                    labelPaint.GetTextBounds (DeleteText, 0, DeleteText.Length, rect);
                    leftBorderWidth = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 32, ctx.Resources.DisplayMetrics);
                }
                return labelPaint;
            }
        }

        public SwipeDismissCallback (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
        {
        }

        public SwipeDismissCallback (int p0, int p1, IDismissListener listener) : base (p0, p1)
        {
            this.listener = listener;
        }

        public override bool OnMove (RecyclerView p0, RecyclerView.ViewHolder p1, RecyclerView.ViewHolder p2)
        {
            return false;
        }

        public override int GetSwipeDirs (RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
        {
            if (listener.CanDismiss (recyclerView, viewHolder)) {
                return ItemTouchHelper.Right;
            }
            return 0;
        }

        public override void OnSwiped (RecyclerView.ViewHolder viewHolder, int direction)
        {
            listener.OnDismiss (viewHolder);
        }

        public override float GetSwipeThreshold (RecyclerView.ViewHolder p0)
        {
            return minThreshold;
        }

        public override void OnChildDraw (Canvas c, RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
        {
            var itemHeight = viewHolder.ItemView.Height;
            BackgroundShape.SetBounds (0, (int)viewHolder.ItemView.GetY (), c.Width, (int)viewHolder.ItemView.GetY () + itemHeight);
            BackgroundShape.Draw (c);
            c.DrawText (DeleteText, leftBorderWidth, viewHolder.ItemView.GetY () + itemHeight /2.0f + rect.Height ()/2.0f, LabelPaint);

            base.OnChildDraw (c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && backgroundShape != null) {
                backgroundShape.Dispose ();
                labelPaint.Dispose ();
            }

            base.Dispose (disposing);
        }
    }
}

