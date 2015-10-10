namespace Toggl.Phoebe.Data.ViewModels
{
    public interface IVModel<T>
    {
        bool IsLoading { get; }

        void Dispose ();
    }
}

