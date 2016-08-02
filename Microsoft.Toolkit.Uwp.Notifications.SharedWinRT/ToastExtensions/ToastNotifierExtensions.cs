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
            return ShowEnhancedHelper(toastNotifier, notification, additionalData).AsAsyncAction();
        }

        private static async Task ShowEnhancedHelper(ToastNotifier toastNotifier, ToastNotification notification, string additionalData)
        {
            // Add the record to the database
            await ToastHistoryChangeDatabase.AddToastNotification(notification, additionalData);

            toastNotifier.Show(notification);
        }

        public static IAsyncAction AddToScheduleEnhanced(this ToastNotifier toastNotifier, ScheduledToastNotification scheduledToast, string additionalData = null)
        {
            return AddToScheduleEnhancedHelper(toastNotifier, scheduledToast, additionalData).AsAsyncAction();
        }

        private static async Task AddToScheduleEnhancedHelper(ToastNotifier toastNotifier, ScheduledToastNotification scheduledToast, string additionalData)
        {
            // Add the record to the database
            await ToastHistoryChangeDatabase.AddScheduledToastNotification(scheduledToast, additionalData);

            toastNotifier.AddToSchedule(scheduledToast);
        }

        /// <summary>
        /// Cancels the scheduled display of a specified <see cref="ScheduledToastNotification"/>.
        /// </summary>
        /// <param name="toastNotifier"></param>
        /// <param name="scheduledToast"></param>
        public static IAsyncAction RemoveFromScheduleEnhanced(this ToastNotifier toastNotifier, ScheduledToastNotification scheduledToast)
        {
            return RemoveFromScheduleEnhancedHelper(toastNotifier, scheduledToast).AsAsyncAction();
        }

        private static async Task RemoveFromScheduleEnhancedHelper(ToastNotifier toastNotifier, ScheduledToastNotification scheduledToast)
        {
            // If hasn't appeared yet, remove from our database
            // Otherwise we don't remove, since the RemoveFromSchedule won't remove the toast if it's
            // already appeared. It only removes toasts that haven't appeared yet.
            if (scheduledToast.DeliveryTime > DateTime.Now)
            {
                await ToastHistoryChangeDatabase.RemoveScheduled(scheduledToast.Tag, scheduledToast.Group, scheduledToast.DeliveryTime);
            }

            // Remove from the platform schedule
            toastNotifier.RemoveFromSchedule(scheduledToast);
        }
    }
}
