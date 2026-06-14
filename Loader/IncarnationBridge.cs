using System;
using System.Collections.Generic;

namespace EOS.Loader
{
    /// <summary>Static registry mapping each view type to its <see cref="IIncarnationBinder{TView}"/>; the engine consumer registers binders here and <c>Incarnation&lt;TView&gt;</c> resolves them.</summary>
    public static class IncarnationBridge
    {
        static readonly Dictionary<Type, object> _binders = new();

        /// <summary>Registers (or replaces) the binder for view type <typeparamref name="TView"/>.</summary>
        public static void Register<TView>(IIncarnationBinder<TView> binder) where TView : class
            => _binders[typeof(TView)] = binder;

        /// <summary>Removes the binder registered for <typeparamref name="TView"/>, if any.</summary>
        public static void Unregister<TView>() where TView : class
            => _binders.Remove(typeof(TView));

        /// <summary>Returns the binder for <typeparamref name="TView"/>, or null if none is registered.</summary>
        public static IIncarnationBinder<TView> Resolve<TView>() where TView : class
            => _binders.TryGetValue(typeof(TView), out var binder) ? (IIncarnationBinder<TView>)binder : null;

        /// <summary>Clears all registered binders; called on domain reset.</summary>
        public static void Reset() => _binders.Clear();
    }
}
