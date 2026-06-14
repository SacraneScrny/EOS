using System;
using EOS.Loader;
using EOS.Logging;
using EOS.Serialization;

namespace EOS.Objects
{
    /// <summary>Component that bridges an entity to an external view of type <typeparamref name="TView"/> (e.g. a Unity GameObject) through a registered <see cref="IIncarnationBinder{TView}"/>.</summary>
    public class Incarnation<TView> : EosObject, IIncarnation, IObjectSerializable
        where TView : class
    {
        /// <summary>The view id used to resolve and instantiate the view; round-trips through serialization.</summary>
        public string Id { get; private set; }
        /// <summary>The instantiated view instance, or null if no binder was found.</summary>
        public TView View { get; private set; }

        IIncarnationBinder<TView> _binder;

        /// <summary>Sets the view id; call before the component awakes so the view resolves correctly.</summary>
        public void Setup(string id) => Id = id;

        protected override void OnAwake()
        {
            _binder = IncarnationBridge.Resolve<TView>();
            if (_binder != null)
                View = _binder.Instantiate(Entity, Id);
            else
                EosLog.Debug($"Incarnation<{typeof(TView).Name}> '{Id}' on '{Entity.Name}' has no binder", nameof(Incarnation<TView>));
        }

        protected override void OnDispose()
        {
            if (View != null)
                _binder?.Destroy(Entity, View);
            View = null;
            _binder = null;
        }

        void IIncarnation.Sync() => _binder?.Sync(Entity, View);
        void IIncarnation.SyncFixed() => _binder?.SyncFixed(Entity, View);
        void IIncarnation.SyncLate() => _binder?.SyncLate(Entity, View);

        Type IObjectSerializable.DataType => typeof(string);
        object IObjectSerializable.SerializeData() => Id;
        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx) => Id = (string)data;
    }
}
