using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EOS.Attributes;
using EOS.CodeGen;
using EOS.Core;
using EOS.Entities;
using EOS.Events;
using EOS.Logging;
using EOS.Objects;
using EOS.Storage;
using EOS.Tags;

namespace EOS.Systems
{
    public partial class SystemsRunner : WorldBound
    {
        const string EXECUTE_METHOD = "Execute";
        const string EVENT_EXECUTE_METHOD = "EventExecute";
        const string GET = "Get";
        const string HAS = "HasReady";
        const string GET_OWNER = "GetOwner";
        const string COUNT = "Count";

        static void Invoke(SystemInvoker invoker, object instance, MethodInfo method, object[] args)
        {
            if (invoker != null) invoker((EosSystem)instance, args);
            else method.Invoke(instance, args);
        }

        (Action<float, ulong> body, bool reactive) BuildQuery(object instance, MethodInfo method, SystemInvoker invoker)
        {
            var parameters = method.GetParameters();
            int entityParamIndex = -1;
            int deltaTimeParamIndex = -1;

            var concreteParams = new List<(int position, Type type, Channel channel, bool optional, bool cascade)>();
            var interfaceParams = new List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)>();

            foreach (var p in parameters)
            {
                var channel = SystemShape.ChannelOf(p, out bool cascade);
                bool optional = p.GetCustomAttribute<OptionalAttribute>() != null;
                bool each = p.GetCustomAttribute<EachAttribute>() != null;

                if (p.ParameterType == typeof(EosEntity))
                    entityParamIndex = p.Position;
                else if (p.ParameterType == typeof(float))
                    deltaTimeParamIndex = p.Position;
                else if (p.ParameterType.IsInterface)
                    interfaceParams.Add((p.Position, p.ParameterType, channel, optional, each, cascade));
                else if (typeof(EosObject).IsAssignableFrom(p.ParameterType))
                    concreteParams.Add((p.Position, p.ParameterType, channel, optional, cascade));
                else
                {
                    EosLog.Error($"Unsupported parameter type {p.ParameterType.Name} in {instance.GetType().Name}.{method.Name}", nameof(SystemsRunner));
                    throw new Exception($"Unsupported parameter type {p.ParameterType.Name} in {method.Name}");
                }
            }

            bool isReactive = concreteParams.Any(p => p.channel != Channel.None)
                              || interfaceParams.Any(p => p.channel != Channel.None);

            if (!isReactive
                && (concreteParams.Count > 0 || interfaceParams.Count > 0)
                && concreteParams.All(p => p.optional)
                && interfaceParams.All(p => p.optional))
            {
                EosLog.Error($"{instance.GetType().Name}.{method.Name}: query with only [Optional] parameters never matches, make at least one parameter mandatory", nameof(SystemsRunner));
                throw new Exception($"query with only [Optional] parameters never matches in {method.Name}");
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
            var concreteOptional = concreteParams.Select(p => p.optional).ToArray();

            var tagFilter = BuildTagFilter(method);

            if (isReactive)
                return (BuildReactiveQuery(instance, method, parameters,
                    concreteParams, concreteStorages, concreteGetMethods, concreteHasMethods,
                    concreteIndexed, concreteOptional,
                    interfaceParams,
                    entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods, tagFilter, invoker), true);

            if (concreteParams.Count == 0 && interfaceParams.Count == 0)
                return (BuildNoComponentQuery(instance, method, parameters,
                    entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods, tagFilter, invoker), false);

            if (concreteParams.Count == 0)
                return (BuildInterfaceOnlyQuery(instance, method, parameters,
                    interfaceParams, entityParamIndex, deltaTimeParamIndex,
                    includeStorages, includeHasMethods,
                    excludeStorages, excludeHasMethods, tagFilter, invoker), false);

            return (BuildConcreteQuery(instance, method, parameters,
                concreteParams, concreteStorages, concreteGetMethods, concreteHasMethods,
                concreteGetOwners, concreteCountProps, concreteIndexed, concreteOptional,
                interfaceParams,
                entityParamIndex, deltaTimeParamIndex,
                includeStorages, includeHasMethods,
                excludeStorages, excludeHasMethods, tagFilter, invoker), false);
        }

        (Action<float> body, IEventChannel channel, int slot) BuildEventQuery(object instance, MethodInfo method, SystemInvoker invoker)
        {
            var parameters = method.GetParameters();
            int eventParamIndex = -1;
            int deltaTimeParamIndex = -1;
            Type eventType = null;

            foreach (var p in parameters)
            {
                if (p.ParameterType == typeof(float))
                    deltaTimeParamIndex = p.Position;
                else if (p.ParameterType.IsValueType && p.ParameterType != typeof(EosEntity))
                {
                    if (eventType != null)
                        throw new Exception($"EventExecute accepts a single event parameter, found {eventType.Name} and {p.ParameterType.Name}");
                    eventType = p.ParameterType;
                    eventParamIndex = p.Position;
                }
                else
                    throw new Exception($"Unsupported parameter type {p.ParameterType.Name} in EventExecute");
            }

            if (eventType == null)
                throw new Exception("EventExecute requires one struct event parameter");

            var channel = World.Events.ChannelFor(eventType)
                ?? throw new Exception($"Could not resolve event channel for {eventType.Name}");

            int slot = channel.RegisterConsumer();
            int evPos = eventParamIndex;
            int dtPos = deltaTimeParamIndex;
            int length = parameters.Length;

            Action<float> body = deltaTime =>
            {
                ulong cursor = channel.CursorOf(slot);
                if (channel.MaxSeq <= cursor) return;

                var args = new object[length];
                if (dtPos != -1) args[dtPos] = deltaTime;

                int count = channel.LiveCount;
                for (int i = 0; i < count; i++)
                {
                    if (channel.SeqAt(i) <= cursor) continue;
                    args[evPos] = channel.BoxedAt(i);
                    Invoke(invoker, instance, method, args);
                }
            };

            return (body, channel, slot);
        }

        TagFilter BuildTagFilter(MethodInfo method)
        {
            var require = World.Tags.BuildMask(CollectTags<WithTagAttribute>(method));
            var exclude = World.Tags.BuildMask(CollectTags<WithoutTagAttribute>(method));
            var any = World.Tags.BuildMask(CollectTags<WithAnyTagAttribute>(method));
            var one = World.Tags.BuildMask(CollectTags<WithOneTagAttribute>(method));

            if (require == null && exclude == null && any == null && one == null)
                return TagFilter.None;

            return new TagFilter(World.Tags, require, exclude, any, one);
        }

        static IEnumerable<object> CollectTags<TAttr>(MethodInfo method) where TAttr : TagFilterAttribute
        {
            List<object> result = null;
            foreach (var attr in method.GetCustomAttributes<TAttr>(true))
            {
                if (attr.Tags == null) continue;
                result ??= new List<object>();
                result.AddRange(attr.Tags);
            }
            return result;
        }

        Func<EosEntity, bool> BuildTagMatch(MethodInfo method)
        {
            var filter = BuildTagFilter(method);
            if (filter.IsNone) return null;
            return filter.Matches;
        }

        IIndexedStorage[] ResolveIndexedStorages(List<Type> types)
        {
            var list = new List<IIndexedStorage>(types.Count);
            for (int i = 0; i < types.Count; i++)
                if (ResolveConcreteStorage(types[i]) is IIndexedStorage indexed)
                    list.Add(indexed);
            return list.ToArray();
        }

        List<Type> CollectIncludeTypes(MethodInfo method)
        {
            var types = new List<Type>();
            foreach (var attr in method.GetCustomAttributes<IncludeAttribute>(true))
                types.AddRange(attr.Types);
            return types;
        }

        List<Type> CollectExcludeTypes(MethodInfo method)
        {
            var types = new List<Type>();
            foreach (var attr in method.GetCustomAttributes<ExcludeAttribute>(true))
                types.AddRange(attr.Types);
            return types;
        }

        Action<float, ulong> BuildConcreteQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional, bool cascade)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods, MethodInfo[] concreteHasMethods,
            MethodInfo[] concreteGetOwners, PropertyInfo[] concreteCountProps,
            IIndexedStorage[] concreteIndexed, bool[] concreteOptional,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods, TagFilter tagFilter,
            SystemInvoker invoker)
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

                    if (!tagFilter.Matches(entity)) continue;

                    var combos = ResolveInterfaceCombinations(entity, interfaceParams);
                    if (combos == null) continue;

                    for (int c = 0; c < combos.Count; c++)
                        Invoke(invoker, instance, method, BuildArgs(parameters, entity, deltaTime,
                            deltaTimeParamIndex, entityParamIndex,
                            concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, pivot, i,
                            interfaceParams, combos[c], 0UL));
                }
            };
        }

        Action<float, ulong> BuildReactiveQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional, bool cascade)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods, MethodInfo[] concreteHasMethods,
            IIndexedStorage[] concreteIndexed, bool[] concreteOptional,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods, TagFilter tagFilter,
            SystemInvoker invoker)
        {
            int driverConcrete = concreteParams.FindIndex(p => p.channel != Channel.None && !p.optional);

            if (driverConcrete >= 0)
            {
                var driver = concreteIndexed[driverConcrete];
                Channel driverChannel = concreteParams[driverConcrete].channel;
                bool driverCascade = concreteParams[driverConcrete].cascade;

                if (driverChannel == Channel.Removed)
                    return BuildRemovedQuery(instance, method, parameters,
                        concreteParams, interfaceParams, driver, driverConcrete, driverCascade,
                        entityParamIndex, deltaTimeParamIndex,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods, tagFilter, invoker);

                bool driverRequiresReady = ChannelRequiresReady(driverChannel);

                return (deltaTime, cursor) =>
                {
                    if (ChannelMax(driver, driverChannel) <= cursor) return;

                    int count = driver.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (driverRequiresReady && !driver.IsReady(i)) continue;
                        if (!ChannelPasses(driver, i, driverChannel, driverCascade, cursor)) continue;

                        var entity = driver.GetOwner(i);

                        if (!ReactiveConcreteMatch(entity, concreteParams, concreteIndexed,
                            concreteHasMethods, concreteStorages, driverConcrete, cursor)) continue;

                        if (!CheckIncludeExclude(entity,
                            includeStorages, includeHasMethods,
                            excludeStorages, excludeHasMethods)) continue;

                        if (!tagFilter.Matches(entity)) continue;

                        var combos = ResolveInterfaceCombinationsReactive(entity, interfaceParams, cursor);
                        if (combos == null) continue;

                        for (int c = 0; c < combos.Count; c++)
                            Invoke(invoker, instance, method, BuildArgs(parameters, entity, deltaTime,
                                deltaTimeParamIndex, entityParamIndex,
                                concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, driverConcrete, i,
                                interfaceParams, combos[c], cursor));
                    }
                };
            }
            else
            {
                var driverIface = interfaceParams.FirstOrDefault(p => p.channel != Channel.None && !p.optional);
                if (driverIface.type == null)
                {
                    EosLog.Error($"{instance.GetType().Name}.Execute: reactive interface query requires at least one non-optional reactive interface parameter", nameof(SystemsRunner));
                    return (_, __) => { };
                }
                Channel driverChannel = driverIface.channel;
                Type driverType = driverIface.type;
                bool driverCascade = driverIface.cascade;

                if (driverChannel == Channel.Removed)
                {
                    EosLog.Error($"{instance.GetType().Name}.Execute: [Removed] is supported only on a concrete component parameter", nameof(SystemsRunner));
                    return (_, __) => { };
                }

                bool driverRequiresReady = ChannelRequiresReady(driverChannel);

                return (deltaTime, cursor) =>
                {
                    var storages = World.ObjectsStorages.GetByInterface(driverType);
                    if (storages == null) return;

                    var seen = new HashSet<int>();

                    foreach (var storage in storages)
                    {
                        var indexed = storage as IIndexedStorage;
                        if (indexed == null) continue;
                        if (ChannelMax(indexed, driverChannel) <= cursor) continue;

                        int count = indexed.Count;
                        for (int i = 0; i < count; i++)
                        {
                            if (driverRequiresReady && !indexed.IsReady(i)) continue;
                            if (!ChannelPasses(indexed, i, driverChannel, driverCascade, cursor)) continue;

                            var entity = indexed.GetOwner(i);
                            if (!seen.Add(entity.Id)) continue;

                            if (!ReactiveConcreteMatch(entity, concreteParams, concreteIndexed,
                                concreteHasMethods, concreteStorages, -1, cursor)) continue;

                            if (!CheckIncludeExclude(entity,
                                includeStorages, includeHasMethods,
                                excludeStorages, excludeHasMethods)) continue;

                            if (!tagFilter.Matches(entity)) continue;

                            var combos = ResolveInterfaceCombinationsReactive(entity, interfaceParams, cursor);
                            if (combos == null) continue;

                            for (int c = 0; c < combos.Count; c++)
                                Invoke(invoker, instance, method, BuildArgs(parameters, entity, deltaTime,
                                    deltaTimeParamIndex, entityParamIndex,
                                    concreteParams, concreteStorages, concreteGetMethods, concreteIndexed, -1, -1,
                                    interfaceParams, combos[c], cursor));
                        }
                    }
                };
            }
        }

        Action<float, ulong> BuildRemovedQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional, bool cascade)> concreteParams,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams,
            IIndexedStorage driver, int driverConcrete, bool driverCascade,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods, TagFilter tagFilter,
            SystemInvoker invoker)
        {
            if (concreteParams.Count != 1 || interfaceParams.Count != 0)
            {
                EosLog.Error($"{instance.GetType().Name}.Execute: [Removed] must be the only component parameter (EosEntity/float/filters allowed)", nameof(SystemsRunner));
                return (_, __) => { };
            }

            int removedPosition = concreteParams[driverConcrete].position;
            int length = parameters.Length;

            return (deltaTime, cursor) =>
            {
                if (driver.MaxRemoveVersion <= cursor) return;

                int count = driver.RemovedCount;
                for (int i = 0; i < count; i++)
                {
                    if (driver.RemovedVersionAt(i) <= cursor) continue;
                    if (!driverCascade && driver.RemovedCascadeAt(i)) continue;

                    var entity = driver.RemovedOwnerAt(i);

                    if (!CheckIncludeExclude(entity,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods)) continue;

                    if (!tagFilter.Matches(entity)) continue;

                    var args = new object[length];
                    if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                    if (entityParamIndex != -1) args[entityParamIndex] = entity;
                    args[removedPosition] = null;
                    Invoke(invoker, instance, method, args);
                }
            };
        }

        Action<float, ulong> BuildNoComponentQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods, TagFilter tagFilter,
            SystemInvoker invoker)
        {
            if (entityParamIndex == -1)
            {
                return (deltaTime, _) =>
                {
                    var args = new object[parameters.Length];
                    if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
                    Invoke(invoker, instance, method, args);
                };
            }

            return (deltaTime, _) =>
            {
                var args = new object[parameters.Length];
                if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;

                foreach (var entity in World.Entities.All())
                {
                    if (!entity.IsActive) continue;

                    if (!CheckIncludeExclude(entity,
                        includeStorages, includeHasMethods,
                        excludeStorages, excludeHasMethods)) continue;

                    if (!tagFilter.Matches(entity)) continue;

                    args[entityParamIndex] = entity;
                    Invoke(invoker, instance, method, args);
                }
            };
        }

        Action<float, ulong> BuildInterfaceOnlyQuery(
            object instance, MethodInfo method, ParameterInfo[] parameters,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams,
            int entityParamIndex, int deltaTimeParamIndex,
            object[] includeStorages, MethodInfo[] includeHasMethods,
            object[] excludeStorages, MethodInfo[] excludeHasMethods, TagFilter tagFilter,
            SystemInvoker invoker)
        {
            var pivotParam = interfaceParams.FirstOrDefault(p => !p.optional);
            if (pivotParam.type == null)
            {
                EosLog.Error($"{instance.GetType().Name}.Execute: at least one interface parameter must be non-optional", nameof(SystemsRunner));
                return (_, __) => { };
            }
            var pivotIfaceType = pivotParam.type;

            return (deltaTime, _) =>
            {
                var pivotStorages = World.ObjectsStorages.GetByInterface(pivotIfaceType);
                if (pivotStorages == null) return;

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

                        if (!tagFilter.Matches(entity)) continue;

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

                            Invoke(invoker, instance, method, args);
                        }
                    }
                }
            };
        }


        static ulong ChannelVersionAt(IIndexedStorage storage, int index, Channel channel) => channel switch
        {
            Channel.Bumped => storage.MarkVersionAt(index),
            Channel.Enabled => storage.EnableVersionAt(index),
            Channel.Disabled => storage.DisableVersionAt(index),
            _ => storage.AddVersionAt(index)
        };

        static ulong ChannelMax(IIndexedStorage storage, Channel channel) => channel switch
        {
            Channel.Bumped => storage.MaxMarkVersion,
            Channel.Enabled => storage.MaxEnableVersion,
            Channel.Disabled => storage.MaxDisableVersion,
            _ => storage.MaxAddVersion
        };

        static bool ChannelRequiresReady(Channel channel) => channel != Channel.Disabled;

        static bool ChannelPasses(IIndexedStorage storage, int index, Channel channel, bool includeCascade, ulong cursor)
        {
            if (ChannelVersionAt(storage, index, channel) <= cursor) return false;
            if (includeCascade) return true;
            if (channel == Channel.Enabled) return !storage.EnableCascadeAt(index);
            if (channel == Channel.Disabled) return !storage.DisableCascadeAt(index);
            return true;
        }

        bool ReactiveConcreteMatch(
            EosEntity entity,
            List<(int position, Type type, Channel channel, bool optional, bool cascade)> concreteParams,
            IIndexedStorage[] concreteIndexed, MethodInfo[] concreteHasMethods, object[] concreteStorages,
            int driverIndex, ulong cursor)
        {
            for (int j = 0; j < concreteParams.Count; j++)
            {
                if (j == driverIndex) continue;
                var channel = concreteParams[j].channel;
                bool optional = concreteParams[j].optional;

                if (channel == Channel.Removed)
                {
                    if (optional) continue;
                    return false;
                }

                if (channel != Channel.None)
                {
                    bool cascade = concreteParams[j].cascade;
                    int idx = concreteIndexed[j].IndexOf(entity);
                    if (idx < 0 || (ChannelRequiresReady(channel) && !concreteIndexed[j].IsReady(idx)))
                    {
                        if (optional) continue;
                        return false;
                    }
                    if (!ChannelPasses(concreteIndexed[j], idx, channel, cascade, cursor))
                    {
                        if (optional) continue;
                        return false;
                    }
                }
                else if (!optional)
                {
                    if (!(bool)concreteHasMethods[j].Invoke(concreteStorages[j], new object[] { entity }))
                        return false;
                }
            }
            return true;
        }

        static readonly List<object[]> _emptyCombo = new() { Array.Empty<object>() };

        List<object[]> ResolveInterfaceCombinations(
            EosEntity entity,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams)
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

        List<object[]> ResolveInterfaceCombinationsReactive(
            EosEntity entity,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams,
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
                    CollectInterfaceComponentsReactive(entity, p.type, p.channel, p.cascade, cursor, list);
                    if (list.Count == 0)
                    {
                        if (!p.optional) return null;
                        list.Add(null);
                    }
                    perParam[j] = list;
                }
                else
                {
                    var component = FindInterfaceComponentReactive(entity, p.type, p.channel, p.cascade, cursor);
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
                var component = indexed.TryGetReadyObject(entity);
                if (component != null) into.Add(component);
            }
        }

        void CollectInterfaceComponentsReactive(
            EosEntity entity, Type interfaceType, Channel channel, bool includeCascade, ulong cursor, List<object> into)
        {
            var storages = World.ObjectsStorages.GetByInterface(interfaceType);
            if (storages == null) return;

            for (int s = 0; s < storages.Count; s++)
            {
                var indexed = storages[s] as IIndexedStorage;
                if (indexed == null) continue;
                if (channel == Channel.Removed) continue;
                int idx = indexed.IndexOf(entity);
                if (idx < 0) continue;
                if (ChannelRequiresReady(channel) && !indexed.IsReady(idx)) continue;
                var component = indexed.GetAt(idx);
                if (component == null) continue;
                if (channel != Channel.None && !ChannelPasses(indexed, idx, channel, includeCascade, cursor)) continue;
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
                var component = indexed.TryGetReadyObject(entity);
                if (component != null) return component;
            }
            return null;
        }

        object FindInterfaceComponentReactive(EosEntity entity, Type interfaceType, Channel channel, bool includeCascade, ulong cursor)
        {
            var storages = World.ObjectsStorages.GetByInterface(interfaceType);
            if (storages == null) return null;

            for (int s = 0; s < storages.Count; s++)
            {
                var indexed = storages[s] as IIndexedStorage;
                if (indexed == null) continue;
                if (channel == Channel.Removed) continue;
                int idx = indexed.IndexOf(entity);
                if (idx < 0) continue;
                if (ChannelRequiresReady(channel) && !indexed.IsReady(idx)) continue;
                var component = indexed.GetAt(idx);
                if (component == null) continue;
                if (channel != Channel.None && !ChannelPasses(indexed, idx, channel, includeCascade, cursor)) continue;
                return component;
            }
            return null;
        }

        object[] BuildArgs(
            ParameterInfo[] parameters, EosEntity entity, float deltaTime,
            int deltaTimeParamIndex, int entityParamIndex,
            List<(int position, Type type, Channel channel, bool optional, bool cascade)> concreteParams,
            object[] concreteStorages, MethodInfo[] concreteGetMethods,
            IIndexedStorage[] concreteIndexed, int pivot, int pivotIndex,
            List<(int position, Type type, Channel channel, bool optional, bool each, bool cascade)> interfaceParams,
            object[] ifaceComponents, ulong cursor)
        {
            var args = new object[parameters.Length];
            if (deltaTimeParamIndex != -1) args[deltaTimeParamIndex] = deltaTime;
            if (entityParamIndex != -1) args[entityParamIndex] = entity;

            for (int j = 0; j < concreteParams.Count; j++)
            {
                if (j == pivot && pivotIndex >= 0)
                    args[concreteParams[j].position] = concreteIndexed[pivot].GetAt(pivotIndex);
                else if (concreteParams[j].optional)
                    args[concreteParams[j].position] = ResolveOptional(concreteIndexed[j], entity, concreteParams[j].channel, concreteParams[j].cascade, cursor);
                else
                    args[concreteParams[j].position] = concreteGetMethods[j].Invoke(concreteStorages[j], new object[] { entity });
            }

            for (int j = 0; j < interfaceParams.Count; j++)
                args[interfaceParams[j].position] = ifaceComponents[j];

            return args;
        }

        static object ResolveOptional(IIndexedStorage storage, EosEntity entity, Channel channel, bool includeCascade, ulong cursor)
        {
            if (channel == Channel.Removed) return null;
            int idx = storage.IndexOf(entity);
            if (idx < 0) return null;
            if (ChannelRequiresReady(channel) && !storage.IsReady(idx)) return null;
            if (channel != Channel.None && !ChannelPasses(storage, idx, channel, includeCascade, cursor)) return null;
            return storage.GetAt(idx);
        }


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