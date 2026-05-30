using System.Collections.Generic;

using EOS.Core;
using EOS.Objects.Interfaces;

namespace EOS.Objects
{
    public class ObjectsContainer : WorldBound
    {
        readonly List<IObjectUpdate> _updates = new();
        readonly List<IObjectFixedUpdate> _fixedUpdates = new();
        readonly List<IObjectLateUpdate> _lateUpdates = new();
        readonly List<EosObject> _inited = new();
        readonly List<EosObject> _waiting = new();

        public IEnumerable<EosObject> All => _inited;
        internal IEnumerable<EosObject> Waiting => _waiting;

        internal void Reset()
        {
            _updates.Clear();
            _fixedUpdates.Clear();
            _lateUpdates.Clear();
            _inited.Clear();
            _waiting.Clear();
        }

        internal void Update(float deltaTime)
        {
            for (int i = 0; i < _updates.Count; i++)
                if (_updates[i].IsEnabled)
                    _updates[i].OnUpdate(deltaTime);
        }
        internal void FixedUpdate(float deltaTime)
        {
            for (int i = 0; i < _fixedUpdates.Count; i++)
                if (_fixedUpdates[i].IsEnabled)
                    _fixedUpdates[i].OnFixedUpdate(deltaTime);
        }
        internal void LateUpdate(float deltaTime)
        {
            for (int i = 0; i < _lateUpdates.Count; i++)
                if (_lateUpdates[i].IsEnabled)
                    _lateUpdates[i].OnLateUpdate(deltaTime);
        }

        internal void RegisterObject(EosObject obj)
        {
            if (obj == null) return;
            if (obj is IObjectUpdate u) _updates.Add(u);
            if (obj is IObjectFixedUpdate fu) _fixedUpdates.Add(fu);
            if (obj is IObjectLateUpdate lu) _lateUpdates.Add(lu);
            _waiting.Add(obj);
        }
        internal void UnregisterObject(EosObject obj)
        {
            if (obj == null) return;
            if (obj is IObjectUpdate u) _updates.Remove(u);
            if (obj is IObjectFixedUpdate fu) _fixedUpdates.Remove(fu);
            if (obj is IObjectLateUpdate lu) _lateUpdates.Remove(lu);
            _waiting.Remove(obj);
            _inited.Remove(obj);
        }

        internal void MarkInitialized(EosObject obj)
        {
            _waiting.Remove(obj);
            _inited.Add(obj);
        }
        internal void MarkFailed(EosObject obj) => UnregisterObject(obj);
    }
}
