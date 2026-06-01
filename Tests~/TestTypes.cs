using System;

using EOS.Objects;

namespace EOS.Tests
{
    public sealed class CompA : EosObject { public int Value; }
    public sealed class CompB : EosObject { public int Value; }
    public sealed class CompC : EosObject { public int Value; }

    public enum Color { Red, Green, Blue }

    [Flags]
    public enum Perm { None = 0, Read = 1, Write = 2, Exec = 4 }
}
