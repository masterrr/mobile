using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.Views;

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
    }
}

