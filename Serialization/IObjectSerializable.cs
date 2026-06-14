using System;

namespace EOS.Serialization
{
    /// <summary>Implemented by an <c>EosObject</c> component to opt into snapshot data capture/restore; component presence alone is restored even without it.</summary>
    public interface IObjectSerializable
    {
        /// <summary>The runtime type of the payload returned by <see cref="SerializeData"/>, recorded so the value can be resolved on restore.</summary>
        Type DataType { get; }
        /// <summary>Returns the plain payload to persist for this component instance.</summary>
        object SerializeData();
        /// <summary>Restores this component from a previously serialized payload, using <paramref name="ctx"/> to resolve cross-entity references.</summary>
        void DeserializeData(object data, IDeserializeContext ctx);
    }
}
