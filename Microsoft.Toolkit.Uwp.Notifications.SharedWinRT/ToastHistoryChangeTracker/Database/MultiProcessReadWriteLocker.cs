using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    /// <summary>
    /// Can be released from any thread (doesn't have to be released from same thread that calls it)
    /// </summary>
    internal class MultiProcessReadWriteLocker
    {
        private class MyDisposableReaderLock : IDisposable
        {
            private MultiProcessReadWriteLocker _locker;
            /// <summary>
            /// Throws <see cref="TimeoutException"/> if lock can't be established in given time.
            /// </summary>
            /// <param name="locker"></param>
            /// <param name="millisecondsTimeout"></param>
            public MyDisposableReaderLock(MultiProcessReadWriteLocker locker, int millisecondsTimeout)
            {
                _locker = locker;
                if (!locker.EnterReadLock(millisecondsTimeout))
                    throw new TimeoutException("Timeout for locker has been reached and lock hasn't been established.");
            }

            public void Dispose()
            {
                _locker.ExitReadLock();
            }
        }

        private class MyDisposableWriterLock : IDisposable
        {
            private MultiProcessReadWriteLocker _locker;
            /// <summary>
            /// Throws <see cref="TimeoutException"/> if lock can't be established in given time.
            /// </summary>
            /// <param name="locker"></param>
            /// <param name="millisecondsTimeout"></param>
            public MyDisposableWriterLock(MultiProcessReadWriteLocker locker, int millisecondsTimeout)
            {
                _locker = locker;
                if (!locker.EnterWriteLock(millisecondsTimeout))
                    throw new TimeoutException("Timeout for locker has been reached and lock hasn't been established.");
            }

            public void Dispose()
            {
                _locker.ExitWriteLock();
            }
        }

        private object _lock = new object();

        /// <summary>
        /// Gets the total number currently in read mode.
        /// </summary>
        public int CurrentReadCount { get; private set; }

        /// <summary>
        /// Gets a value that indicates whether the write lock is currently being held.
        /// </summary>
        public bool IsWriteLockHeld { get; private set; }

        private ManualResetEventSlim _writerResetEvent = new ManualResetEventSlim(true);
        private ManualResetEventSlim _readerResetEvent = new ManualResetEventSlim(true);

        /// <summary>
        /// Tries to enter the lock in read mode.
        /// </summary>
        public bool EnterReadLock(int millisecondsTimeout)
        {
            DateTime start = DateTime.UtcNow;

            while (true)
            {
                lock (_lock)
                {
                    // If there's no write lock held
                    if (!IsWriteLockHeld)
                    {
                        // Enter read and return
                        CurrentReadCount++;
                        _readerResetEvent.Reset(); // Now if Wait is called, it'll block till changed to set
                        return true;
                    }
                }

                int remainingTimeout = GetRemainingTimeout(start, millisecondsTimeout);

                if (ExceededTimeout(remainingTimeout))
                    return false;

                // Otherwise wait for the current write lock to finish
                if (!_writerResetEvent.Wait(remainingTimeout))
                    return false;


                // And then repeat
            }
        }

        public void ExitReadLock()
        {
            lock (_lock)
            {
                if (CurrentReadCount == 0)
                    throw new Exception("Cannot exit read lock when there's currently no existing read locks. More calls to exit were made than enter.");

                CurrentReadCount--;

                // If that was our last reader
                if (CurrentReadCount == 0)
                    _readerResetEvent.Set(); // Release the read blocker (calls to Wait on this will complete now)
            }
        }

        public bool EnterWriteLock(int millisecondsTimeout)
        {
            DateTime start = DateTime.UtcNow;

            while (true)
            {
                lock (_lock)
                {
                    // If there's no existing locks
                    if (!IsWriteLockHeld && CurrentReadCount == 0)
                    {
                        // Establish write lock
                        IsWriteLockHeld = true;
                        _writerResetEvent.Reset(); // Now if Wait is called, it'll block till changed to set
                        return true;
                    }
                }

                int remainingTimeout = GetRemainingTimeout(start, millisecondsTimeout);

                if (ExceededTimeout(remainingTimeout))
                    return false;

                // Wait on existing writer if there is one
                if (!_writerResetEvent.Wait(remainingTimeout))
                    return false;

                remainingTimeout = GetRemainingTimeout(start, millisecondsTimeout);

                if (ExceededTimeout(remainingTimeout))
                    return false;

                // Wait on existing readers (this will continue once all readers reach zero)
                if (!_readerResetEvent.Wait(remainingTimeout))
                    return false;
            }
        }

        private static bool ExceededTimeout(int remainingTimeout)
        {
            // -1 represents infinite time-out
            if (remainingTimeout == -1)
                return false;

            // If there's no time remaining, then we've exceeded
            if (remainingTimeout <= 0)
                return true;

            // Otherwise there's time left
            return false;
        }

        private static int GetRemainingTimeout(DateTime start, int originalMillisecondsTimeout)
        {
            // -1 represents infinite time-out
            if (originalMillisecondsTimeout == -1)
                return -1;

            int elapsedMilliseconds = (int)(DateTime.UtcNow - start).TotalMilliseconds;

            if (elapsedMilliseconds > originalMillisecondsTimeout)
                return 0;

            return originalMillisecondsTimeout - elapsedMilliseconds;
        }

        public void ExitWriteLock()
        {
            lock (_lock)
            {
                if (!IsWriteLockHeld)
                    throw new Exception("Cannot exit write lock when there's currently no existing write lock. More calls to exit were made than enter.");

                IsWriteLockHeld = false;

                // Release the write blocker (calls to Wait on this will complete now)
                _writerResetEvent.Set();
            }
        }

        /// <summary>
        /// Must be disposed from same thread that initially called this
        /// </summary>
        /// <returns></returns>
        public IDisposable LockRead(int millisecondsTimeout)
        {
            return new MyDisposableReaderLock(this, millisecondsTimeout);
        }

        /// <summary>
        /// Must be disposed from same thread that initially called this
        /// </summary>
        /// <returns></returns>
        public IDisposable LockWrite(int millisecondsTimeout)
        {
            return new MyDisposableWriterLock(this, millisecondsTimeout);
        }
    }
}
