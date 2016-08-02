using System;
using Windows.Foundation;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    /// <summary>
    /// Provides functionality for monitoring changes to <see cref="Windows.UI.Notifications.ToastNotificationHistory"/>. To obtain an instance of this, use <see cref="ToastHistoryChangeTracker.Current"/>.
    /// </summary>
    public sealed class ToastHistoryChangeTracker
    {
        private ToastHistoryChangeTracker() { }

        /// <summary>
        /// Gets a reference to the current <see cref="ToastHistoryChangeTracker"/>.
        /// </summary>
        public static readonly ToastHistoryChangeTracker Current = new ToastHistoryChangeTracker();

        /// <summary>
        /// Call this method to enable change tracking.
        /// </summary>
        /// <returns>An async action.</returns>
        public IAsyncAction EnableAsync()
        {
            return ToastHistoryChangeDatabase.EnableAsync().AsAsyncAction();
        }

        /// <summary>
        /// Gets a <see cref="ToastHistoryChangeReader"/> that can be used to process changes.
        /// </summary>
        /// <returns>A <see cref="ToastHistoryChangeReader"/> that can be used to process changes.</returns>
        public IAsyncOperation<ToastHistoryChangeReader> GetChangeReaderAsync()
        {
            return ToastHistoryChangeReader.Initialize().AsAsyncOperation();
        }

        /// <summary>
        /// Call this method to reset the change tracker if your app receives 
        /// </summary>
        /// <returns>An async action.</returns>
        public IAsyncAction ResetAsync()
        {
            return ToastHistoryChangeDatabase.ResetAsync().AsAsyncAction();
        }
    }
}
