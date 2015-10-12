using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Threading.Tasks;
using PropertyChanged;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Logging;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Models
{
    [DoNotNotify]
    public abstract class Model<T> : IModel
        where T : CommonData, new()
    {
        private static readonly string Tag = String.Concat ("Model<", typeof (T).Name, ">");

        private static string GetPropertyName<U> (Expression<Func<Model<T>, U>> expr) {
            return expr.ToPropertyName ();
        }

        protected static readonly string PropertyId = GetPropertyName (m => m.Id);

        public static void MarkDirty (T data) {
            data.IsDirty = true;
            data.ModifiedAt = Time.UtcNow;
            data.RemoteRejected = false;
        }

        protected static Guid? GetOptionalId (IModel model) {
            if (!ModelExists (model)) {
                return null;
            }
            return model.Id;
        }

        protected static bool ModelExists (IModel model) {
            return model != null && model.Id != Guid.Empty;
        }

        private readonly Subscription<DataChangeMessage> subscriptionDataChange;
        private TaskCompletionSource<object> loadingTCS;
        private bool isLoaded;
        private T data;

        protected Model () : this (Guid.Empty, null) {
        }

        protected Model (Guid id) : this (id, null) {
        }

        protected Model (T data) : this (Guid.Empty, data) {
        }

        private Model (Guid id, T data) {
            var isLoaded = true;

            if (data != null) {
                // Model was initialized with data already loaded
            } else if (id != Guid.Empty) {
                // Check cache for model
                var dataCache = ServiceContainer.Resolve<DataCache> ();
                if (!dataCache.TryGetCached (id, out data)) {
                    // Not in cache, need to load data later
                    data = new T () { Id = id };
                    isLoaded = false;
                }
            } else {
                // Initialize a new instance for the model
                data = new T ();
            }

            this.data = data;
            this.isLoaded = isLoaded;

            // Listen for global data changes
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);

            InitializeRelations ();
        }

        private void OnDataChange (DataChangeMessage msg) {
            if (data.Matches (msg.Data)) {
                var updatedData = (T)msg.Data;
                var isDeleted = msg.Action == DataAction.Delete
                                || updatedData.DeletedAt != null;

                if (isDeleted) {
                    ResetIds ();
                } else {
                    Data = Duplicate (updatedData);
                }
            }
        }

        protected virtual void InitializeRelations () {
        }

        protected abstract T Duplicate (T data);

        public async Task LoadAsync () {
            if (loadingTCS != null) {
                await loadingTCS.Task;
                return;
            }
            if (isLoaded) {
                return;
            }

            loadingTCS = new TaskCompletionSource<object> ();
            var dataCache = ServiceContainer.Resolve<DataCache> ();
            var pk = Data.Id;

            var data = await dataCache.GetAsync<T> (pk);
            // When the data is not found in the database, leave the object as a temporary one, hoping that it will
            // be committed to the database soon enough and picked up by the change monitor
            if (data != null && data.DeletedAt == null) {
                Data = data;
            }

            isLoaded = true;
            loadingTCS.SetResult (null);
            loadingTCS = null;
        }

        public void Touch () {
            MutateData (delegate {
            });
        }

        public async Task SaveAsync () {
            OnBeforeSave ();

            var dataStore = ServiceContainer.Resolve<IDataStore> ();
            Data = await dataStore.PutAsync (Data);
        }

        protected abstract void OnBeforeSave ();

        public async Task DeleteAsync () {
            var dataStore = ServiceContainer.Resolve<IDataStore> ();

            if (data.RemoteId == null) {
                // We can safely delete the item as it has not been synchronized with the server yet
                await dataStore.DeleteAsync (data);
            } else {
                // Need to just mark this item as deleted so that it could be synced with the server
                var newData = Duplicate (data);
                newData.DeletedAt = Time.UtcNow;
                MarkDirty (newData);

                await dataStore.PutAsync (newData);
            }

            ResetIds ();
        }

        protected async void EnsureLoaded () {
            if (isLoaded || loadingTCS != null) {
                return;
            }

            try {
                await LoadAsync ().ConfigureAwait (false);
            } catch (Exception ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Warning (Tag, ex, "Failed to auto-load data.");
            }
        }

        protected void OnPropertyChanged (string propertyName) {
            var handler = PropertyChanged;
            if (handler != null) {
                handler (this, new PropertyChangedEventArgs (propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Guid Id {
            get { return data.Id; }
        }

        protected virtual void DetectChangedProperties (T oldData, T newData) {
            if (oldData.Id != newData.Id) {
                OnPropertyChanged (PropertyId);
            }
        }

        private void ResetIds () {
            var newData = Duplicate (Data);
            newData.Id = Guid.Empty;
            newData.RemoteId = null;
            newData.RemoteRejected = false;
            Data = newData;
        }

        protected void MutateData (Action<T> mutator) {
            var newData = Duplicate (Data);
            mutator (newData);
            MarkDirty (newData);
            Data = newData;
        }

        public T Data {
            get { return data; }
            set {
                var oldData = data ?? new T ();
                var newData = value ?? new T ();

                data = value;

                DetectChangedProperties (oldData, newData);
            }
        }
    }
}
