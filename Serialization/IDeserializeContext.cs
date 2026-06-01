using EOS.Core;
using EOS.Entities;

namespace EOS.Serialization
{
    public interface IDeserializeContext
    {
        EosEntity Resolve(int localId);
        World World { get; }
    }
}
