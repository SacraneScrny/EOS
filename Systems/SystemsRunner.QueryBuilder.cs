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
            int entityParamIndex = -1;
            int deltaTimeParamIndex = -1;

            var concreteParams = new List<(int position, Type type)>();
            var interfaceParams = new List<(int position, Type type)>();

            foreach (var p in parameters)
            {
                if (p.ParameterType == typeof(EosEntity))
                {
                    entityParamIndex = p.Position;
                }
                else if (p.ParameterType == typeof(float))
                {
                    deltaTimeParamIndex = p.Position;
                }
                else if (p.ParameterType.IsInterface)
                {
                    interfaceParams.Add((p.Position, p.ParameterType));
                }
                else if (typeof(EosObject).IsAssignableFrom(p.ParameterType))
                {
                    concreteParams.Add((p.Position, p.ParameterType));
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

            var includeStorages = includeTypes.Select(ResolveConcreteStorage).ToArray();
            var excludeStorages = excludeTypes.Select(ResolveConcreteStorage).ToArray();
            var includeHasMethods = includeStorages.Select(s => GetMethod(s, HAS)).ToArray();
            var excludeHasMethods = excludeStorages.Select(s => GetMethod(s, HAS)).ToArray();

            var concreteStorages = concreteParams.Select(p => ResolveConcreteStorage(p.type)).ToArray();
            var concreteGetMethods = concreteStorages.Select(s => GetMethod(s, GET)).ToArray();
            var concreteHasMethods = concreteStorages.Select(s => GetMethod(s, HAS)).ToArray();
            var concreteGetOwners = concreteStorages.Select(s => GetMethod(s, GET_OWNER)).ToArray();
            var concreteCountProps = concreteStorages.Select(s => GetProp(s, COUNT)).ToArray();
            var concreteIndexed = concreteStorages.Select(s => s as IIndexedStorage).ToArray();

            if (concreteParams.Count == 0)
            {
                if (interfaceParams.Count == 0)
                    return BuildNoComponentQuery(instance, method, parameters,
                        entityParamIndex, deltaTimeParamIndex,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods);

                return BuildInterfaceOnlyQuery(instance, method, parameters,
                    interfaceParams, entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods);
            }

            return (deltaTime) =>
            {
                int pivot = 0;
                int min = (int)concreteCountProps[0].GetValue(concreteStorages[0]);
                for (int j = 1; j < concreteCountProps.Length; j++)
                {
                    int c = (int)concreteCountProps[j].GetValue(concreteStorages[j]);
                    if (c < min) { min = c; pivot = j; }
                }

                for (int i = 0; i < min; i++)
                {
                    var entity = (EosEntity)concreteGetOwners[pivot].Invoke(concreteStorages[pivot], new object[] { i });

                    if (!CheckFilters(entity,
                        concreteStorages, concreteHasMethods, pivot,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods)) continue;

                    var ifaceComponents = new object[interfaceParams.Count];
                    bool valid = true;
                    for (int j = 0; j < interfaceParams.Count; j++)
                    {
                        var component = FindInterfaceComponent(entity, interfaceParams[j].type);
                        if (component == null) { valid = false; break; }
                        ifaceComponents[j] = component;
                    }
                    if (!valid) continue;

                    var args = new object[parameters.Length];
                    if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                    if (entityParamIndex != -1) args[entityParamIndex] = entity;

                    int compIdx = 0;
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        var pType = parameters[j].ParameterType;
                        if (typeof(EosObject).IsAssignableFrom(pType))
                        {
                            if (compIdx == pivot)
                                args[j] = concreteIndexed[pivot].GetAt(i);
                            else
                                args[j] = concreteGetMethods[compIdx].Invoke(concreteStorages[compIdx], new object[] { entity });
                            compIdx++;
                        }
                    }

                    int ifaceIdx = 0;
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        if (parameters[j].ParameterType.IsInterface)
                            args[j] = ifaceComponents[ifaceIdx++];
                    }

                    method.Invoke(instance, args);
                }
            };
        }

        static Action<float> BuildNoComponentQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
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

            return (deltaTime) =>
            {
                var args = new object[parameters.Length];
                if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;

                foreach (var entity in EntitiesContainer.All())
                {
                    if (!CheckIncludeExclude(entity,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods)) continue;

                    args[entityParamIndex] = entity;
                    method.Invoke(instance, args);
                }
            };
        }

        static Action<float> BuildInterfaceOnlyQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            var pivotIfaceType = interfaceParams[0].type;

            return (deltaTime) =>
            {
                var pivotStorages = StorageMap.GetByInterface(pivotIfaceType);
                if (pivotStorages == null) return;

                foreach (var storage in pivotStorages)
                {
                    var indexed = storage as IIndexedStorage;
                    if (indexed == null) continue;
                    int count = indexed.Count;

                    for (int i = 0; i < count; i++)
                    {
                        var entity = indexed.GetOwner(i);

                        if (!CheckIncludeExclude(entity,
                            includeStorages, includeHasMethods,
                            excludeStorages, excludeHasMethods)) continue;

                        var ifaceComponents = new object[interfaceParams.Count];
                        bool valid = true;

                        ifaceComponents[0] = indexed.GetAt(i);

                        for (int j = 1; j < interfaceParams.Count; j++)
                        {
                            var component = FindInterfaceComponent(entity, interfaceParams[j].type);
                            if (component == null) { valid = false; break; }
                            ifaceComponents[j] = component;
                        }
                        if (!valid) continue;

                        var args = new object[parameters.Length];
                        if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                        if (entityParamIndex != -1) args[entityParamIndex] = entity;

                        int ifaceIdx = 0;
                        for (int j = 0; j < parameters.Length; j++)
                        {
                            if (parameters[j].ParameterType.IsInterface)
                                args[j] = ifaceComponents[ifaceIdx++];
                        }

                        method.Invoke(instance, args);
                    }
                }
            };
        }

        static object FindInterfaceComponent(EosEntity entity, Type interfaceType)
        {
            var storages = StorageMap.GetByInterface(interfaceType);
            if (storages == null) return null;

            foreach (var storage in storages)
            {
                var indexed = storage as IIndexedStorage;
                if (indexed == null) continue;
                var component = indexed.TryGetObject(entity);
                if (component != null) return component;
            }
            return null;
        }

        static bool CheckFilters(
            EosEntity entity,
            object[] concreteStorages, MethodInfo[] concreteHasMethods, int pivot,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            for (int j = 0; j < concreteHasMethods.Length; j++)
            {
                if (j == pivot) continue;
                if (!(bool)concreteHasMethods[j].Invoke(concreteStorages[j], new object[] { entity }))
                    return false;
            }
            return CheckIncludeExclude(entity,
                includeStorages, includeHasMethods,
                excludeStorages, excludeHasMethods);
        }

        static bool CheckIncludeExclude(
            EosEntity entity,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            for (int j = 0; j < includeHasMethods.Length; j++)
                if (!(bool)includeHasMethods[j].Invoke(includeStorages[j], new object[] { entity }))
                    return false;

            for (int j = 0; j < excludeHasMethods.Length; j++)
                if ((bool)excludeHasMethods[j].Invoke(excludeStorages[j], new object[] { entity }))
                    return false;

            return true;
        }

        static object ResolveConcreteStorage(Type componentType)
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