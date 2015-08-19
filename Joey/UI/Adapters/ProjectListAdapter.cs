using System;
using System.Collections.Specialized;
using Android.Content;
using Android.Graphics;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;
using PopupArgs = Android.Widget.PopupMenu.MenuItemClickEventArgs;

namespace Toggl.Joey.UI.Adapters
{
    public class ProjectListAdapter : RecycledDataViewAdapter<object>
    {
        protected static readonly int ViewTypeContent = 1;
        protected static readonly int ViewTypeNoProject = ViewTypeContent;
        protected static readonly int ViewTypeClient = ViewTypeContent + 1;
        protected static readonly int ViewTypeProject = ViewTypeContent + 2;
        protected static readonly int ViewTypeTask = ViewTypeContent + 3;
        protected static readonly int ViewTypeLoaderPlaceholder = 0;

        public Action<object> HandleProjectSelection { get; set; }

        public event EventHandler<int> TasksProjectItemClick;

        private WorkspaceProjectsView collectionView;
        private RecyclerView owner;

        public ProjectListAdapter (RecyclerView owner, WorkspaceProjectsView collectionView) : base (owner, collectionView)
        {
            this.owner = owner;
            this.collectionView = collectionView;
        }

        protected override void CollectionChanged (NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) {
                NotifyDataSetChanged();
            }
        }

        protected override RecyclerView.ViewHolder GetViewHolder (ViewGroup parent, int viewType)
        {
            View view;
            RecyclerView.ViewHolder holder;

            if (viewType == ViewTypeClient) {
                view =  LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.ProjectListClientItem, parent, false);
                holder = new ClientListItemHolder (this, view);
            } else if (viewType == ViewTypeProject) {
                view =  LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.ProjectListProjectItem, parent, false);
                holder = new ProjectListItemHolder (this, view, HandleTasksProjectItemClick, HandleProjectItemClick);
            } else if (viewType == ViewTypeTask) {
                view =  LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.ProjectListTaskItem, parent, false);
                holder = new ProjectListTaskItemHolder (this, view);
            } else {
                view =  LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.ProjectListNoProjectItem, parent, false);
                holder = new NoProjectListItemHolder (this, view);
            }
            return holder;
        }

        private void HandleProjectItemClick (int position)
        {
            var proj = (WorkspaceProjectsView.Project)GetEntry (position);
            var handler = HandleProjectSelection;
            if (handler != null) {
                handler (proj);
            }
            return;
        }

        private void HandleTasksProjectItemClick (int position)
        {
            var proj = (WorkspaceProjectsView.Project)GetEntry (position);
            if (TasksProjectItemClick != null) {
                TasksProjectItemClick (this, position);
            }

            int collapsingCount;
            collectionView.ShowTaskForProject (proj, position, out collapsingCount);
            owner.ScrollToPosition (position - collapsingCount);
        }

        protected override void BindHolder (RecyclerView.ViewHolder holder, int position)
        {
            var viewType = GetItemViewType (position);

            if (viewType == ViewTypeTask) {
                var data = (TaskData)GetEntry (position);
                var projectHolder = (ProjectListTaskItemHolder) holder;
                projectHolder.Bind (data);
            } else if (viewType == ViewTypeClient) {
                var data = (WorkspaceProjectsView.Client)GetEntry (position);
                var clientHolder = (ClientListItemHolder)holder;
                clientHolder.Bind (data);
            } else {

                var data = (WorkspaceProjectsView.Project) GetEntry (position);
                if (viewType == ViewTypeProject) {
                    var projectHolder = (ProjectListItemHolder)holder;
                    projectHolder.Bind (data);
                    projectHolder.TasksButton.Selected |= null != collectionView.DisplayingTaskForProject && collectionView.DisplayingTaskForProject.Data.RemoteId == data.Data.RemoteId;
                } else {
                    var projectHolder = (NoProjectListItemHolder)holder;
                    projectHolder.Bind (data);
                }
            }
        }

        public override int GetItemViewType (int position)
        {
            if (position == DataView.Count) {
                return ViewTypeLoaderPlaceholder;
            }

            var obj = GetEntry (position);
            if (obj is WorkspaceProjectsView.Project) {
                var p = (WorkspaceProjectsView.Project)obj;

                return p.IsNoProject ? ViewTypeNoProject : ViewTypeProject;
            } else if (obj is WorkspaceProjectsView.Client) {
                return ViewTypeClient;
            }
            return ViewTypeTask;
        }

        #region View holders

        public class ProjectListItemHolder : RecycledBindableViewHolder<WorkspaceProjectsView.Project>, View.IOnClickListener
        {
            private ProjectModel model;

            public View ColorView { get; private set; }

            public TextView ProjectTextView { get; private set; }

            public TextView ClientTextView { get; private set; }

            public ImageButton TasksButton { get; private set; }

            public ImageView TasksImageView { get; private set; }

            private Action<int> clickListener;

            private bool displayClientText;

            public ProjectListItemHolder (ProjectListAdapter adapter, View root, Action<int> tasksClickListener, Action<int> clickListener) : base (root)
            {
                displayClientText = !adapter.collectionView.SortByClients;
                ColorView = root.FindViewById<View> (Resource.Id.ColorView);
                ProjectTextView = root.FindViewById<TextView> (Resource.Id.ProjectTextView).SetFont (Font.Roboto);
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.RobotoLight);
                TasksButton = root.FindViewById<ImageButton> (Resource.Id.TasksButton);
                this.clickListener = clickListener;

                TasksButton.Click += (sender, e) => tasksClickListener (AdapterPosition);
                root.SetOnClickListener (this);
            }

            public void OnClick (View v)
            {
                if (v == TasksButton) {
                    return;
                }
                if (clickListener != null) {
                    clickListener (AdapterPosition);
                }
            }

            protected async override void Rebind ()
            {
                // Protect against Java side being GCed
                if (Handle == IntPtr.Zero) {
                    return;
                }

                model = null;
                if (DataSource != null) {
                    model = (ProjectModel)DataSource.Data;
                }

                if (model == null) {
                    ColorView.SetBackgroundColor (ColorView.Resources.GetColor (Resource.Color.dark_gray_text));
                    ProjectTextView.SetText (Resource.String.ProjectsNoProject);
                    ClientTextView.Visibility = ViewStates.Gone;
                    TasksButton.Visibility = ViewStates.Gone;
                    return;
                }

                await model.LoadAsync ();

                var color = Color.ParseColor (model.GetHexColor ());
                ColorView.SetBackgroundColor (color);
                ProjectTextView.SetTextColor (color);
                ClientTextView.SetTextColor (color);

                ProjectTextView.Text = model.Name;
                if (model.Client != null && displayClientText) {
                    ClientTextView.Text = model.Client.Name;
                    ClientTextView.Visibility = ViewStates.Visible;
                } else {
                    ClientTextView.Visibility = ViewStates.Gone;
                }

                TasksButton.Visibility = DataSource.Tasks.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
                TasksButton.Selected = false;
            }
        }

        public class ProjectListTaskItemHolder : RecycledBindableViewHolder<TaskData>, View.IOnClickListener
        {
            private TaskData model;
            private readonly ProjectListAdapter adapter;

            public TextView TaskTextView { get; private set; }

            public ProjectListTaskItemHolder (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
            {
            }

            public ProjectListTaskItemHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                TaskTextView = root.FindViewById<TextView> (Resource.Id.TaskTextView).SetFont (Font.RobotoLight);

                root.SetOnClickListener (this);
            }

            protected async override void Rebind ()
            {
                // Protect against Java side being GCed
                if (Handle == IntPtr.Zero) {
                    return;
                }

                model = null;
                if (DataSource != null) {
                    model = DataSource;
                }

                if (model == null) {
                    return;
                }

                TaskTextView.Text = model.Name;
            }

            public void OnClick (View v)
            {
                adapter.HandleProjectSelection (DataSource);
            }
        }

        public class NoProjectListItemHolder : RecycledBindableViewHolder<WorkspaceProjectsView.Project>, View.IOnClickListener
        {
            private readonly ProjectListAdapter adapter;

            public TextView ProjectTextView { get; private set; }

            public NoProjectListItemHolder (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
            {
            }

            public NoProjectListItemHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                ProjectTextView = root.FindViewById<TextView> (Resource.Id.ProjectTextView).SetFont (Font.Roboto);
                root.SetOnClickListener (this);
            }

            protected override void Rebind ()
            {
                ProjectTextView.SetText (Resource.String.ProjectsNoProject);
            }

            public void OnClick (View v)
            {
                adapter.HandleProjectSelection (DataSource);
            }
        }

        public class ClientListItemHolder : RecycledBindableViewHolder<WorkspaceProjectsView.Client>
        {
            private WorkspaceProjectsView.Client model;
            private readonly ProjectListAdapter adapter;

            public TextView ClientTextView { get; private set; }

            public ClientListItemHolder (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
            {
            }

            public ClientListItemHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.Roboto);
            }

            protected async override void Rebind ()
            {
                // Protect against Java side being GCed
                if (Handle == IntPtr.Zero) {
                    return;
                }

                model = null;
                if (DataSource != null) {
                    model = DataSource;
                }

                if (model == null) {
                    return;
                }

                if (model.IsNoClient) {
                    ClientTextView.SetText (Resource.String.ProjectsNoClient);
                } else {
                    ClientTextView.Text = model.Data.Name;
                }

            }

        }
        #endregion
    }
}

