using System;
using System.Collections.Generic;

using EOS.Logging;

namespace EOS.Core
{
    public interface IServiceLocator
    {
        T Get<T>() where T : class;
        bool TryGet<T>(out T service) where T : class;
        bool Has<T>() where T : class;
    }

    public interface IServiceRegistry : IServiceLocator
    {
        void Register<T>(T service) where T : class;
        void Unregister<T>() where T : class;
    }

    public class ServiceContainer : WorldBound, IServiceRegistry
    {
        readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T service) where T : class
        {
            if (World != null && World.IsIterating)
            {
                EosLog.Error(
                    $"Service '{typeof(T).Name}' cannot be registered during system iteration. " +
                    "Register services from outside, before driving the world.",
                    nameof(ServiceContainer));
                return;
            }
            if (service == null)
            {
                EosLog.Error($"Attempted to register a null service of type {typeof(T).Name}.", nameof(ServiceContainer));
                return;
            }
            _services[typeof(T)] = service;
        }

        public void Unregister<T>() where T : class
        {
            if (World != null && World.IsIterating)
            {
                EosLog.Error(
                    $"Service '{typeof(T).Name}' cannot be unregistered during system iteration.",
                    nameof(ServiceContainer));
                return;
            }
            _services.Remove(typeof(T));
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
            EosLog.Error($"No service of type {typeof(T).Name} is registered.", nameof(ServiceContainer));
            return null;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var existing))
            {
                service = (T)existing;
                return true;
            }
            service = null;
            return false;
        }

        public bool Has<T>() where T : class => _services.ContainsKey(typeof(T));

        internal void Clear() => _services.Clear();
    }
}
