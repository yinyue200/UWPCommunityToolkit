using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Windows.Foundation;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    public sealed class ToastHistoryChangeTracker
    {
        public static readonly ToastHistoryChangeTracker Current = new ToastHistoryChangeTracker();

        /// <summary>
        /// Call this method to enable change tracking.
        /// </summary>
        /// <returns></returns>
        public IAsyncAction EnableAsync()
        {
            return ToastHistoryChangeDatabase.EnableAsync().AsAsyncAction();
        }

        /// <summary>
        /// Gets a <see cref="ToastHistoryChangeReader"/> that can be used to process changes.
        /// </summary>
        /// <returns></returns>
        public ToastHistoryChangeReader GetChangeReader()
        {
            return new ToastHistoryChangeReader();
        }

        /// <summary>
        /// Call this method to reset the change tracker if your app receives 
        /// </summary>
        /// <returns></returns>
        public IAsyncAction ResetAsync()
        {
            return ToastHistoryChangeDatabase.ResetAsync().AsAsyncAction();
        }
    }
}
