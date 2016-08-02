using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Notifications;

namespace Microsoft.Toolkit.Uwp.Notifications.SharedWinRT.ToastExtensions
{
    public static class ToastNotificationHistoryExtensions
    {
        /// <summary>
        /// Removes all notifications sent by this app from Action Center.
        /// </summary>
        /// <param name="history"></param>
        public static IAsyncAction ClearEnhanced(this ToastNotificationHistory history)
        {
            return ClearEnhancedHelper(history).AsAsyncAction();
        }

        private static async Task ClearEnhancedHelper(ToastNotificationHistory history)
        {
            await ToastHistoryChangeDatabase.Clear();

            history.Clear();
        }

        public static IAsyncAction RemoveEnhanced(this ToastNotificationHistory history, string tag)
        {
            return RemoveEnhanced(history, tag, "");
        }

        public static IAsyncAction RemoveEnhanced(this ToastNotificationHistory history, string tag, string group)
        {
            return RemoveEnhancedHelper(history, tag, group).AsAsyncAction();
        }

        private static async Task RemoveEnhancedHelper(ToastNotificationHistory history, string tag, string group)
        {
            await ToastHistoryChangeDatabase.Remove(tag, group);

            history.Remove(tag);
        }

        public static IAsyncAction RemoveGroupEnhanced(this ToastNotificationHistory history, string group)
        {
            return RemoveGroupEnhancedHelper(history, group).AsAsyncAction();
        }

        private static async Task RemoveGroupEnhancedHelper(ToastNotificationHistory history, string group)
        {
            await ToastHistoryChangeDatabase.RemoveGroup(group);

            history.RemoveGroup(group);
        }
    }
}
