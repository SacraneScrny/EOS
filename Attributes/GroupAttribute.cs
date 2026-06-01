using System;

namespace EOS.Systems.Groups
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GroupAttribute : Attribute
    {
        public readonly Type Group;
        public GroupAttribute(Type group) => Group = group;
    }
}