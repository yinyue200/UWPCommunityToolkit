using System;
using System.Threading.Tasks;
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
        public static ToastHistoryChangeTracker Current { get; private set; } = new ToastHistoryChangeTracker();

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

        /// <summary>
        /// Returns a task that represents the current database saving operation. You must await this task before your app closes, otherwise
        /// the history change tracking might not be saved. This is especially critical to await when using the tracker in a background task,
        /// since the background process will be terminated immediately after you release the deferral, meaning the tracker won't have time
        /// to save the changes to disk.
        /// </summary>
        public Task SavingTask
        {
            get
            {
                return ToastHistoryChangeDatabase.SavingTask;
            }
        }
    }
}
