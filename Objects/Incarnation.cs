using System;
using EOS.Loader;
using EOS.Logging;
using EOS.Serialization;

namespace EOS.Objects
{
    public class Incarnation : EosObject, IObjectSerializable
    {
        public string Id { get; private set; }
        public object View { get; internal set; }

        public void Setup(string id) => Id = id;

        public T As<T>() where T : class => View as T;

        protected override void OnAwake()
        {
            if (IncarnationBridge.Binder != null)
                View = IncarnationBridge.Binder.Instantiate(Entity, Id);
            else
                EosLog.Debug($"Incarnation '{Id}' on '{Entity.Name}' has no binder", nameof(Incarnation));
        }

        protected override void OnDispose()
        {
            if (View != null)
                IncarnationBridge.Binder?.Destroy(Entity, View);
            View = null;
        }

        Type IObjectSerializable.DataType => typeof(string);
        object IObjectSerializable.SerializeData() => Id;
        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx) => Id = (string)data;
    }
}
