using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Widget;
using Toggl.Phoebe.Data.Views;

namespace Toggl.Joey.UI.Views
{
    public class TogglTagsField : RelativeLayout
    {
        const int TagMaxLength = 30;

        public event EventHandler FullClick;

        public TogglTagsField (Context context) : base (context)
        {
            Initialize ();
        }

        public TogglTagsField (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            Initialize ();
        }

        public EditText EditText { get; private set; }

        public TextView TextView { get; private set; }

        protected virtual void OnClick (object obj, EventArgs args)
        {
            EventHandler handler = FullClick;
            if (handler != null) {
                handler (this, args);
            }
        }

        void Initialize()
        {
            LayoutInflater inflater = (LayoutInflater)Context.GetSystemService (Context.LayoutInflaterService);
            inflater.Inflate (Resource.Layout.TogglTagsField, this);

            EditText = FindViewById<EditText> (Resource.Id.TogglTagsFieldEditText);
            TextView = (TextView)FindViewById<TextView> (Resource.Id.TogglTagsFieldTitle);

            EditText.Touch += (object sender, View.TouchEventArgs e) => {
                e.Handled = false;
                TextView.Pressed = e.Event.Action != MotionEventActions.Up;
            };

            Click += OnClick;
            EditText.Click += OnClick;
        }

        public TogglTagsField ShowTitle (bool show)
        {
            TextView.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
            return this;
        }

        public virtual void RebindTags (TimeEntryTagsView tagsView)
        {
            List<String> tagList = new List<String> ();
            String t;

            if (tagsView == null || tagsView.IsLoading) {
                return;
            }

            if (tagsView.Count == 0) {
                EditText.Text = String.Empty;
                return;
            }

            foreach (String tagText in tagsView.Data) {
                t = tagText.Length > TagMaxLength ? tagText.Substring (0, TagMaxLength - 1).Trim () + "…" : tagText;
                if (tagText.Length > 0) {
                    tagList.Add (t);
                }
            }
            // The extra whitespace prevents the ImageSpans and the text they are over
            // to break at different positions, leaving zero linespacing on edge cases.
            var tags = new SpannableStringBuilder (String.Join (" ", tagList) + " ");

            int x = 0;
            foreach (String tagText in tagList) {
                tags.SetSpan (new ImageSpan (MakeTagChip (tagText)), x, x + tagText.Length, SpanTypes.ExclusiveExclusive);
                x = x + tagText.Length + 1;
            }
            EditText.SetText (tags, EditText.BufferType.Spannable);
        }

        private BitmapDrawable MakeTagChip (String tagText)
        {
            var Inflater = LayoutInflater.FromContext (Context);
            var tagChipView = (TextView)Inflater.Inflate (Resource.Layout.TagViewChip, this, false);
            tagChipView.Text = tagText.ToUpper ();
            int spec = MeasureSpec.MakeMeasureSpec (0, MeasureSpecMode.Unspecified);
            tagChipView.Measure (spec, spec);
            tagChipView.Layout (0, 0, tagChipView.MeasuredWidth, tagChipView.MeasuredHeight);

            var b = Bitmap.CreateBitmap (tagChipView.Width, tagChipView.Height, Bitmap.Config.Argb8888);

            var canvas = new Canvas (b);
            canvas.Translate (-tagChipView.ScrollX, -tagChipView.ScrollY);
            tagChipView.Draw (canvas);
            tagChipView.DrawingCacheEnabled = true;

            var cacheBmp = tagChipView.DrawingCache;
            var viewBmp = cacheBmp.Copy (Bitmap.Config.Argb8888, true);
            tagChipView.DestroyDrawingCache ();
            var bmpDrawable = new BitmapDrawable (Resources, viewBmp);
            bmpDrawable.SetBounds (0, 0, bmpDrawable.IntrinsicWidth, bmpDrawable.IntrinsicHeight);
            return bmpDrawable;
        }
    }
}

