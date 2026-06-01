using System;

namespace EOS.Serialization
{
    public interface IObjectSerializable
    {
        Type DataType { get; }
        object SerializeData();
        void DeserializeData(object data, IDeserializeContext ctx);
    }
}
