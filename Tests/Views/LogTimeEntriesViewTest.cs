using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.Views;
using System;
using System.Collections.Generic;
using Toggl.Phoebe.Data.Utils;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Tests.Views
{
    [TestFixture]
    public class LogTimeEntriesViewTest : TimeEntriesCollectionTest
    {
        [Test]
        public void TestCollectionSimpleLoad ()
        {
            RunAsync (async delegate {
                var view = await LogTimeEntriesView.CreateAsync ();
                Assert.AreEqual (false, view.IsLoading);
                Assert.AreEqual (6, view.Data.Count ());
            });
        }

        [Test]
        public void TestCollectionReload ()
        {
            RunAsync (async delegate {
                var view = await LogTimeEntriesView.CreateAsync ();
                Assert.AreEqual (false, view.IsLoading);
                var initialEntryNumber = view.Data.Count ();

                await view.LoadMoreAsync ();
                Assert.AreEqual (false, view.IsLoading);
                Assert.IsTrue (view.Data.Count () > initialEntryNumber);
            });
        }

        [Test]
        public void TestDeleteItemWithUndo ()
        {
            RunAsync (async delegate {
                var view = await LogTimeEntriesView.CreateAsync ();
                Assert.AreEqual (false, view.IsLoading);

                await view.LoadMoreAsync ();
                Assert.AreEqual (false, view.IsLoading);

                // Test delete
                var itemIndex = GetRandomItemIndex (view.Data);
                var itemToRemove = view.Data.ElementAt (itemIndex);
                await view.RemoveItemWithUndoAsync (itemIndex);
                Assert.IsTrue (view.Data.Count (d => AreEquals (d, itemToRemove)) == 0);

                // Test restore
                await view.RestoreItemFromUndoAsync ();
                Assert.IsTrue (view.Data.Count (d => AreEquals (d, itemToRemove)) == 1);
                Assert.IsTrue (AreEquals (view.Data.ElementAt (itemIndex), itemToRemove));
            });
        }

        [Test]
        public void TestDeletePermanently ()
        {
            RunAsync (async delegate {
                var view = await LogTimeEntriesView.CreateAsync ();
                Assert.AreEqual (false, view.IsLoading);

                await view.LoadMoreAsync ();
                Assert.AreEqual (false, view.IsLoading);

                // Test delete
                var itemIndex = GetRandomItemIndex (view.Data);
                var itemToRemove = (TimeEntryHolder)view.Data.ElementAt (itemIndex);
                await view.RemoveItemWithUndoAsync (itemIndex);
                Assert.IsTrue (view.Data.Count (d => AreEquals (d, itemToRemove)) == 0);

                // Try to find the item after a time
                await Task.Delay ((TimeEntriesCollectionView.UndoSecondsInterval + 2) * 1000);
                Assert.IsTrue (view.Data.Count (d => AreEquals (d, itemToRemove)) == 0);

                var items = await DataStore.Table<TimeEntryData> ()
                            .QueryAsync (entry => entry.Id == itemToRemove.Id);
                Assert.IsFalse (items.Count == 0);
            });
        }


        protected int GetRandomItemIndex (IEnumerable<object> items)
        {
            var random = new Random ();
            var isTimeEntryHolder = false;
            var result = 1;
            var enumerable = items as IList<object> ?? items.ToList ();

            while (!isTimeEntryHolder) {
                var randomIndex = random.Next (1, enumerable.Count ());
                if (enumerable.ElementAt (randomIndex) is TimeEntryHolder) {
                    result = randomIndex;
                    isTimeEntryHolder = true;
                }
            }
            return result;
        }

        protected bool AreEquals (object holderA, object holderB)
        {
            var a = holderA as TimeEntryHolder;
            var b = holderB as TimeEntryHolder;

            if (a == null) {
                return false;
            }

            if (b == null) {
                return false;
            }

            return a.Id == b.Id;
        }


    }
}

