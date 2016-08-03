using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UnitTests.Notifications
{
    public static class ToastHistoryChangeTrackerTestHelpers
    {
        public static XmlDocument CreateToastContent(string content)
        {
            return new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = content
                            }
                        }
                    }
                }
            }.GetXml();
        }

        public static ToastNotification CreateToast(string content, string tag)
        {
            return new ToastNotification(CreateToastContent(content))
            {
                Tag = tag
            };
        }

        public static ScheduledToastNotification CreateScheduled(string content, string tag, DateTimeOffset deliveryTime)
        {
            return new ScheduledToastNotification(CreateToastContent(content), deliveryTime)
            {
                Tag = tag
            };
        }

        public static async Task Show(string content, string tag)
        {
            var notif = CreateToast(content, tag);
            await Show(notif, content);
        }

        public static async Task Show(ToastNotification notif, string additionalData = null)
        {
            await ToastNotificationManager.CreateToastNotifier().ShowEnhanced(notif, additionalData);
        }

        public static async Task<ScheduledToastNotification> Schedule(string content, string tag, DateTimeOffset deliveryTime)
        {
            var notif = CreateScheduled(content, tag, deliveryTime);
            await ToastNotificationManager.CreateToastNotifier().AddToScheduleEnhanced(notif, content);
            return notif;
        }

        public static async Task<IList<ToastHistoryChange>> GetChangesAsync()
        {
            var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
            return await reader.ReadChangesAsync();
        }

        public static void Dismiss(string tag)
        {
            ToastNotificationManager.History.Remove(tag);
        }

        public static async Task Finish()
        {
            // Accept the changes
            var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
            await reader.AcceptChangesAsync();

            // And then make sure that we don't have any items being returned
            Microsoft.VisualStudio.TestPlatform.UnitTestFramework.Assert.AreEqual(0, (await GetChangesAsync()).Count);
        }
    }
}
