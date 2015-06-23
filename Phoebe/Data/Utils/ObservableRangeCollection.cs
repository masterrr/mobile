using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Toggl.Phoebe.Data.Utils
{
    /// <summary>
    /// Represents a dynamic data collection that provides notifications when items get added, removed, or when the whole list is refreshed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        public void AddRange (IEnumerable<T> collection)
        {
            if (collection == null) { throw new ArgumentNullException ("collection"); }

            int startingIndex = Items.Count;
            foreach (var i in collection) { Items.Add (i); }
            OnCollectionChanged (new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Add, collection as List<T>, startingIndex));
        }

        public void RemoveRange (IEnumerable<T> collection)
        {
            if (collection == null) { throw new ArgumentNullException ("collection"); }

            var enumerable = collection.ToList ();
            foreach (var i in enumerable) { Items.Remove (i); }
            OnCollectionChanged (new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Remove, enumerable.ToList()));
        }

        public void Replace (T item)
        {
            ReplaceRange (new [] { item });
        }

        public void ReplaceRange (IEnumerable<T> collection)
        {
            if (collection == null) { throw new ArgumentNullException ("collection"); }

            Items.Clear();
            foreach (var i in collection) { Items.Add (i); }
            OnCollectionChanged (new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Reset));
        }

        public void RemoveFromIndex (int index)
        {
            for (int i = index; i < Items.Count; i++) {
                Items.RemoveAt (index);
            }
        }

        public ObservableRangeCollection() { }

        public ObservableRangeCollection (IEnumerable<T> collection) : base (collection) { }
    }

    public static class CollectionEventBuilder
    {

        public static NotifyCollectionChangedEventArgs GetEvent (NotifyCollectionChangedAction action, int newIndex, int oldIndex, object newObject)
        {
            NotifyCollectionChangedEventArgs args;
            switch (action) {
            case NotifyCollectionChangedAction.Move:
                args = new NotifyCollectionChangedEventArgs (action, newObject, newIndex, oldIndex);
                break;
            case NotifyCollectionChangedAction.Replace:
                args = new NotifyCollectionChangedEventArgs (action, newObject, new object (), newIndex);
                break;
            case NotifyCollectionChangedAction.Reset:
                args = new NotifyCollectionChangedEventArgs (action);
                break;
            default:
                args = new NotifyCollectionChangedEventArgs (action, newObject, newIndex);
                break;
            }
            return args;
        }

        public static NotifyCollectionChangedEventArgs GetRangeEvent (NotifyCollectionChangedAction action, int newIndex, int numberOfItems)
        {
            NotifyCollectionChangedEventArgs args;
            switch (action) {
            case NotifyCollectionChangedAction.Add:
                args = new NotifyCollectionChangedEventArgs (action, new List<object> (new string[numberOfItems]), newIndex);
                break;
            default:
                args = new NotifyCollectionChangedEventArgs (action, new Object(), newIndex);
                break;
            }
            return args;
        }
    }
}

