using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Notifications;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    public static class ToastNotifierExtensions
    {
        /// <summary>
        /// Displays the specified Toast notification.
        /// </summary>
        /// <param name="toastNotifier"></param>
        /// <param name="notification"></param>
        public static IAsyncAction ShowEnhanced(this ToastNotifier toastNotifier, ToastNotification notification, string additionalData = null)
        {
            return ToastHistoryChangeDatabase.ShowToastNotification(toastNotifier, notification, additionalData).AsAsyncAction();
        }

        public static IAsyncAction AddToScheduleEnhanced(this ToastNotifier toastNotifier, ScheduledToastNotification scheduledToast, string additionalData = null)
        {
            return ToastHistoryChangeDatabase.AddScheduledToastNotification(toastNotifier, scheduledToast, additionalData).AsAsyncAction();
        }

        /// <summary>
        /// Cancels the scheduled display of a specified <see cref="ScheduledToastNotification"/>.
        /// </summary>
        /// <param name="toastNotifier"></param>
        /// <param name="scheduledToast"></param>
        public static IAsyncAction RemoveFromScheduleEnhanced(this ToastNotifier toastNotifier, ScheduledToastNotification scheduledToast)
        {
            return ToastHistoryChangeDatabase.RemoveScheduled(toastNotifier, scheduledToast).AsAsyncAction();
        }
    }
}
