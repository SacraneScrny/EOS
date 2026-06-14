namespace EOS.Objects.Interfaces
{
    /// <summary>Empty marker interface that opts a component type into <see cref="EOS.Storage.Storage{T}"/> instance pooling; reset stale fields in <c>OnDispose</c> since reused instances re-run the full lifecycle.</summary>
    public interface IPoolableObject { }
}
