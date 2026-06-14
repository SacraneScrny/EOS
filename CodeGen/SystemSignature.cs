using System;
using System.Reflection;
using System.Text;

namespace EOS.CodeGen
{
    /// <summary>Builds a stable textual signature (name plus fully-qualified parameter types) for a system <c>Execute</c>/<c>EventExecute</c> method, used as the key shared by reflection and codegen paths.</summary>
    public static class SystemSignature
    {
        /// <summary>Returns the signature string <c>Name(Type,Type,...)</c> for <paramref name="method"/>.</summary>
        public static string Of(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append(method.Name).Append('(');
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendType(sb, parameters[i].ParameterType);
            }
            sb.Append(')');
            return sb.ToString();
        }

        static void AppendType(StringBuilder sb, Type type)
        {
            if (type.IsByRef)
            {
                AppendType(sb, type.GetElementType());
                return;
            }

            if (type.IsArray)
            {
                AppendType(sb, type.GetElementType());
                sb.Append('[').Append(new string(',', type.GetArrayRank() - 1)).Append(']');
                return;
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var name = definition.FullName ?? definition.Name;
                int tick = name.IndexOf('`');
                if (tick >= 0) name = name.Substring(0, tick);
                sb.Append(name).Append('<');
                var args = type.GetGenericArguments();
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendType(sb, args[i]);
                }
                sb.Append('>');
                return;
            }

            sb.Append(type.FullName ?? type.Name);
        }
    }
}
