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

        // Builds the query body for one Execute method. The body is a pure iteration of the
        // form (deltaTime, cursor) => run-once-per-matching-entity. It carries NO scheduling
        // policy: the runner decides when to call it (IsUpdate) and what cursor to pass.
        // For non-reactive queries the cursor is ignored; for reactive ([New]/[Bumped]) ones
        // only entities whose channel version is strictly newer than the cursor are visited.
        (Action<float, ulong> body, bool reactive) BuildQuery(object instance, MethodInfo method)
        {
            var parameters = method.GetParameters();
            int entityParamIndex = -1;
            int deltaTimeParamIndex = -1;

            var concreteParams = new List<(int position, Type type, Channel channel, bool optional)>();
            var interfaceParams = new List<(int position, Type type, Channel channel, bool optional, bool each)>();

            foreach (var p in parameters)
            {
                Channel channel = Channel.None;
                if (p.GetCustomAttribute<NewAttribute>() != null) channel = Channel.New;
                else if (p.GetCustomAttribute<BumpedAttribute>() != null) channel = Channel.Bumped;
                bool optional = p.GetCustomAttribute<OptionalAttribute>() != null;
                bool each = p.GetCustomAttribute<EachAttribute>() != null;

                if (p.ParameterType == typeof(EosEntity))
                    entityParamIndex = p.Position;
                else if (p.ParameterType == typeof(float))
                    deltaTimeParamIndex = p.Position;
                else if (p.ParameterType.IsInterface)
                    interfaceParams.Add((p.Position, p.ParameterType, channel, optional, each));
                else if (typeof(EosObject).IsAssignableFrom(p.ParameterType))
                    concreteParams.Add((p.Position, p.ParameterType, channel, optional));
                else
                    throw new Exception($"Unsupported parameter type {p.ParameterType.Name} in {method.Name}");
            }

            bool isReactive = concreteParams.Any(p => p.channel != Channel.None)
                              || interfaceParams.Any(p => p.channel != Channel.None);

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

            if (isReactive)
                return (BuildReactiveQuery(instance, method, parameters,
                    concreteParams, concreteStorages, concreteGetMethods, concreteHasMethods,
                    concreteIndexed, concreteOptional,
                    interfaceParams,
                    entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods), true);

            if (concreteParams.Count == 0 && interfaceParams.Count == 0)
                return (BuildNoComponentQuery(instance, method, parameters,
                    entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods), false);

            if (concreteParams.Count == 0)
                return (BuildInterfaceOnlyQuery(instance, method, parameters,
                    interfaceParams, entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods), false);

            return (BuildConcreteQuery(instance, method, parameters,
                concreteParams, concreteStorages, concreteGetMethods, concreteHasMethods,
                concreteGetOwners, concreteCountProps, concreteIndexed, concreteOptional,
                interfaceParams,
                entityParamIndex, deltaTimeParamIndex,
                includeStorages, includeHasMethods,
                excludeStorages, excludeHasMethods), false);
        }

        Action<float, ulong> BuildConcreteQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods, MethodInfo[] concreteHasMethods,
            MethodInfo[] concreteGetOwners, PropertyInfo[] concreteCountProps,
            IIndexedStorage[] concreteIndexed, bool[] concreteOptional,
            List<(int position, Type type, Channel channel, bool optional, bool each)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            return (deltaTime, _) =>
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

                    var combos = ResolveInterfaceCombinations(entity, interfaceParams);
                    if (combos == null) continue;

                    for (int c = 0; c < combos.Count; c++)
                        method.Invoke(instance, BuildArgs(parameters, entity, deltaTime,
                            deltaTimeParamIndex, entityParamIndex,
                            concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, pivot, i,
                            interfaceParams, combos[c]));
                }
            };
        }

        // Reactive query body: visit only entities whose driver channel version is newer than
        // `cursor`. No gating, no cursor bookkeeping here — that is entirely the runner's job.
        Action<float, ulong> BuildReactiveQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods, MethodInfo[] concreteHasMethods,
            IIndexedStorage[] concreteIndexed, bool[] concreteOptional,
            List<(int position, Type type, Channel channel, bool optional, bool each)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            int driverConcrete = concreteParams.FindIndex(p => p.channel != Channel.None && !p.optional);

            if (driverConcrete >= 0)
            {
                var driver = concreteIndexed[driverConcrete];
                Channel driverChannel = concreteParams[driverConcrete].channel;

                return (deltaTime, cursor) =>
                {
                    if (ChannelMax(driver, driverChannel) <= cursor) return;

                    int count = driver.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (!driver.IsReady(i)) continue;
                        if (ChannelVersionAt(driver, i, driverChannel) <= cursor) continue;

                        var entity = driver.GetOwner(i);

                        if (!ReactiveConcreteMatch(entity, concreteParams, concreteIndexed,
                            concreteHasMethods, concreteStorages, driverConcrete, cursor)) continue;

                        if (!CheckIncludeExclude(entity,
                            includeStorages, includeHasMethods,
                            excludeStorages, excludeHasMethods)) continue;

                        var combos = ResolveInterfaceCombinationsReactive(entity, interfaceParams, cursor);
                        if (combos == null) continue;

                        for (int c = 0; c < combos.Count; c++)
                            method.Invoke(instance, BuildArgs(parameters, entity, deltaTime,
                                deltaTimeParamIndex, entityParamIndex,
                                concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, driverConcrete, i,
                                interfaceParams, combos[c]));
                    }
                };
            }
            else
            {
                var driverIface = interfaceParams.First(p => p.channel != Channel.None && !p.optional);
                Channel driverChannel = driverIface.channel;
                Type driverType = driverIface.type;

                return (deltaTime, cursor) =>
                {
                    var storages = World.ObjectsStorages.GetByInterface(driverType);
                    if (storages == null) return;

                    // The driver interface may have several implementations on one entity; dedup so
                    // an entity is considered once (the combination resolver below decides how many
                    // times Execute actually runs, based on [Each]).
                    var seen = new HashSet<int>();

                    foreach (var storage in storages)
                    {
                        var indexed = storage as IIndexedStorage;
                        if (indexed == null) continue;
                        if (ChannelMax(indexed, driverChannel) <= cursor) continue;

                        int count = indexed.Count;
                        for (int i = 0; i < count; i++)
                        {
                            if (!indexed.IsReady(i)) continue;
                            if (ChannelVersionAt(indexed, i, driverChannel) <= cursor) continue;

                            var entity = indexed.GetOwner(i);
                            if (!seen.Add(entity.Id)) continue;

                            if (!ReactiveConcreteMatch(entity, concreteParams, concreteIndexed,
                                concreteHasMethods, concreteStorages, -1, cursor)) continue;

                            if (!CheckIncludeExclude(entity,
                                includeStorages, includeHasMethods,
                                excludeStorages, excludeHasMethods)) continue;

                            var combos = ResolveInterfaceCombinationsReactive(entity, interfaceParams, cursor);
                            if (combos == null) continue;

                            for (int c = 0; c < combos.Count; c++)
                                method.Invoke(instance, BuildArgs(parameters, entity, deltaTime,
                                    deltaTimeParamIndex, entityParamIndex,
                                    concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, -1, -1,
                                    interfaceParams, combos[c]));
                        }
                    }
                };
            }
        }

        Action<float, ulong> BuildNoComponentQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            if (entityParamIndex == -1)
            {
                return (deltaTime, _) =>
                {
                    var args = new object[parameters.Length];
                    if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                    method.Invoke(instance, args);
                };
            }

            return (deltaTime, _) =>
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

        Action<float, ulong> BuildInterfaceOnlyQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional, bool each)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods)
        {
            var pivotIfaceType = interfaceParams.First(p => !p.optional).type;

            return (deltaTime, _) =>
            {
                var pivotStorages = World.ObjectsStorages.GetByInterface(pivotIfaceType);
                if (pivotStorages == null) return;

                // An entity may show up in several implementation storages of the pivot interface;
                // dedup so it is visited once and the combination resolver decides the run count.
                var seen = new HashSet<int>();

                foreach (var storage in pivotStorages)
                {
                    var indexed = storage as IIndexedStorage;
                    if (indexed == null) continue;

                    for (int i = 0; i < indexed.Count; i++)
                    {
                        if (!indexed.IsReady(i)) continue;

                        var entity = indexed.GetOwner(i);
                        if (!seen.Add(entity.Id)) continue;

                        if (!CheckIncludeExclude(entity,
                            includeStorages, includeHasMethods,
                            excludeStorages, excludeHasMethods)) continue;

                        var combos = ResolveInterfaceCombinations(entity, interfaceParams);
                        if (combos == null) continue;

                        for (int c = 0; c < combos.Count; c++)
                        {
                            var combo = combos[c];
                            var args = new object[parameters.Length];
                            if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                            if (entityParamIndex != -1) args[entityParamIndex] = entity;

                            for (int j = 0; j < interfaceParams.Count; j++)
                                args[interfaceParams[j].position] = combo[j];

                            method.Invoke(instance, args);
                        }
                    }
                }
            };
        }

        // -------------------------------------------------------------------

        static ulong ChannelVersionAt(IIndexedStorage storage, int index, Channel channel)
            => channel == Channel.Bumped ? storage.MarkVersionAt(index) : storage.AddVersionAt(index);

        static ulong ChannelMax(IIndexedStorage storage, Channel channel)
            => channel == Channel.Bumped ? storage.MaxMarkVersion : storage.MaxAddVersion;

        // Validates concrete parameters other than the driver:
        //  - reactive (New/Bumped): must be present AND have a version newer than the cursor
        //  - required non-reactive: must be present
        //  - optional non-reactive: no constraint
        //  - optional reactive: absent is fine, but present-yet-stale fails the match
        bool ReactiveConcreteMatch(
            EosEntity entity,
            List<(int position, Type type, Channel channel, bool optional)> concreteParams,
            IIndexedStorage[] concreteIndexed, MethodInfo[] concreteHasMethods, object[] concreteStorages,
            int driverIndex, ulong cursor)
        {
            for (int j = 0; j < concreteParams.Count; j++)
            {
                if (j == driverIndex) continue;
                var channel = concreteParams[j].channel;
                bool optional = concreteParams[j].optional;

                if (channel != Channel.None)
                {
                    int idx = concreteIndexed[j].IndexOf(entity);
                    if (idx < 0)
                    {
                        if (optional) continue;
                        return false;
                    }
                    if (ChannelVersionAt(concreteIndexed[j], idx, channel) <= cursor)
                        return false;
                }
                else if (!optional)
                {
                    if (!(bool)concreteHasMethods[j].Invoke(concreteStorages[j], new object[] { entity }))
                        return false;
                }
            }
            return true;
        }

        // Shared single empty combination for queries with no interface parameters: one run, no
        // interface arguments. Read-only — never mutated by callers.
        static readonly List<object[]> _emptyCombo = new() { Array.Empty<object>() };

        // Resolves the set of argument combinations for the interface parameters of one entity.
        // Plain interface params contribute their single first-found implementation; [Each] params
        // contribute every implementation present. The result is the cartesian product of those, so
        // Execute runs once per combination. Returns null when a required parameter has no match.
        List<object[]> ResolveInterfaceCombinations(
            EosEntity entity,
            List<(int position, Type type, Channel channel, bool optional, bool each)> interfaceParams)
        {
            int n = interfaceParams.Count;
            if (n == 0) return _emptyCombo;

            var perParam = new List<object>[n];
            for (int j = 0; j < n; j++)
            {
                var p = interfaceParams[j];
                if (p.each)
                {
                    var list = new List<object>();
                    CollectInterfaceComponents(entity, p.type, list);
                    if (list.Count == 0)
                    {
                        if (!p.optional) return null;
                        list.Add(null);
                    }
                    perParam[j] = list;
                }
                else
                {
                    var component = FindInterfaceComponent(entity, p.type);
                    if (component == null && !p.optional) return null;
                    perParam[j] = new List<object>(1) { component };
                }
            }
            return CartesianProduct(perParam);
        }

        // Reactive counterpart: implementations are filtered by the parameter's channel version so
        // only ones strictly newer than the cursor qualify.
        List<object[]> ResolveInterfaceCombinationsReactive(
            EosEntity entity,
            List<(int position, Type type, Channel channel, bool optional, bool each)> interfaceParams,
            ulong cursor)
        {
            int n = interfaceParams.Count;
            if (n == 0) return _emptyCombo;

            var perParam = new List<object>[n];
            for (int j = 0; j < n; j++)
            {
                var p = interfaceParams[j];
                if (p.each)
                {
                    var list = new List<object>();
                    CollectInterfaceComponentsReactive(entity, p.type, p.channel, cursor, list);
                    if (list.Count == 0)
                    {
                        if (!p.optional) return null;
                        list.Add(null);
                    }
                    perParam[j] = list;
                }
                else
                {
                    var component = FindInterfaceComponentReactive(entity, p.type, p.channel, cursor);
                    if (component == null && !p.optional) return null;
                    perParam[j] = new List<object>(1) { component };
                }
            }
            return CartesianProduct(perParam);
        }

        static List<object[]> CartesianProduct(List<object>[] perParam)
        {
            int n = perParam.Length;
            var result = new List<object[]>();
            var idx = new int[n];
            while (true)
            {
                var combo = new object[n];
                for (int j = 0; j < n; j++) combo[j] = perParam[j][idx[j]];
                result.Add(combo);

                int k = n - 1;
                while (k >= 0)
                {
                    idx[k]++;
                    if (idx[k] < perParam[k].Count) break;
                    idx[k] = 0;
                    k--;
                }
                if (k < 0) break;
            }
            return result;
        }

        void CollectInterfaceComponents(EosEntity entity, Type interfaceType, List<object> into)
        {
            var storages = World.ObjectsStorages.GetByInterface(interfaceType);
            if (storages == null) return;

            for (int s = 0; s < storages.Count; s++)
            {
                var indexed = storages[s] as IIndexedStorage;
                if (indexed == null) continue;
                var component = indexed.TryGetObject(entity);
                if (component != null) into.Add(component);
            }
        }

        void CollectInterfaceComponentsReactive(
            EosEntity entity, Type interfaceType, Channel channel, ulong cursor, List<object> into)
        {
            var storages = World.ObjectsStorages.GetByInterface(interfaceType);
            if (storages == null) return;

            for (int s = 0; s < storages.Count; s++)
            {
                var indexed = storages[s] as IIndexedStorage;
                if (indexed == null) continue;
                int idx = indexed.IndexOf(entity);
                if (idx < 0) continue;
                var component = indexed.GetAt(idx);
                if (component == null) continue;
                if (channel != Channel.None && ChannelVersionAt(indexed, idx, channel) <= cursor) continue;
                into.Add(component);
            }
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

        object FindInterfaceComponentReactive(EosEntity entity, Type interfaceType, Channel channel, ulong cursor)
        {
            var storages = World.ObjectsStorages.GetByInterface(interfaceType);
            if (storages == null) return null;

            for (int s = 0; s < storages.Count; s++)
            {
                var indexed = storages[s] as IIndexedStorage;
                if (indexed == null) continue;
                int idx = indexed.IndexOf(entity);
                if (idx < 0) continue;
                var component = indexed.GetAt(idx);
                if (component == null) continue;
                if (channel != Channel.None && ChannelVersionAt(indexed, idx, channel) <= cursor) continue;
                return component;
            }
            return null;
        }

        object[] BuildArgs(
            ParameterInfo[] parameters, EosEntity entity, float deltaTime,
            int deltaTimeParamIndex, int entityParamIndex,
            List<(int position, Type type, Channel channel, bool optional)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods,
            IIndexedStorage[] concreteIndexed, int pivot, int pivotIndex,
            List<(int position, Type type, Channel channel, bool optional, bool each)> interfaceParams,
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