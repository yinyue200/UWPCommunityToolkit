using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Notifications;
using static UnitTests.Notifications.ToastHistoryChangeTrackerTestHelpers;

namespace UnitTests.Notifications.UWP
{
    [TestClass]
    public class ToastHistoryChangeTrackerNotEnabledTests
    {
        [TestInitialize]
        public async Task Initialize()
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();
            foreach (var scheduled in notifier.GetScheduledToastNotifications())
            {
                notifier.RemoveFromSchedule(scheduled);
            }
            ToastNotificationManager.History.Clear();
            // Remove any files so that change tracker is reset to default non-enabled state
            foreach (var file in await ApplicationData.Current.LocalCacheFolder.GetFilesAsync())
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

            ApplicationData.Current.LocalSettings.Values.Clear();
        }

        [TestMethod]
        public async Task TestShow()
        {
            await Show("Cool", "1");

            // The notification should have shown even though not enabled
            var history = GetHistory();
            Assert.AreEqual(1, history.Count);
        }

        [TestMethod]
        public async Task TestRemove()
        {
            Push("Cool", "1");

            await ToastNotificationManager.History.RemoveEnhanced("1");

            Assert.AreEqual(0, GetHistory().Count);
        }

        [TestMethod]
        public async Task TestRemoveWithGroup()
        {
            Push("Cool", "1", "a");

            await ToastNotificationManager.History.RemoveEnhanced("1", "a");

            Assert.AreEqual(0, GetHistory().Count);
        }

        [TestMethod]
        public async Task TestRemoveGroup()
        {
            Push("First", "1", "a");
            Push("Second", "2", "a");
            Push("Third", "1", "b");

            await ToastNotificationManager.History.RemoveGroupEnhanced("a");

            Assert.AreEqual(1, GetHistory().Count);
        }

        [TestMethod]
        public async Task TestClear()
        {
            Push("First", "1", "a");
            Push("Second", "2", "a");
            Push("Third", "1", "b");

            await ToastNotificationManager.History.ClearEnhanced();

            Assert.AreEqual(0, GetHistory().Count);
        }

        [TestMethod]
        public async Task TestSchedule()
        {
            var notif = new ScheduledToastNotification(CreateToastContent("Cool"), DateTimeOffset.Now.AddMinutes(5))
            {
                Tag = "1"
            };

            await ToastNotificationManager.CreateToastNotifier().AddToScheduleEnhanced(notif);

            var scheduled = ToastNotificationManager.CreateToastNotifier().GetScheduledToastNotifications();
            Assert.AreEqual(1, scheduled.Count);
        }

        [TestMethod]
        public async Task TestRemoveFromSchedule()
        {
            var notif = new ScheduledToastNotification(CreateToastContent("Cool"), DateTimeOffset.Now.AddMinutes(5))
            {
                Tag = "1"
            };

            ToastNotificationManager.CreateToastNotifier().AddToSchedule(notif);

            await ToastNotificationManager.CreateToastNotifier().RemoveFromScheduleEnhanced(notif);

            var scheduled = ToastNotificationManager.CreateToastNotifier().GetScheduledToastNotifications();
            Assert.AreEqual(0, scheduled.Count);
        }
    }
}
