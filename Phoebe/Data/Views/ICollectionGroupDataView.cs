using System;
using System.Collections.Generic;

namespace Toggl.Phoebe.Data.Views
{
    public interface ICollectionGroupDataView<T, TT> : ICollectionDataView<T>
    {
        IList<TT> Groups { get; }
    }
}

