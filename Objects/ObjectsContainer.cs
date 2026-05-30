using System.Collections.Generic;

using EOS.Objects.Interfaces;

namespace EOS.Objects
{
    internal static class ObjectsContainer
    {
        static readonly List<IObjectUpdate> _updates = new();
        static readonly List<IObjectFixedUpdate> _fixedUpdates = new();
        static readonly List<IObjectLateUpdate> _lateUpdates = new();
        static readonly List<EosObject> _inited = new();
        static readonly List<EosObject> _waiting = new();

        public static IEnumerable<EosObject> Waiting => _waiting;

        public static void Init()
        {
            _updates.Clear();
            _fixedUpdates.Clear();
            _lateUpdates.Clear();
        }

        public static void Update(float deltaTime)
        {
            for (int i = 0; i < _updates.Count; i++)
                if (_updates[i].IsEnabled)
                    _updates[i].OnUpdate(deltaTime);
        }
        public static void FixedUpdate(float deltaTime)
        {
            for (int i = 0; i < _fixedUpdates.Count; i++)
                if (_fixedUpdates[i].IsEnabled)
                    _fixedUpdates[i].OnFixedUpdate(deltaTime);
        }
        public static void LateUpdate(float deltaTime)
        {
            for (int i = 0; i < _lateUpdates.Count; i++)
                if (_lateUpdates[i].IsEnabled)
                    _lateUpdates[i].OnLateUpdate(deltaTime);
        }

        public static void RegisterObject(EosObject obj)
        {
            if (obj == null) return;
            if (obj is IObjectUpdate u) _updates.Add(u);
            if (obj is IObjectFixedUpdate fu) _fixedUpdates.Add(fu);
            if (obj is IObjectLateUpdate lu) _lateUpdates.Add(lu);
            _waiting.Add(obj);
        }
        public static void UnregisterObject(EosObject obj)
        {
            if (obj == null) return;
            if (obj is IObjectUpdate u) _updates.Remove(u);
            if (obj is IObjectFixedUpdate fu) _fixedUpdates.Remove(fu);
            if (obj is IObjectLateUpdate lu) _lateUpdates.Remove(lu);
            _waiting.Remove(obj);
            _inited.Remove(obj);
        }

        public static void MarkInitialized(EosObject obj)
        {
            _waiting.Remove(obj);
            _inited.Add(obj);
        }
        public static void MarkFailed(EosObject obj) => UnregisterObject(obj);
    }
}
