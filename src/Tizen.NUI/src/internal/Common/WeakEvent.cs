/*
 * Copyright (c) 2020 Samsung Electronics Co., Ltd.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tizen.NUI
{
    internal class WeakEvent<T> where T : Delegate
    {
        private const int cleanUpThreshold = 100; // Experimetal constant
        private int cleanUpCount = 0;
        private List<WeakHandler<T>> handlers = new List<WeakHandler<T>>();

        protected int Count => handlers.Count;

        public virtual void Add(T handler)
        {
            handlers.Add(new WeakHandler<T>(handler));
            OnCountIncreased();

            CleanUpDeadHandlersIfNeeds();
        }

        public virtual void Remove(T handler)
        {
            int lastIndex = handlers.FindLastIndex(item => item.Equals(handler));

            if (lastIndex >= 0)
            {
                handlers.RemoveAt(lastIndex);
                OnCountDicreased();
            }

            CleanUpDeadHandlersIfNeeds();
        }

        public void Invoke(object sender, EventArgs args)
        {
            // Iterate copied one to prevent addition/removal item in the handler call.
            var copiedArray = handlers.ToArray();
            foreach (var item in copiedArray)
            {
                item.Invoke(sender, args);
            }

            // Clean up GC items
            CleanUpDeadHandlers();
        }

        protected virtual void OnCountIncreased()
        {
        }


        protected virtual void OnCountDicreased()
        {
        }

        private void CleanUpDeadHandlersIfNeeds()
        {
            if (++cleanUpCount == cleanUpThreshold)
            {
                CleanUpDeadHandlers();
            }
        }

        private void CleanUpDeadHandlers()
        {
            cleanUpCount = 0;
            int count = handlers.Count;
            handlers.RemoveAll(item => !item.IsAlive);
            if (count > handlers.Count) OnCountDicreased();
        }

        internal class WeakHandler<U> where U : Delegate
        {
            private WeakReference weakTarget; // Null value means the method is static.
            private MethodInfo methodInfo;

            public WeakHandler(U handler)
            {
                Delegate d = (Delegate)(object)handler;
                if (d.Target != null) weakTarget = new WeakReference(d.Target);
                methodInfo = d.Method;
            }

            private bool IsStatic => weakTarget == null;

            public bool IsAlive
            {
                get
                {
                    var rooting = weakTarget?.Target;

                    return IsStatic || !IsDisposed(rooting);
                }
            }

            private static bool IsDisposed(object target)
            {
                if (target == null) return true;

                if (target is BaseHandle basehandle) return basehandle.Disposed || basehandle.IsDisposeQueued;

                if (target is Disposable disposable) return disposable.Disposed || disposable.IsDisposeQueued;

                return false;
            }

            public bool Equals(U handler)
            {
                Delegate other = (Delegate)(object)handler;
                bool isOtherStatic = other.Target == null;
                return (isOtherStatic || weakTarget?.Target == other.Target) && methodInfo.Equals(other.Method);
            }

            public void Invoke(params object[] args)
            {
                if (IsStatic)
                {
                    Delegate.CreateDelegate(typeof(U), methodInfo).DynamicInvoke(args);
                }
                else
                {
                    // Because GC is done in other thread,
                    // it needs to check again that the reference is still alive before calling method.
                    // To do that, the reference should be assigned to the local variable first.
                    var rooting = weakTarget.Target;

                    if (IsAlive)
                    {
                        Delegate.CreateDelegate(typeof(U), rooting, methodInfo).DynamicInvoke(args);
                    }
                }
            }
        }
    }
}
