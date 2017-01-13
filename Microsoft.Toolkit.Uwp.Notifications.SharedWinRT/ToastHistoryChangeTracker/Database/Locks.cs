using Microsoft.Toolkit.Uwp.Notifications.SharedWinRT.ToastHistoryChangeTracker.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    internal static class Locks
    {
        private static readonly AsyncLocker _locker = new AsyncLocker();

        public static Task<IDisposable> LockAsync()
        {
            return _locker.LockAsync();
        }
    }
}
