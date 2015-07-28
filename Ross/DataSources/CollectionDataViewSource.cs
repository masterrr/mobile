using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using CoreGraphics;
using Foundation;
using Toggl.Phoebe.Data.Views;
using UIKit;

namespace Toggl.Ross.DataSources
{
    public abstract class CollectionDataViewSource<TData, TSection, TRow> : UITableViewSource
    {
        private readonly UITableView tableView;
        private readonly ICollectionDataView<TData> dataView;
        private IEnumerable<TSection> sections = new List<TSection> ();

        protected CollectionDataViewSource (UITableView tableView, ICollectionDataView<TData> dataView)
        {
            this.tableView = tableView;
            this.dataView = dataView;
            dataView.Reload ();
            dataView.CollectionChanged += OnCollectionChange;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                dataView.CollectionChanged -= OnCollectionChange;
            }
            base.Dispose (disposing);
        }

        private bool enoughRowsCheck = false;

        public virtual void OnCollectionChange (object sender, NotifyCollectionChangedEventArgs e)
        {
            // Cache sections
            UpdateSectionList ();

            if (!enoughRowsCheck && dataView.Data.OfType<TRow> ().Count() < 10) {
                TryLoadMore ();
                enoughRowsCheck = true;
            }
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
                return dataView.Data.Any ();
            }
        }

        protected IEnumerable<TSection> Sections
        {
            get {
                return sections;
            }
        }

        protected IEnumerable<TRow> GetRowsFromSection (TSection section)
        {
            var rows = new List<TData> ();
            var startToCollect = false;

            foreach (var item in dataView.Data) {
                if (item is TSection) {
                    startToCollect = CompareDataSections (item, section);
                } else if (startToCollect) {
                    rows.Add (item);
                }
            }

            return rows.Cast <TRow> ();
        }

        protected int GetPlainSectionIndexOfItemIndex (int itemIndex)
        {
            int sectionIndex = -1;
            for (int i = 0; i <= itemIndex; i++) {
                var obj = dataView.Data.ElementAt (i);
                if (obj is TSection) {
                    sectionIndex = i;
                }
            }
            return sectionIndex;
        }

        protected NSIndexSet GetSectionIndexFromItemIndex (int itemIndex)
        {
            nint sectionIndex = -1;
            for (int i = 0; i <= itemIndex; i++) {
                var obj = dataView.Data.ElementAt (i);
                if (obj is TSection) {
                    sectionIndex ++;
                }
            }
            return NSIndexSet.FromIndex (sectionIndex);
        }

        protected NSIndexPath GetRowPathFromItemIndex (int itemIndex)
        {
            var rowIndex = 0;
            var sectionIndex = -1;
            int count = 0;

            foreach (var obj in dataView.Data) {
                if (obj is TSection) {
                    sectionIndex ++;
                    rowIndex = 0;
                } else {
                    if (count == itemIndex) {
                        return NSIndexPath.FromRowSection (rowIndex, sectionIndex);
                    }
                    rowIndex ++;
                }
                count++;
            }

            return NSIndexPath.FromRowSection (0, 0);
        }

        protected int GetItemIndexFromRowPath (NSIndexPath indexPath)
        {
            var rowIndex = int.MinValue;
            var sectionIndex = 0;
            var count = 0;

            foreach (var obj in dataView.Data) {
                if (obj is TSection) {
                    if (sectionIndex == indexPath.Section) {
                        rowIndex = 0;
                    }
                    sectionIndex ++;
                } else {
                    if (rowIndex == indexPath.Row) {
                        return count;
                    }
                    rowIndex ++;
                }
                count++;
            }
            return -1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            if (!Sections.Any ()) {
                return 0;
            }
            var rowsInSection = GetRowsFromSection (Sections.ElementAt ((int)section));
            return rowsInSection.Count ();
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return Sections.Count ();
        }

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
        protected abstract bool CompareDataSections (TData data, TSection section);

        private void UpdateSectionList ()
        {
            sections = dataView.Data.OfType<TSection> ();
        }

        protected virtual void Update ()
        {
            tableView.ReloadData ();
        }

        public UITableView TableView
        {
            get { return tableView; }
        }
    }

}

