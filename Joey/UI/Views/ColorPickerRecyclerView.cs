using System;
using System.Linq;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using Android.Support.V7.Widget;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.Models;

using Fragment = Android.Support.V4.App.Fragment;
using MeasureSpec = Android.Views.View.MeasureSpec;

namespace Toggl.Joey.UI.Views
{
    public class ColorPickerRecyclerView : FrameLayout
    {
        public ColorPickerRecyclerView (Context context) :
        base (context)
        {
            Initialize ();
        }

        public ColorPickerRecyclerView (Context context, IAttributeSet attrs) :
        base (context, attrs)
        {
            Initialize ();
        }

        public ColorPickerRecyclerView (Context context, IAttributeSet attrs, int defStyle) :
        base (context, attrs, defStyle)
        {
            Initialize ();
        }

        public RecyclerView Recycler { get; private set; }
        public ColorPickerAdapter Adapter { get; private set; }
        public RecyclerView.LayoutManager LayoutManager { get; private set; }

        public static int ColumnsCount = 5;
        public static int RowsCount = 5;

        private void Initialize ()
        {
            var inflater = (LayoutInflater)Context.GetSystemService (Context.LayoutInflaterService);
            inflater.Inflate (Resource.Layout.ColorPicker, this);

            Recycler = FindViewById<RecyclerView> (Resource.Id.ColorPickerRecyclerView);

            LayoutManager = new GridLayoutManager (Context, ColumnsCount);
            Recycler.SetLayoutManager (LayoutManager);

            Adapter = new ColorPickerAdapter ();
            Recycler.SetAdapter (Adapter);
        }

        public class ColorPickerAdapter : RecyclerView.Adapter
        {
            // SelectedColorChanged is needed for binding!
            public event EventHandler<int> SelectedColorChanged;
            public int SelectedColor { get; private set; }

            public ColorPickerAdapter()
            {
                SelectedColor = -1;
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
            {
                var v = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ColorPickerItem, parent, false);
                return new ColorPickerViewHolder (v, OnClick);
            }

            public override int GetItemViewType (int position)
            {
                return 1;
            }

            public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
            {
                var h = (ColorPickerViewHolder)holder;
                h.Button.SetBackgroundColor (Color.ParseColor (ProjectModel.HexColors.ElementAt (position)));
                h.Tick.Visibility = position == SelectedColor ? ViewStates.Visible : ViewStates.Invisible;
            }

            public override int ItemCount
            {
                get {
                    return ProjectModel.HexColors.Take (ColumnsCount*RowsCount).Count();
                }
            }

            private void OnClick (int position)
            {
                SelectedColor = position;
                NotifyDataSetChanged ();
                if (SelectedColorChanged != null) {
                    SelectedColorChanged (this, SelectedColor);
                }
            }

            public class ColorPickerViewHolder : RecyclerView.ViewHolder
            {
                public View Button { get; private set; }
                public ImageView Tick { get; private set; }

                public ColorPickerViewHolder (View v, Action<int> listener) : base (v)
                {
                    v.Click += (sender, e) => listener (AdapterPosition);
                    Tick = v.FindViewById<ImageView> (Resource.Id.ColorPickerViewTick);
                    Tick.BringToFront();
                    Button = v.FindViewById<View> (Resource.Id.ColorPickerViewButton);
                }
            }

        }
    }
}

