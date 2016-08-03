using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using static UnitTests.Notifications.ToastHistoryChangeTrackerTestHelpers;

namespace UnitTests.Notifications
{
    [TestClass]
    public class ToastHistoryChangeTrackerTests
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
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            // Accept the changes
            var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
            await reader.AcceptChangesAsync();

            // And then make sure that we don't have any items being returned
            Assert.AreEqual(0, (await GetChangesAsync()).Count);
        }

        [TestMethod]
        public async Task TestAdd()
        {
            await Show("Testing", "1");

            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);
        }

        [TestMethod]
        public async Task TestAddAndRemove()
        {
            await Show("Testing", "1");
            await ToastNotificationManager.History.RemoveEnhanced("1");
            Assert.AreEqual(0, (await GetChangesAsync()).Count);
        }

        [TestMethod]
        public async Task TestAddAndExpire()
        {
            var notif = CreateToast("Testing", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);

            await Show(notif);

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            var change = changes.FirstOrDefault();
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, change.ChangeType);
        }

        [TestMethod]
        public async Task TestAddAndReplaceAndExpire()
        {
            await Show("First", "1");
            var notif = CreateToast("Replaced", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "Replaced");
            Assert.AreEqual(0, (await GetChangesAsync()).Count);

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);

            var change = changes.FirstOrDefault();
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, change.ChangeType);
            Assert.AreEqual("Replaced", change.AdditionalData);
        }

        [TestMethod]
        public async Task TestAddAndExpireAndReAddAndExpire()
        {
            var notif = CreateToast("First", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "First");

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            var change = changes.First();
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, change.ChangeType);

            notif = CreateToast("Replaced", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "Replaced");

            await Task.Delay(2000);

            changes = await GetChangesAsync();
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[1].ChangeType);
            Assert.AreEqual("First", changes[0].AdditionalData);
            Assert.AreEqual("Replaced", changes[1].AdditionalData);
        }

        [TestMethod]
        public async Task TestAddAndExpireAndReAddAndRemove()
        {
            // In this scenario, the first dismiss (expire) should be preserved,
            // but the remove shouldn't appear anywhere since that was a dev action

            var notif = CreateToast("First", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "First");

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            var change = changes.First();
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, change.ChangeType);

            notif = CreateToast("Replaced", "1");
            await Show("Replaced", "1");

            await ToastNotificationManager.History.RemoveEnhanced("1");
            
            changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("First", changes[0].AdditionalData);
        }

        [TestMethod]
        public async Task TestSchedule()
        {
            await Schedule("Scheduled", "1", DateTime.Now.AddSeconds(1));

            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Task.Delay(2000);

            // After it has shown, changes should still be zero
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count());

            // Unfortunately can't test expired, since ScheduledToast is missing Expired property
        }

        [TestMethod]
        public async Task TestScheduleAndUnschedule()
        {
            // Schedule it
            var scheduled = await Schedule("Scheduled", "1", DateTime.Now.AddSeconds(1));

            // Then remove it
            await ToastNotificationManager.CreateToastNotifier().RemoveFromScheduleEnhanced(scheduled);

            // There should be zero changes immediately
            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            // And also zero changes after the original scheduled time occurs
            await Task.Delay(2000);
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count());
        }

        [TestMethod]
        public async Task TestAddPush()
        {
            // Show toast using non-Enhanced method so it's like a push
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push", "1");
            notifier.Show(notif);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual("1", changes[0].Tag);
            Assert.AreEqual(ToastHistoryChangeType.AddedViaPush, changes[0].ChangeType);
        }

        [TestMethod]
        public async Task TestAddPushAndReplaceViaPush()
        {
            // Show toast using non-Enhanced method so it's like a push
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push", "1");
            notifier.Show(notif);

            // Call GetChanges so that it processes the "push" toast
            await GetChangesAsync();

            // Replace
            notifier.Show(CreateToast("Push Replace", "1"));

            var changes = await GetChangesAsync();
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual("1", changes[0].Tag);
            Assert.AreEqual(ToastHistoryChangeType.ReplacedViaPush, changes[0].ChangeType);
            Assert.AreEqual("1", changes[1].Tag);
            Assert.AreEqual(ToastHistoryChangeType.AddedViaPush, changes[1].ChangeType);
        }

        [TestMethod]
        public async Task TestAddPushAndExpire()
        {
            // Show toast using non-Enhanced method so it's like a push
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            notifier.Show(notif);

            // GetChanges so that it processes the "push" toast
            await GetChangesAsync();

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("1", changes[0].Tag);
        }

        [TestMethod]
        public async Task TestAddPushAndExpireAndReAddAndExpire()
        {
            await TestAddPushAndExpire();

            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push Re-Add", "1");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            notifier.Show(notif);

            var changes = await GetChangesAsync();
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("1", changes[0].Tag);
            Assert.AreEqual(ToastHistoryChangeType.AddedViaPush, changes[1].ChangeType);
            Assert.AreEqual("1", changes[1].Tag);

            await Task.Delay(2000);

            changes = await GetChangesAsync();
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("1", changes[0].Tag);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[1].ChangeType);
            Assert.AreEqual("1", changes[1].Tag);
        }
    }
}
