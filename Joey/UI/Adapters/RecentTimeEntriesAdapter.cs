﻿using System;
using System.Linq;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Joey.UI.Adapters
{
    public class RecentTimeEntriesAdapter : BaseModelsViewAdapter<TimeEntryModel>
    {
        public RecentTimeEntriesAdapter () : base (new RecentTimeEntriesView ())
        {
        }

        protected override View GetModelView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView;
            if (view == null) {
                view = LayoutInflater.FromContext (parent.Context).Inflate (
                    Resource.Layout.RecentTimeEntryListItem, parent, false);
                view.Tag = new RecentTimeEntryListItemHolder (view);
            }
            var holder = (RecentTimeEntryListItemHolder)view.Tag;
            holder.Bind (GetModel (position));
            return view;
        }

        private class RecentTimeEntryListItemHolder : Java.Lang.Object
        {
            #pragma warning disable 0414
            private readonly object subscriptionModelChanged;
            #pragma warning restore 0414
            private TimeEntryModel model;

            public View ColorView { get; private set; }

            public TextView ProjectTextView { get; private set; }

            public TextView DateTextView { get; private set; }

            public TextView DescriptionTextView { get; private set; }

            public TextView TagsTextView { get; private set; }

            public TextView BillableTextView { get; private set; }

            public RecentTimeEntryListItemHolder (View root)
            {
                FindViews (root);

                // Cannot use model.OnPropertyChanged callback directly as it would most probably leak memory,
                // thus the global ModelChangedMessage is used instead.
                var bus = ServiceContainer.Resolve<MessageBus> ();
                subscriptionModelChanged = bus.Subscribe<ModelChangedMessage> (OnModelChanged);
            }

            private void FindViews (View root)
            {
                ColorView = root.FindViewById<View> (Resource.Id.ColorView);
                ProjectTextView = root.FindViewById<TextView> (Resource.Id.ProjectTextView);
                DateTextView = root.FindViewById<TextView> (Resource.Id.DateTextView);
                DescriptionTextView = root.FindViewById<TextView> (Resource.Id.DescriptionTextView);
                TagsTextView = root.FindViewById<TextView> (Resource.Id.TagsTextView);
                BillableTextView = root.FindViewById<TextView> (Resource.Id.BillableTextView);
            }

            private void OnModelChanged (ModelChangedMessage msg)
            {
                if (model == null)
                    return;

                if (model == msg.Model) {
                    if (msg.PropertyName == TimeEntryModel.PropertyStartTime
                        || msg.PropertyName == TimeEntryModel.PropertyIsBillable
                        || msg.PropertyName == TimeEntryModel.PropertyDescription
                        || msg.PropertyName == TimeEntryModel.PropertyProjectId
                        || msg.PropertyName == TimeEntryModel.PropertyTaskId)
                        Rebind ();
                } else if (model.ProjectId.HasValue && model.ProjectId == msg.Model.Id) {
                    if (msg.PropertyName == ProjectModel.PropertyName
                        || msg.PropertyName == ProjectModel.PropertyColor)
                        Rebind ();
                } else if (model.TaskId.HasValue && model.TaskId == msg.Model.Id) {
                    if (msg.PropertyName == TaskModel.PropertyName)
                        Rebind ();
                }
            }

            public void Bind (TimeEntryModel model)
            {
                this.model = model;
                Rebind ();
            }

            private void Rebind ()
            {
                var ctx = ProjectTextView.Context;

                if (model.Project == null) {
                    ProjectTextView.Text = ctx.GetString (Resource.String.RecentTimeEntryNoProject);
                } else if (model.Task != null) {
                    ProjectTextView.Text = String.Format ("{0} | {1}", model.Project.Name, model.Task.Name);
                } else {
                    ProjectTextView.Text = model.Project.Name;
                }

                var color = Android.Graphics.Color.Transparent;
                if (model.Project != null) {
                    color = Android.Graphics.Color.ParseColor (model.Project.HexColor);
                }
                ColorView.SetBackgroundColor (color);

                if (String.IsNullOrWhiteSpace (model.Description)) {
                    DescriptionTextView.Text = ctx.GetString (Resource.String.RecentTimeEntryNoDescription);
                } else {
                    DescriptionTextView.Text = model.Description;
                }

                // TODO: Use user defined date format
                DateTextView.Text = model.StartTime.ToShortDateString ();

                TagsTextView.Visibility = model.Tags.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
                BillableTextView.Visibility = model.IsBillable ? ViewStates.Visible : ViewStates.Gone;
            }
        }
    }
}
