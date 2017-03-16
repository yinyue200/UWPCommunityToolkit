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

namespace UnitTests.Notifications
{
    [TestClass]
    public class ToastHistoryChangeTrackerInvalidStateTests
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
            await ToastHistoryChangeTracker.Current.ResetAsync();
            await ToastHistoryChangeTracker.Current.EnableAsync();
            
            // Delete the database file so that we're in an invalid state
            foreach (var file in await ApplicationData.Current.LocalCacheFolder.GetFilesAsync())
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

            // And clear the cached item
            ToastHistoryChangeDatabase.ClearCache();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            // Clear the cached item
            ToastHistoryChangeDatabase.ClearCache();

            await AssertChangeTrackingLost();
        }

        [TestMethod]
        public async Task TestChangeTrackingLost()
        {
            // Let's say we have a notification
            Push("Cool", "1");

            var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
            var changes = await reader.ReadChangesAsync();

            // We should be in the invalid state
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.ChangeTrackingLost, changes[0].ChangeType);

            // Accepting change shouldn't throw any exceptions
            await reader.AcceptChangesAsync();

            // We should still get the same change tracking lost after accepting
            await AssertChangeTrackingLost();

            // And then we should be able to call Reset to fix the issue
            await ToastHistoryChangeTracker.Current.ResetAsync();

            // And then we should be able to get changes successfully
            // and there should be zero changes
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            // Now dismiss
            Dismiss("1");

            // We should know about that dismissal
            changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.Removed, changes[0].ChangeType);
            Assert.AreEqual("1", changes[0].Tag);

            // Delete the database file so that we're in an invalid state (cleanup checks this)
            foreach (var file in await ApplicationData.Current.LocalCacheFolder.GetFilesAsync())
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        [TestMethod]
        public async Task TestShow()
        {
            await Show("Cool", "1");

            // The notification should have shown even though the state is still invalid
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

        private async Task AssertChangeTrackingLost()
        {
            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.ChangeTrackingLost, changes[0].ChangeType);
        }
    }
}
