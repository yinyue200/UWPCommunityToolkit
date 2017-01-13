using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.Notifications.SharedWinRT.ToastHistoryChangeTracker.Database
{
    internal class AsyncLocker
    {
        private List<AsyncLockInstance> _lockInstatnces = new List<AsyncLockInstance>();

        public async Task<IDisposable> LockAsync()
        {
            AsyncLockInstance instance;

            lock (this)
            {
                instance = new AsyncLockInstance(this);
                _lockInstatnces.Add(instance);

                // If this is the first task, start it immediately
                if (_lockInstatnces.Count == 1)
                {
                    instance.Start();
                }
            }

            // Wait for the instance to be locked
            await instance.LockEstablishedTask;

            // And then return the disposable instance
            return instance;
        }

        private void Release(AsyncLockInstance instance)
        {
            lock (this)
            {
                bool wasCurrentTask = _lockInstatnces.FirstOrDefault() == instance;

                _lockInstatnces.Remove(instance);

                if (wasCurrentTask && _lockInstatnces.Count > 0)
                {
                    _lockInstatnces[0].Start();
                }
            }
        }

        private class AsyncLockInstance : IDisposable
        {
            public Task LockEstablishedTask
            {
                get
                {
                    return _taskCompletionSource.Task;
                }
            }

            private AsyncLocker _locker;
            private TaskCompletionSource<bool> _taskCompletionSource;

            public AsyncLockInstance(AsyncLocker locker)
            {
                _locker = locker;
                _taskCompletionSource = new TaskCompletionSource<bool>();
            }

            public void Start()
            {
                bool succeeded = _taskCompletionSource.TrySetResult(true);
                if (!succeeded)
                {
                    Dispose();
                }
            }

            public void Dispose()
            {
                _locker.Release(this);
            }
        }
    }
}
