using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using CoreGraphics;
using System.Linq;
using CoreFoundation;
using Foundation;
using UIKit;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Views;

namespace Toggl.Ross.DataSources
{
    public abstract class CollectionDataViewSource<TRow, TSection> : UITableViewSource
    {

        private readonly UITableView tableView;
        private readonly ICollectionGroupDataView<TRow, TSection> dataView;
        private DataCache cache;


        public CollectionDataViewSource (UITableView tableView, ICollectionGroupDataView<TRow, TSection> dataView)
        {
            this.tableView = tableView;
            this.dataView = dataView;
            dataView.Reload ();
            dataView.CollectionChanged += OnCollectionChange;
        }

        public virtual void Attach ()
        {
            tableView.Source = this;
            UpdateFooter ();
            TryLoadMore ();
        }

        private UIActivityIndicatorView defaultFooterView;

        public bool IsEmpty
        {
            get { return !HasData && !dataView.IsLoading && !dataView.HasMore; }
        }

        protected virtual bool HasData
        {
            get {
                var sections = GetSections ();
                var sections_ = sections as IList<TSection> ?? sections.ToList ();
                if (sections_.Count == 1) {
                    return GetRows (sections_ [0]).Any ();
                }
                return sections_.Count > 0;
            }
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            var sectionsCount = GetSections ().Count();
            if (sectionsCount == 0) {
                return 0;
            }
            return GetRows (GetSections ().ElementAt ((int)section)).Count();
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            var sectionsCount = GetSections ().Count();
            return sectionsCount;
        }

        protected abstract IEnumerable<TSection> GetSections ();

        protected abstract IEnumerable<TRow> GetRows (TSection section);

        public UIView EmptyView { get; set; }


        protected virtual void UpdateFooter ()
        {
            if (dataView.HasMore || dataView.IsLoading) {
                if (defaultFooterView == null) {
                    defaultFooterView = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
                    defaultFooterView.Frame = new CGRect (0, 0, 50, 50);
                }
                tableView.TableFooterView = defaultFooterView;
                defaultFooterView.StartAnimating ();
            } else if (IsEmpty) {
                tableView.TableFooterView = EmptyView;
            } else {
                tableView.TableFooterView = null;
            }
        }

        public override void Scrolled (UIScrollView scrollView)
        {
            TryLoadMore ();
        }

        private void TryLoadMore ()
        {
            var currentOffset = tableView.ContentOffset.Y;
            var maximumOffset = tableView.ContentSize.Height - tableView.Frame.Height;

            if (maximumOffset - currentOffset <= 200.0) {
                // Automatically load more
                if (!dataView.IsLoading && dataView.HasMore) {
                    dataView.LoadMore ();
                }
            }
        }

        private void OnCollectionChange (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (Handle == IntPtr.Zero) {
                return;
            }

            tableView.ReloadData ();
        }

        protected virtual void Update () {
         //    tableView.ReloadData ();
        }

        public UITableView TableView
        {
            get { return tableView; }
        }
    }

}

