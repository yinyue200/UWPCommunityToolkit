using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Notifications;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    public static class ToastNotificationHistoryExtensions
    {
        /// <summary>
        /// Removes all notifications sent by this app from Action Center.
        /// </summary>
        /// <param name="history"></param>
        public static IAsyncAction ClearEnhanced(this ToastNotificationHistory history)
        {
            return ToastHistoryChangeDatabase.Clear(history).AsAsyncAction();
        }

        public static IAsyncAction RemoveEnhanced(this ToastNotificationHistory history, string tag)
        {
            return ToastHistoryChangeDatabase.Remove(history, tag).AsAsyncAction();
        }

        public static IAsyncAction RemoveEnhanced(this ToastNotificationHistory history, string tag, string group)
        {
            return ToastHistoryChangeDatabase.Remove(history, tag, group).AsAsyncAction();
        }

        public static IAsyncAction RemoveGroupEnhanced(this ToastNotificationHistory history, string group)
        {
            return ToastHistoryChangeDatabase.RemoveGroup(history, group).AsAsyncAction();
        }
    }
}
