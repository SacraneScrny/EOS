using System;
using System.Collections.Generic;

namespace EOS.Loader
{
    public static class IncarnationBridge
    {
        static readonly Dictionary<Type, object> _binders = new();

        public static void Register<TView>(IIncarnationBinder<TView> binder) where TView : class
            => _binders[typeof(TView)] = binder;

        public static void Unregister<TView>() where TView : class
            => _binders.Remove(typeof(TView));

        public static IIncarnationBinder<TView> Resolve<TView>() where TView : class
            => _binders.TryGetValue(typeof(TView), out var binder) ? (IIncarnationBinder<TView>)binder : null;

        public static void Reset() => _binders.Clear();
    }
}
