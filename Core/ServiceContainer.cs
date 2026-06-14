using System;
using System.Collections.Generic;

using EOS.Logging;

namespace EOS.Core
{
    /// <summary>Read-only view of a world's service locator, keyed by type. Exposed as <c>World.Services</c>.</summary>
    public interface IServiceLocator
    {
        /// <summary>Returns the registered service of type <typeparamref name="T"/>, or null (with an error) if none is registered.</summary>
        T Get<T>() where T : class;
        /// <summary>Returns the registered service of type <typeparamref name="T"/> if present, without logging on a miss.</summary>
        bool TryGet<T>(out T service) where T : class;
        /// <summary>Whether a service of type <typeparamref name="T"/> is registered.</summary>
        bool Has<T>() where T : class;
    }

    /// <summary>Writable service locator that adds registration. Exposed as <c>World.ServiceRegistry</c>; wire services before driving the world.</summary>
    public interface IServiceRegistry : IServiceLocator
    {
        /// <summary>Registers (overwriting any existing) the service of type <typeparamref name="T"/>; nulls and registrations during iteration are rejected.</summary>
        void Register<T>(T service) where T : class;
        /// <summary>Removes the registered service of type <typeparamref name="T"/>; rejected during iteration.</summary>
        void Unregister<T>() where T : class;
    }

    /// <summary>The per-world service locator implementation backing <c>World.Services</c> / <c>World.ServiceRegistry</c>.</summary>
    public class ServiceContainer : WorldBound, IServiceRegistry
    {
        readonly Dictionary<Type, object> _services = new();

        /// <summary>Registers (overwriting any existing) the service of type <typeparamref name="T"/>; nulls and registrations during iteration are rejected with an error.</summary>
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

        /// <summary>Removes the registered service of type <typeparamref name="T"/>; rejected during iteration.</summary>
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

        /// <summary>Returns the registered service of type <typeparamref name="T"/>, or null (with an error) if none is registered.</summary>
        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
            EosLog.Error($"No service of type {typeof(T).Name} is registered.", nameof(ServiceContainer));
            return null;
        }

        /// <summary>Returns the registered service of type <typeparamref name="T"/> if present, without logging on a miss.</summary>
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

        /// <summary>Whether a service of type <typeparamref name="T"/> is registered.</summary>
        public bool Has<T>() where T : class => _services.ContainsKey(typeof(T));

        internal void Clear() => _services.Clear();
    }
}
