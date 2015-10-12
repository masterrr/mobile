using System;
using System.ComponentModel;
using PropertyChanged;

#if false
#define NotifyPropertyChanging
#endif
namespace Toggl.Phoebe
{
    [DoNotNotify]
    public class ObservableObject :
        #if NotifyPropertyChanging
        INotifyPropertyChanging,
        #endif
        INotifyPropertyChanged
    {
        #if NotifyPropertyChanging
        public event PropertyChangingEventHandler PropertyChanging;
        #endif
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanging (string property)
        {
            #if NotifyPropertyChanging
            var propertyChanging = PropertyChanging;
            if (propertyChanging != null) {
                propertyChanging (this, new PropertyChangingEventArgs (GetPropertyName (expr)));
            }
            #endif
        }

        /// <summary>
        /// Helper function to call PropertyChanging and PropertyChanged events before and after
        /// the property has been changed.
        /// </summary>
        /// <param name="propertyName">Property name, see example for suggested usage.</param>
        /// <param name="change">Delegate to do the actual property changing.</param>
        /// <example>
        /// // ...
        /// private string description;
        /// public static readonly string PropertyDescription = GetPropertyName ((m) => m.Description);
        ///
        /// public string Description {
        ///     get { return description; }
        ///     set {
        ///         if (description == value)
        ///             return;
        ///         ChangePropertyAndNotify (PropertyDescription, delegate {
        ///             description = value;
        ///         });
        ///     }
        /// }
        /// // ...
        /// </example>
        protected void ChangePropertyAndNotify (string propertyName, Action change)
        {
            OnPropertyChanging (propertyName);
            change ();
            OnPropertyChanged (propertyName);
        }

        protected virtual void OnPropertyChanged (string property)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged != null) {
                propertyChanged (this, new PropertyChangedEventArgs (property));
            }
        }
    }
}

