using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EOS.Core;
using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Systems
{
    public partial class SystemsRunner : WorldBound
    {
        const string EXECUTE_METHOD = "Execute";
        const string GET = "Get";
        const string HAS = "Has";
        const string GET_OWNER = "GetOwner";
        const string COUNT = "Count";

        Action<float> BuildQuery(object instance, MethodInfo method)
        {
            var parameters = method.GetParameters();
            int entityParamIndex = -1;
            int deltaTimeParamIndex = -1;

            var concreteParams = new List<(int position, Type type, bool onlyNew, bool optional)>();
            var interfaceParams = new List<(int position, Type type, bool onlyNew, bool optional)>();

            foreach (var p in parameters)
            {
                bool onlyNew = p.GetCustomAttribute<OnlyNewAttribute>() != null;
                bool optional = p.GetCustomAttribute<OptionalAttribute>() != null;

                if (p.ParameterType == typeof(EosEntity))
                    entityParamIndex = p.Position;
                else if (p.ParameterType == typeof(float))
                    deltaTimeParamIndex = p.Position;
                else if (p.ParameterType.IsInterface)
                    interfaceParams.Add((p.Position, p.ParameterType, onlyNew, optional));
                else if (typeof(EosObject).IsAssignableFrom(p.ParameterType))
                    concreteParams.Add((p.Position, p.ParameterType, onlyNew, optional));
                else
                    throw new Exception($"Unsupported parameter type {p.ParameterType.Name} in {method.Name}");
            }

            bool hasOnlyNew = concreteParams.Any(p => p.onlyNew) || interfaceParams.Any(p => p.onlyNew);

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
            var concreteOptional = concreteParams.Select(p => p.optional).ToArray();

            if (concreteParams.Count == 0 && interfaceParams.Count == 0)
                return BuildNoComponentQuery(instance, method, parameters,
                    entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods);

            if (hasOnlyNew)
                return BuildOnlyNewQuery(instance, method, parameters,
                    concreteParams, concreteStorages, concreteGetMethods, concreteHasMethods,
                    concreteIndexed, concreteOptional,
                    interfaceParams,
                    entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods);

            if (concreteParams.Count == 0)
                return BuildInterfaceOnlyQuery(instance, method, parameters,
                    interfaceParams, entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods);

            return BuildConcreteQuery(instance, method, parameters,
                concreteParams, concreteStorages, concreteGetMethods, concreteHasMethods,
                concreteGetOwners, concreteCountProps, concreteIndexed, concreteOptional,
                interfaceParams,
                entityParamIndex, deltaTimeParamIndex,
                includeStorages, includeHasMethods,
                excludeStorages, excludeHasMethods);
        }

        Action<float> BuildConcreteQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, bool onlyNew, bool optional)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods, MethodInfo[] concreteHasMethods,
            MethodInfo[] concreteGetOwners, PropertyInfo[] concreteCountProps,
            IIndexedStorage[] concreteIndexed, bool[] concreteOptional,
            List<(int position, Type type, bool onlyNew, bool optional)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            return (deltaTime) =>
            {
                int pivot = 0;
                int min = int.MaxValue;
                for (int j = 0; j < concreteParams.Count; j++)
                {
                    if (concreteOptional[j]) continue;
                    int c = (int)concreteCountProps[j].GetValue(concreteStorages[j]);
                    if (c < min) { min = c; pivot = j; }
                }
                if (min == int.MaxValue) min = 0;

                for (int i = 0; i < min; i++)
                {
                    if (!concreteIndexed[pivot].IsReady(i)) continue;

                    var entity = concreteIndexed[pivot].GetOwner(i);

                    if (!CheckFilters(entity,
                        concreteStorages, concreteHasMethods, pivot, concreteOptional,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods)) continue;

                    var ifaceComponents = ResolveInterfaceComponents(entity, interfaceParams);
                    if (ifaceComponents == null) continue;

                    method.Invoke(instance, BuildArgs(parameters, entity, deltaTime,
                        deltaTimeParamIndex, entityParamIndex,
                        concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, pivot, i,
                        interfaceParams, ifaceComponents));
                }
            };
        }

        Action<float> BuildOnlyNewQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, bool onlyNew, bool optional)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods, MethodInfo[] concreteHasMethods,
            IIndexedStorage[] concreteIndexed, bool[] concreteOptional,
            List<(int position, Type type, bool onlyNew, bool optional)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            int onlyNewPivot = concreteParams.FindIndex(p => p.onlyNew && !p.optional);

            return (deltaTime) =>
            {
                if (onlyNewPivot >= 0)
                {
                    var pivotIndexed = concreteIndexed[onlyNewPivot];
                    var recent = pivotIndexed.RecentlyAdded;

                    for (int r = 0; r < recent.Count; r++)
                    {
                        int i = recent[r];
                        if (!pivotIndexed.IsReady(i)) continue;

                        var entity = pivotIndexed.GetOwner(i);
                        bool valid = true;

                        for (int j = 0; j < concreteParams.Count; j++)
                        {
                            if (j == onlyNewPivot) continue;
                            if (concreteParams[j].onlyNew)
                            {
                                if (concreteParams[j].optional)
                                {
                                    // optional+onlyNew — если есть, должен быть новым
                                    var comp = concreteIndexed[j].TryGetObject(entity);
                                    if (comp != null)
                                    {
                                        int idx = GetIndexOf(concreteIndexed[j], entity);
                                        if (idx < 0 || !concreteIndexed[j].RecentlyAdded.Contains(idx))
                                        { valid = false; break; }
                                    }
                                }
                                else
                                {
                                    // обязательный onlyNew — должен быть и быть новым
                                    var comp = concreteIndexed[j].TryGetObject(entity);
                                    if (comp == null) { valid = false; break; }
                                    int idx = GetIndexOf(concreteIndexed[j], entity);
                                    if (idx < 0 || !concreteIndexed[j].RecentlyAdded.Contains(idx))
                                    { valid = false; break; }
                                }
                            }
                            else if (!concreteOptional[j])
                            {
                                // обычный обязательный
                                if (!(bool)concreteHasMethods[j].Invoke(concreteStorages[j], new object[] { entity }))
                                { valid = false; break; }
                            }
                        }
                        if (!valid) continue;

                        if (!CheckIncludeExclude(entity,
                            includeStorages, includeHasMethods,
                            excludeStorages, excludeHasMethods)) continue;

                        var ifaceComponents = ResolveInterfaceComponentsOnlyNew(entity, interfaceParams);
                        if (ifaceComponents == null) continue;

                        method.Invoke(instance, BuildArgs(parameters, entity, deltaTime,
                            deltaTimeParamIndex, entityParamIndex,
                            concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, onlyNewPivot, i,
                            interfaceParams, ifaceComponents));
                    }
                }
                else
                {
                    // pivot — первый обязательный [OnlyNew] интерфейс
                    var pivotIfaceType = interfaceParams.First(p => p.onlyNew && !p.optional).type;
                    var pivotStorages = World.ObjectsStorages.GetByInterface(pivotIfaceType);
                    if (pivotStorages == null) return;

                    foreach (var storage in pivotStorages)
                    {
                        var indexed = storage as IIndexedStorage;
                        if (indexed == null) continue;
                        var recent = indexed.RecentlyAdded;

                        for (int r = 0; r < recent.Count; r++)
                        {
                            int idx = recent[r];
                            if (!indexed.IsReady(idx)) continue;

                            var entity = indexed.GetOwner(idx);
                            bool valid = true;

                            for (int j = 0; j < concreteParams.Count; j++)
                            {
                                if (concreteOptional[j]) continue;
                                if (!(bool)concreteHasMethods[j].Invoke(concreteStorages[j], new object[] { entity }))
                                { valid = false; break; }
                            }
                            if (!valid) continue;

                            if (!CheckIncludeExclude(entity,
                                includeStorages, includeHasMethods,
                                excludeStorages, excludeHasMethods)) continue;

                            var ifaceComponents = ResolveInterfaceComponentsOnlyNew(entity, interfaceParams);
                            if (ifaceComponents == null) continue;

                            method.Invoke(instance, BuildArgs(parameters, entity, deltaTime,
                                deltaTimeParamIndex, entityParamIndex,
                                concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, -1, -1,
                                interfaceParams, ifaceComponents));
                        }
                    }
                }
            };
        }

        Action<float> BuildNoComponentQuery(
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

                foreach (var entity in World.Entities.All())
                {
                    if (!CheckIncludeExclude(entity,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods)) continue;

                    args[entityParamIndex] = entity;
                    method.Invoke(instance, args);
                }
            };
        }

        Action<float> BuildInterfaceOnlyQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, bool onlyNew, bool optional)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            var pivotIfaceType = interfaceParams.First(p => !p.optional).type;

            return (deltaTime) =>
            {
                var pivotStorages = World.ObjectsStorages.GetByInterface(pivotIfaceType);
                if (pivotStorages == null) return;

                foreach (var storage in pivotStorages)
                {
                    var indexed = storage as IIndexedStorage;
                    if (indexed == null) continue;

                    for (int i = 0; i < indexed.Count; i++)
                    {
                        if (!indexed.IsReady(i)) continue;

                        var entity = indexed.GetOwner(i);

                        if (!CheckIncludeExclude(entity,
                            includeStorages, includeHasMethods,
                            excludeStorages, excludeHasMethods)) continue;

                        var ifaceComponents = ResolveInterfaceComponents(entity, interfaceParams);
                        if (ifaceComponents == null) continue;

                        var args = new object[parameters.Length];
                        if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                        if (entityParamIndex != -1) args[entityParamIndex] = entity;

                        int ifaceIdx = 0;
                        for (int j = 0; j < parameters.Length; j++)
                            if (parameters[j].ParameterType.IsInterface)
                                args[j] = ifaceComponents[ifaceIdx++];

                        method.Invoke(instance, args);
                    }
                }
            };
        }

        // -------------------------------------------------------------------

        object[] ResolveInterfaceComponents(
            EosEntity entity,
            List<(int position, Type type, bool onlyNew, bool optional)> interfaceParams)
        {
            var result = new object[interfaceParams.Count];
            for (int j = 0; j < interfaceParams.Count; j++)
            {
                var component = FindInterfaceComponent(entity, interfaceParams[j].type);
                if (component == null && !interfaceParams[j].optional) return null;
                result[j] = component;
            }
            return result;
        }

        object[] ResolveInterfaceComponentsOnlyNew(
            EosEntity entity,
            List<(int position, Type type, bool onlyNew, bool optional)> interfaceParams)
        {
            var result = new object[interfaceParams.Count];
            for (int j = 0; j < interfaceParams.Count; j++)
            {
                var (position, type, onlyNew, optional) = interfaceParams[j];
                var storages = World.ObjectsStorages.GetByInterface(type);

                object found = null;
                if (storages != null)
                {
                    foreach (var storage in storages)
                    {
                        var indexed = storage as IIndexedStorage;
                        if (indexed == null) continue;
                        var component = indexed.TryGetObject(entity);
                        if (component == null) continue;

                        if (onlyNew)
                        {
                            int idx = GetIndexOf(indexed, entity);
                            if (idx < 0 || !indexed.RecentlyAdded.Contains(idx)) continue;
                        }

                        found = component;
                        break;
                    }
                }

                if (found == null && !optional) return null;
                result[j] = found;
            }
            return result;
        }

        object FindInterfaceComponent(EosEntity entity, Type interfaceType)
        {
            var storages = World.ObjectsStorages.GetByInterface(interfaceType);
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

        static int GetIndexOf(IIndexedStorage indexed, EosEntity entity)
        {
            for (int i = 0; i < indexed.Count; i++)
                if (indexed.GetOwner(i) == entity) return i;
            return -1;
        }

        object[] BuildArgs(
            ParameterInfo[] parameters, EosEntity entity, float deltaTime,
            int deltaTimeParamIndex, int entityParamIndex,
            List<(int position, Type type, bool onlyNew, bool optional)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods,
            IIndexedStorage[] concreteIndexed, int pivot, int pivotIndex,
            List<(int position, Type type, bool onlyNew, bool optional)> interfaceParams,
            object[] ifaceComponents)
        {
            var args = new object[parameters.Length];
            if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
            if (entityParamIndex != -1) args[entityParamIndex] = entity;

            for (int j = 0; j < concreteParams.Count; j++)
            {
                if (j == pivot && pivotIndex >= 0)
                    args[concreteParams[j].position] = concreteIndexed[pivot].GetAt(pivotIndex);
                else if (concreteParams[j].optional)
                    args[concreteParams[j].position] = concreteIndexed[j].TryGetObject(entity);
                else
                    args[concreteParams[j].position] = concreteGetMethods[j].Invoke(concreteStorages[j], new object[] { entity });
            }

            for (int j = 0; j < interfaceParams.Count; j++)
                args[interfaceParams[j].position] = ifaceComponents[j];

            return args;
        }

        // -------------------------------------------------------------------

        bool CheckFilters(
            EosEntity entity,
            object[] concreteStorages, MethodInfo[] concreteHasMethods, int pivot, bool[] concreteOptional,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            for (int j = 0; j < concreteHasMethods.Length; j++)
            {
                if (j == pivot) continue;
                if (concreteOptional[j]) continue;
                if (!(bool)concreteHasMethods[j].Invoke(concreteStorages[j], new object[] { entity }))
                    return false;
            }
            return CheckIncludeExclude(entity,
                includeStorages, includeHasMethods,
                excludeStorages, excludeHasMethods);
        }

        bool CheckIncludeExclude(
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

        object ResolveConcreteStorage(Type componentType)
        {
            var method = typeof(ObjectsStorageMap)
                .GetMethod(nameof(ObjectsStorageMap.Get), BindingFlags.Instance | BindingFlags.Public)
                ?? throw new Exception("ObjectsStorageMap.Get<T>() not found");
            return method.MakeGenericMethod(componentType).Invoke(World.ObjectsStorages, null);
        }

        MethodInfo GetMethod(object obj, string name) =>
            obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new Exception($"{name} not found on {obj.GetType().Name}");

        PropertyInfo GetProp(object obj, string name) =>
            obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new Exception($"{name} not found on {obj.GetType().Name}");
    }
}