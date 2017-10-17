using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti.Flow.FlowHelpers
{
    public class LockCollection<T>
    {
        private readonly Dictionary<T, LockItem> locks;

        public LockCollection(IEqualityComparer<T> comparer)
        {
            locks = new Dictionary<T, LockItem>(comparer);
        }

        public Task<IDisposable> GetLock(T key)
        {
            LockItem nextLi = new LockItem(locks, key);
            try
            {
                bool continueImmediately = false;
                lock (locks)
                {
                    LockItem li;
                    if (!locks.TryGetValue(key, out li))
                    {
                        locks.Add(key, nextLi);
                        continueImmediately = true;
                    }
                    else
                    {
                        while (li.Next != null)
                            li = li.Next;

                        li.Next = nextLi;
                    }
                }
                if (continueImmediately)
                    nextLi.Continue();
            }
            catch (Exception e)
            {
                nextLi.Error(e);
            }
            return nextLi.GetTask();
        }

        private class LockItem : IDisposable
        {
            internal volatile LockItem Next;

            private readonly Dictionary<T, LockItem> locks;
            private readonly TaskCompletionSource<IDisposable> tcs = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly T key;

            public LockItem(Dictionary<T, LockItem> locks, T key)
            {
                this.locks = locks;
                this.key = key;
            }

            internal void Continue()
            {
                tcs.TrySetResult(this);
            }

            internal void Error(Exception e)
            {
                tcs.SetException(e);
            }

            internal Task<IDisposable> GetTask()
            {
                return tcs.Task;
            }

            public void Dispose()
            {
                lock (locks)
                {
                    LockItem li;
                    if (!locks.TryGetValue(key, out li))
                        return;

                    if (li != this)
                    {
                        // Something is wrong (comparer is not stable?), but we cannot loose the completions sources
                        while (li.Next != null)
                            li = li.Next;
                        li.Next = Next;
                        return;
                    }

                    if (Next == null)
                    {
                        locks.Remove(key);
                        return;
                    }

                    locks[key] = Next;
                }

                Next.Continue();
            }
        }
    }

}
