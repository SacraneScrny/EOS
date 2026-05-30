using System;
using System.Collections.Generic;
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

        static Action<float> BuildQuery(object instance, MethodInfo method)
        {
            var parameters = method.GetParameters();
            var componentTypes = new List<Type>();
            int entityParamIndex = -1;
            int deltaTimeParamIndex = -1;
            
            foreach (var p in parameters)
            {
                if (typeof(EosObject).IsAssignableFrom(p.ParameterType))
                {
                    componentTypes.Add(p.ParameterType);
                }
                else if (p.ParameterType == typeof(EosEntity))
                {
                    entityParamIndex = p.Position;
                }
                else if (p.ParameterType == typeof(float))
                {
                    deltaTimeParamIndex = p.Position;
                }
                else
                {
                    throw new Exception($"Unsupported parameter type {p.ParameterType.Name} in {method.Name}");
                }
            }
            
            var excludeTypes = new List<Type>();
            foreach (var attr in method.GetCustomAttributes<ExcludeAttribute>(true))
                excludeTypes.AddRange(attr.Types);
                
            var includeTypes = new List<Type>();
            foreach (var attr in method.GetCustomAttributes<IncludeAttribute>(true))
                includeTypes.AddRange(attr.Types);

            var includeStorages = includeTypes.Select(GetStorage).ToArray();
            var includeHasMethods = includeStorages.Select(s => GetMethod(s, HAS)).ToArray();
            var excludeStorages = excludeTypes.Select(GetStorage).ToArray();
            var excludeHasMethods = excludeStorages.Select(s => GetMethod(s, HAS)).ToArray();

            if (componentTypes.Count == 0)
            {
                if (entityParamIndex == -1)
                {
                    return (deltaTime) =>
                    {
                        var args = new object[parameters.Length];
                        if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                        method.Invoke(instance, args);
                    };
                }
                else
                {
                    return (deltaTime) =>
                    {
                        var args = new object[parameters.Length];
                        if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                        
                        foreach (var entity in EntitiesContainer.All())
                        {
                            bool valid = true;
                            
                            for (int j = 0; j < includeHasMethods.Length; j++)
                            {
                                if (!(bool)includeHasMethods[j].Invoke(includeStorages[j], new object[] { entity }))
                                {
                                    valid = false;
                                    break;
                                }
                            }
                            if (!valid) continue;
                            
                            for (int j = 0; j < excludeHasMethods.Length; j++)
                            {
                                if ((bool)excludeHasMethods[j].Invoke(excludeStorages[j], new object[] { entity }))
                                {
                                    valid = false;
                                    break;
                                }
                            }
                            if (!valid) continue;

                            args[entityParamIndex] = entity;
                            method.Invoke(instance, args);
                        }
                    };
                }
            }

            var storages = componentTypes.Select(GetStorage).ToArray();
            var getMethods = storages.Select(s => GetMethod(s, GET)).ToArray();
            var hasMethods = storages.Select(s => GetMethod(s, HAS)).ToArray();
            var getOwnerMethods = storages.Select(s => GetMethod(s, GET_OWNER)).ToArray();
            var countProps = storages.Select(s => GetProp(s, COUNT)).ToArray();

            return (deltaTime) =>
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
                    if (!valid) continue;
                    
                    for (int j = 0; j < includeHasMethods.Length; j++)
                    {
                        if (!(bool)includeHasMethods[j].Invoke(includeStorages[j], new object[] { entity }))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid) continue;
                    
                    for (int j = 0; j < excludeHasMethods.Length; j++)
                    {
                        if ((bool)excludeHasMethods[j].Invoke(excludeStorages[j], new object[] { entity }))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid) continue;
                    
                    var args = new object[parameters.Length];
                    int compIdx = 0;
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        if (typeof(EosObject).IsAssignableFrom(parameters[j].ParameterType))
                        {
                            args[j] = getMethods[compIdx].Invoke(storages[compIdx], new object[] { entity });
                            compIdx++;
                        }
                        else if (parameters[j].ParameterType == typeof(EosEntity))
                        {
                            args[j] = entity;
                        }
                        else if (parameters[j].ParameterType == typeof(float))
                        {
                            args[j] = deltaTime;
                        }
                    }
                    method.Invoke(instance, args);
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