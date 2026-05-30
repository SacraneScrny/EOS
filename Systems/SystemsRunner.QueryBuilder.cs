using System;
using System.Linq;
using System.Reflection;
using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Systems
{
    internal static partial class SystemsRunner
    {
        const string EXECUTE_METHOD = "Execute";
        const string GET = "Get";
        const string HAS = "Has";
        const string GET_OWNER = "GetOwner";
        const string COUNT = "Count";

        static Action BuildQuery(object instance, MethodInfo method, Type[] paramTypes)
        {
            if (paramTypes.Length == 0)
            {
                return () => method.Invoke(instance, null);
            }

            var storages = paramTypes.Select(GetStorage).ToArray();
            
            var getMethods = storages.Select(s => GetMethod(s, GET)).ToArray();
            var hasMethods = storages.Select(s => GetMethod(s, HAS)).ToArray();
            var getOwnerMethods = storages.Select(s => GetMethod(s, GET_OWNER)).ToArray();
            var countProps = storages.Select(s => GetProp(s, COUNT)).ToArray();

            return () =>
            {
                int pivot = 0;
                int min = (int)countProps[0].GetValue(storages[0]);
                for (int j = 1; j < countProps.Length; j++)
                {
                    int c = (int)countProps[j].GetValue(storages[j]);
                    if (c < min) 
                    { 
                        min = c; 
                        pivot = j; 
                    }
                }

                for (int i = 0; i < min; i++)
                {
                    var entity = (EosEntity)getOwnerMethods[pivot].Invoke(storages[pivot], new object[] { i });
                    
                    bool valid = true;
                    for (int j = 0; j < hasMethods.Length; j++)
                    {
                        if (j == pivot) continue;
                        if (!(bool)hasMethods[j].Invoke(storages[j], new object[] { entity }))
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        var args = new object[paramTypes.Length];
                        for (int j = 0; j < paramTypes.Length; j++)
                        {
                            args[j] = getMethods[j].Invoke(storages[j], new object[] { entity });
                        }
                        
                        method.Invoke(instance, args);
                    }
                }
            };
        }

        static object GetStorage(Type componentType)
        {
            var method = typeof(StorageMap)
                .GetMethod(nameof(StorageMap.Get), BindingFlags.Static | BindingFlags.Public)
                ?? throw new Exception("StorageMap.Get<T>() not found");
            
            return method.MakeGenericMethod(componentType).Invoke(null, null);
        }

        static MethodInfo GetMethod(object obj, string name) =>
            obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new Exception($"{name} not found on {obj.GetType().Name}");

        static PropertyInfo GetProp(object obj, string name) =>
            obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new Exception($"{name} not found on {obj.GetType().Name}");
    }
}