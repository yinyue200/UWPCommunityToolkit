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

        [TestMethod]
        public async Task TestAdd()
        {
            await Show("Testing", "tag");

            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddAndRemove()
        {
            await Show("Testing", "tag");
            await ToastNotificationManager.History.RemoveEnhanced("tag");
            Assert.AreEqual(0, (await GetChangesAsync()).Count);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddAndExpire()
        {
            var notif = CreateToast("Testing", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);

            DateTimeOffset beforeAdd = DateTimeOffset.Now;
            await Show(notif);
            DateTimeOffset afterAdd = DateTimeOffset.Now;

            DateTimeOffset beforeDismiss = DateTimeOffset.Now;

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            var change = changes.FirstOrDefault();
            Assert.AreEqual(ToastHistoryChangeType.Expired, change.ChangeType);
            Assert.AreEqual("tag", changes[0].Tag);
            AssertIsInRange(beforeAdd, changes[0].DateAdded, afterAdd);
            AssertIsInRange(beforeDismiss, changes[0].DateRemoved, DateTimeOffset.Now);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddAndDismiss()
        {
            DateTimeOffset beforeAdd = DateTimeOffset.Now;
            await Show("Testing", "tag");
            DateTimeOffset afterAdd = DateTimeOffset.Now;

            DateTimeOffset beforeDismiss = DateTimeOffset.Now;
            Dismiss("tag");

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("tag", changes[0].Tag);
            Assert.AreEqual("Testing", changes[0].AdditionalData);
            Assert.IsTrue(changes[0].DateAdded >= beforeAdd && changes[0].DateAdded <= afterAdd);
            Assert.IsTrue(changes[0].DateRemoved >= beforeDismiss && changes[0].DateRemoved <= DateTimeOffset.Now);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddAndReplaceAndExpire()
        {
            await Show("First", "tag");
            var notif = CreateToast("Replaced", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "Replaced");
            Assert.AreEqual(0, (await GetChangesAsync()).Count);

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);

            var change = changes.FirstOrDefault();
            Assert.AreEqual(ToastHistoryChangeType.Expired, change.ChangeType);
            Assert.AreEqual("Replaced", change.AdditionalData);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddAndExpireAndReAddAndExpire()
        {
            var notif = CreateToast("First", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "First");

            await Task.Delay(2000);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.Expired, changes[0].ChangeType);

            notif = CreateToast("Replaced", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "Replaced");

            // There should be zero changes, since local add clears any previous
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Task.Delay(2000);

            // And now there should be one expired for the latest
            changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.Expired, changes[0].ChangeType);
            Assert.AreEqual("Replaced", changes[0].AdditionalData);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddAndExpireAndReAddAndRemove()
        {
            var notif = CreateToast("First", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            await Show(notif, "First");

            await Task.Delay(2000);

            // The dismiss initially appears
            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.Expired, changes[0].ChangeType);

            // And then we show again
            notif = CreateToast("Replaced", "tag");
            await Show("Replaced", "tag");

            // And then programmatic remove
            await ToastNotificationManager.History.RemoveEnhanced("tag");
            
            // There should be zero changes, since we did programmatic add/remove
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Finish();
        }

        [TestMethod]
        public async Task TestSchedule()
        {
            await Schedule("Scheduled", "tag", DateTime.Now.AddSeconds(1));

            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Task.Delay(2000);

            // After it has shown, changes should still be zero
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count());

            // Unfortunately can't test expired, since ScheduledToast is missing Expired property

            await Finish();
        }

        [TestMethod]
        public async Task TestScheduleAndUnschedule()
        {
            // Schedule it
            var scheduled = await Schedule("Scheduled", "tag", DateTime.Now.AddSeconds(1));

            // Then remove it
            await ToastNotificationManager.CreateToastNotifier().RemoveFromScheduleEnhanced(scheduled);

            // There should be zero changes immediately
            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            // And also zero changes after the original scheduled time occurs
            await Task.Delay(2000);
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count());

            await Finish();
        }

        [TestMethod]
        public async Task TestAddPush()
        {
            // Show toast using non-Enhanced method so it's like a push
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push", "tag");
            notifier.Show(notif);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual("tag", changes[0].Tag);
            Assert.AreEqual(ToastHistoryChangeType.AddedViaPush, changes[0].ChangeType);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddPushAndReplaceViaPush()
        {
            // Show toast using non-Enhanced method so it's like a push
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push", "tag");
            notifier.Show(notif);

            // Call GetChanges so that it processes the "push" toast
            await GetChangesAsync();

            // Replace
            notifier.Show(CreateToast("Push Replace", "tag"));

            // There should only be one change, since same tag/group was used
            // Appears as "Added" since dev hadn't committed previous one
            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual("tag", changes[0].Tag);
            Assert.AreEqual(ToastHistoryChangeType.AddedViaPush, changes[0].ChangeType);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddPushAndExpire()
        {
            // Show toast using non-Enhanced method so it's like a push
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            notifier.Show(notif);

            // GetChanges so that it processes the "push" toast
            await GetChangesAsync();

            await Task.Delay(2000);

            // There should be zero changes, since the dev never accepted the initial
            // change, so to the dev's perspective, the push never even occurred.
            // This is the same behavior as ContactChangeTracker, if a contact was added
            // and then removed without dev calling AcceptChanges before the remove occurred,
            // the dev is never notified of the subsequent remove.
            var changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Finish();
        }

        [TestMethod]
        public async Task TestAddPushAndExpireAndReAddAndExpire()
        {
            await TestAddPushAndExpire();

            var notifier = ToastNotificationManager.CreateToastNotifier();
            var notif = CreateToast("Push Re-Add", "tag");
            notif.ExpirationTime = DateTime.Now.AddSeconds(1);
            notifier.Show(notif);

            var changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.AddedViaPush, changes[0].ChangeType);
            Assert.AreEqual("tag", changes[0].Tag);

            await Task.Delay(2000);

            // Same explanation as previous scenario, dev shouldn't know about this change
            // since they didn't accept previous changes
            changes = await GetChangesAsync();
            Assert.AreEqual(0, changes.Count);

            await Finish();
        }

        [TestMethod]
        public async Task TestAcceptingChangesWhileNewChangesArrive()
        {
            // This scenario ensures that AcceptChanges only accepts the changes that
            // the reader itself has seen, and not any new changes that occurred after the
            // reader was created.
            await Show("First", "tag");
            Dismiss("tag");

            var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
            var changes = await reader.ReadChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);

            // Now new notif gets added
            await Show("Second", "tag");

            // Accepting the changes shouldn't mess anything up
            await reader.AcceptChangesAsync();

            // New notif gets dismissed
            Dismiss("tag");

            changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("Second", changes[0].AdditionalData);

            await Finish();
        }

        [TestMethod]
        public async Task TestAcceptingChangesWhileNewChangesArrive2()
        {
            // Another scenario testing a similar setup
            await Show("First", "tag");
            Dismiss("tag");

            var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
            var changes = await reader.ReadChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);

            // Now new notif gets added and dismissed
            await Show("Second", "tag");
            Dismiss("tag");

            // Get separate changes (should see the second change)
            await GetChangesAsync();
            changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("Second", changes[0].AdditionalData);

            // Now accept the old changes
            await reader.AcceptChangesAsync();

            // And now get changes again (we should still have the second change)
            changes = await GetChangesAsync();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ToastHistoryChangeType.DismissedByUser, changes[0].ChangeType);
            Assert.AreEqual("Second", changes[0].AdditionalData);

            await Finish();
        }
    }
}
