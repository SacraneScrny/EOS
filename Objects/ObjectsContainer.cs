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
            for (int i = _updates.Count - 1; i >= 0; i--)
                if (_updates[i].IsEnabled)
                    _updates[i].OnUpdate(deltaTime);
        }
        internal void FixedUpdate(float deltaTime)
        {
            for (int i = _fixedUpdates.Count - 1; i >= 0; i--)
                if (_fixedUpdates[i].IsEnabled)
                    _fixedUpdates[i].OnFixedUpdate(deltaTime);
        }
        internal void LateUpdate(float deltaTime)
        {
            for (int i = _lateUpdates.Count - 1; i >= 0; i--)
                if (_lateUpdates[i].IsEnabled)
                    _lateUpdates[i].OnLateUpdate(deltaTime);
        }

        internal void RegisterObject(EosObject obj)
        {
            if (obj == null) return;
            if (obj is IObjectUpdate u) { obj.UpdateIndex = _updates.Count; _updates.Add(u); }
            if (obj is IObjectFixedUpdate fu) { obj.FixedIndex = _fixedUpdates.Count; _fixedUpdates.Add(fu); }
            if (obj is IObjectLateUpdate lu) { obj.LateIndex = _lateUpdates.Count; _lateUpdates.Add(lu); }
            obj.Initialized = false;
            obj.PoolIndex = _waiting.Count;
            _waiting.Add(obj);
        }
        internal void UnregisterObject(EosObject obj)
        {
            if (obj == null) return;
            RemoveUpdate(obj);
            RemoveFixed(obj);
            RemoveLate(obj);
            RemovePool(obj);
        }

        internal void MarkInitialized(EosObject obj)
        {
            RemovePool(obj);
            obj.Initialized = true;
            obj.PoolIndex = _inited.Count;
            _inited.Add(obj);
        }
        internal void MarkFailed(EosObject obj) => UnregisterObject(obj);

        void RemoveUpdate(EosObject obj)
        {
            int i = obj.UpdateIndex;
            if (i < 0) return;
            int last = _updates.Count - 1;
            if (i != last)
            {
                var moved = _updates[last];
                _updates[i] = moved;
                ((EosObject)moved).UpdateIndex = i;
            }
            _updates.RemoveAt(last);
            obj.UpdateIndex = -1;
        }
        void RemoveFixed(EosObject obj)
        {
            int i = obj.FixedIndex;
            if (i < 0) return;
            int last = _fixedUpdates.Count - 1;
            if (i != last)
            {
                var moved = _fixedUpdates[last];
                _fixedUpdates[i] = moved;
                ((EosObject)moved).FixedIndex = i;
            }
            _fixedUpdates.RemoveAt(last);
            obj.FixedIndex = -1;
        }
        void RemoveLate(EosObject obj)
        {
            int i = obj.LateIndex;
            if (i < 0) return;
            int last = _lateUpdates.Count - 1;
            if (i != last)
            {
                var moved = _lateUpdates[last];
                _lateUpdates[i] = moved;
                ((EosObject)moved).LateIndex = i;
            }
            _lateUpdates.RemoveAt(last);
            obj.LateIndex = -1;
        }
        void RemovePool(EosObject obj)
        {
            int i = obj.PoolIndex;
            if (i < 0) return;
            var list = obj.Initialized ? _inited : _waiting;
            int last = list.Count - 1;
            if (i != last)
            {
                var moved = list[last];
                list[i] = moved;
                moved.PoolIndex = i;
            }
            list.RemoveAt(last);
            obj.PoolIndex = -1;
        }
    }
}
