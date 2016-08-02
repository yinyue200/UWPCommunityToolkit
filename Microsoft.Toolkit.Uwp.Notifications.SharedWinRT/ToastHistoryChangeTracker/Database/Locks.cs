using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    internal static class Locks
    {
        private static readonly MultiProcessReadWriteLocker _lock = new MultiProcessReadWriteLocker();

        private const int MILLISECOND_TIMEOUT = 5000;
        
        public static IDisposable LockForWrite()
        {
            Debug.WriteLine("Locking for write...");
            IDisposable answer = _lock.LockWrite(MILLISECOND_TIMEOUT);
            Debug.WriteLine("Locked for write.");
            return answer;
        }
    }
}
