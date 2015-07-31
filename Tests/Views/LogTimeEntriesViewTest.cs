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
    }
}

