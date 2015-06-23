using System;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Views;

namespace Toggl.Phoebe.Tests.Views
{
    public class CollectionDataViewTest : Test
    {
        protected DateTime MakeTime (int hour, int minute, int second = 0)
        {
            return Time.UtcNow.Date
                   .AddHours (hour)
                   .AddMinutes (minute)
                   .AddSeconds (second);
        }

        protected async Task<T> GetByRemoteId<T> (long remoteId)
        where T : CommonData, new()
        {
            var rows = await DataStore.Table<T> ().QueryAsync (r => r.RemoteId == remoteId);
            return rows.Single ();
        }

        protected async Task ChangeData<T> (long remoteId, Action<T> modifier)
        where T : CommonData, new()
        {
            var model = await GetByRemoteId<T> (remoteId);
            modifier (model);
            await DataStore.PutAsync (model);
        }

        protected async Task WaitForLoaded<T> (ICollectionDataView<T> view)
        {
            if (!view.IsLoading) {
                return;
            }

            var tcs = new TaskCompletionSource<object> ();
            EventHandler onUpdated = null;

            onUpdated = delegate {
                if (view.IsLoading) {
                    return;
                }
                view.OnIsLoadingChanged -= onUpdated;
                tcs.SetResult (null);
            };

            view.OnIsLoadingChanged += onUpdated;
            await tcs.Task.ConfigureAwait (false);
        }

        protected async Task WaitForUpdates<T> (ICollectionDataView<T> view, int count = 1)
        {
            var tcs = new TaskCompletionSource<object> ();
            EventHandler onCollectionChanged = null;

            onCollectionChanged = delegate {
                if (--count > 0) {
                    return;
                }
                view.CollectionChanged -= onCollectionChanged;
                tcs.TrySetResult (null);
            };

            view.CollectionChanged += onCollectionChanged;
            await tcs.Task.ConfigureAwait (false);
        }
    }
}

