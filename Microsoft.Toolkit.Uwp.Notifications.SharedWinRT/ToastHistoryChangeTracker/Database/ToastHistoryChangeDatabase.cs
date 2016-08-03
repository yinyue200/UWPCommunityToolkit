using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Platform.WinRT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    internal static class ToastHistoryChangeDatabase
    {
        private const string DATABASE_FILE_NAME = "ToastHistoryChangeDatabase.db";
        private const string IS_IN_GOOD_STATE_SETTINGS_KEY = "ToastHistoryChangeIsInGoodState";

        private static bool GetIsInGoodState()
        {
            return ApplicationData.Current.LocalSettings.Values.ContainsKey(IS_IN_GOOD_STATE_SETTINGS_KEY);
        }

        private static void SetIsInGoodState(bool isInGoodState)
        {
            if (isInGoodState)
            {
                ApplicationData.Current.LocalSettings.Values[IS_IN_GOOD_STATE_SETTINGS_KEY] = true;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values.Remove(IS_IN_GOOD_STATE_SETTINGS_KEY);
            }
        }

        /// <summary>
        /// Returns true if the notification has a tag (which means we can track it)
        /// </summary>
        /// <param name="notif"></param>
        /// <returns></returns>
        internal static bool SupportsTracking(ToastNotification notif)
        {
            return notif.Tag.Length > 0;
        }

        internal static void EnsureHasTag(ToastNotification notif)
        {
            if (notif.Tag.Length == 0)
                notif.Tag = GetAutoTag();
        }

        internal static void EnsureHasTag(ScheduledToastNotification notif)
        {
            if (notif.Tag.Length == 0)
                notif.Tag = GetAutoTag();
        }

        private static string GetAutoTag()
        {
            return Guid.NewGuid().GetHashCode().ToString();
        }

        internal static void PopulateRecord(ToastHistoryChangeRecord record, ToastNotification notif, string additionalData)
        {
            record.ToastTag = notif.Tag;
            record.ToastGroup = notif.Group;
            if (notif.ExpirationTime != null)
            {
                record.ExpirationTime = notif.ExpirationTime.Value;
            }

            // Store payload if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayload)
            {
                record.Payload = notif.Content.GetXml();
            }

            // Pull out arguments if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayloadArguments)
            {
                record.PayloadArguments = GetPayloadArguments(notif.Content);
            }

            record.AdditionalData = additionalData;
        }

        internal static void PopulateRecord(ToastHistoryChangeRecord record, ScheduledToastNotification notif, string additionalData)
        {
            record.ToastTag = notif.Tag;
            record.ToastGroup = notif.Group;
            record.ExpirationTime = DateTimeOffset.MaxValue;

            // Store payload if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayload)
            {
                record.Payload = notif.Content.GetXml();
            }

            // Pull out arguments if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayloadArguments)
            {
                record.PayloadArguments = GetPayloadArguments(notif.Content);
            }

            record.AdditionalData = additionalData;
        }

        private static string GetPayloadArguments(XmlDocument content)
        {
            var toastNode = content.SelectSingleNode("/toast");
            if (toastNode != null)
            {
                var launchAttr = toastNode.Attributes.FirstOrDefault(i => i.LocalName.Equals("launch"));
                if (launchAttr != null)
                {
                    return launchAttr.InnerText;
                }
            }

            return string.Empty;
        }

        internal static Task AddToastNotification(ToastNotification notif, string additionalData)
        {
            return Execute((conn) =>
            {
                // Make sure it has a tag (adds one if doesn't)
                ToastHistoryChangeDatabase.EnsureHasTag(notif);

                // Populate the record entry
                ToastHistoryChangeRecord record = new Notifications.ToastHistoryChangeRecord()
                {
                    DateAdded = DateTimeOffset.Now,
                    Status = ToastHistoryChangeRecordStatus.Committed
                };
                ToastHistoryChangeDatabase.PopulateRecord(record, notif, additionalData);

                // Delete any other existing, since the programmatic add replaces
                // We check DateAdded to preserve scheduled notifications
                conn.Execute(
                    @"delete from ToastHistoryChangeRecord
                    where ToastTag = ? and ToastGroup = ? and DateAdded <= ?",
                    notif.Tag,
                    notif.Group,
                    record.DateAdded);

                // And then insert this
                conn.Insert(record);
            });
        }

        public static Task AddScheduledToastNotification(ScheduledToastNotification notif, string additionalData)
        {
            return Execute((conn) =>
            {
                // Make sure it has a tag (adds one if doesn't)
                ToastHistoryChangeDatabase.EnsureHasTag(notif);

                // Populate the record entry
                ToastHistoryChangeRecord record = new ToastHistoryChangeRecord()
                {
                    DateAdded = notif.DeliveryTime,
                    Status = ToastHistoryChangeRecordStatus.Committed
                };
                PopulateRecord(record, notif, additionalData);

                conn.Insert(record);
            });
        }

        public static Task Remove(string tag, string group)
        {
            return Execute((conn) =>
            {
                conn.Execute($@"delete from ToastHistoryChangeRecord where ToastTag = ? and ToastGroup = ? and DateAdded <= ?", tag, group, DateTimeOffset.Now);
            });
        }

        public static Task RemoveGroup(string group)
        {
            return Execute((conn) =>
            {
                conn.Execute($@"delete from ToastHistoryChangeRecord where ToastGroup = ? and DateAdded <= ?", group, DateTimeOffset.Now);
            });
        }

        public static Task Clear()
        {
            return Execute((conn) =>
            {
                conn.Execute("delete from ToastHistoryChangeRecord where DateAdded <= ?", DateTimeOffset.Now);
            });
        }

        public static Task RemoveScheduled(string tag, string group, DateTimeOffset deliveryTime)
        {
            return Execute((conn) =>
            {
                conn.Execute(
                    $"delete from ToastHistoryChangeRecord where ToastTag = ? and ToastGroup = ? and DateAdded = ?",
                    tag,
                    group,
                    deliveryTime);
            });
        }

        private static SQLiteConnection CreateConnection(bool enabling = false)
        {
            SQLiteConnection conn = null;

            try
            {
                conn = new SQLiteConnection(

                    // Provide the platform (WinRT)
                    sqlitePlatform: new SQLitePlatformWinRT(),

                    // Provide the full path where you want the database stored
                    databasePath: Path.Combine(ApplicationData.Current.LocalFolder.Path, DATABASE_FILE_NAME)

                    );

                // Ensure the table exists in all cases except enabling case
                // This will make sure that if the database was randomly deleted,
                // we don't silently continue unaware that the database was deleted
                if (!enabling)
                {
                    conn.ExecuteScalar<int>("select count(*) from ToastHistoryChangeRecord");
                }

                // Creates table (if already exists, does nothing)
                conn.CreateTable<ToastHistoryChangeRecord>();
            }
            catch
            {
                if (conn != null)
                {
                    conn.Dispose();
                }
            }

            return conn;
        }

        /// <summary>
        /// Compares the current Toast notifications in order to obtain what has changed
        /// </summary>
        /// <returns></returns>
        public static Task SyncWithPlatformAsync()
        {
            DateTimeOffset now = DateTimeOffset.Now;

            return Execute((conn) =>
            {
                SyncWithPlatformHelper(now, conn);
            });
        }

        private static void SyncWithPlatformHelper(DateTimeOffset now, SQLiteConnection conn)
        {
            // Get all current notifications from the platform
            var notifs = ToastNotificationManager.History.GetHistory().Where(i => i.Tag.Length > 0);

            var toBeAdded = new List<ToastNotification>();

            // HMM TODO - we should probably manage dupes while inserting... then we can make the updating a lot more efficient too
            // for when we support expire

            // The only time we'll have dupes is from scheduled notifications that finally "popped"
            // (their DateAdded is finally less than current time)
            //
            // Obtain the notifications that we have stored
            // We skip notifications that have a DateAdded of future values, since
            // those are scheduled ones that haven't appeared yet.
            List<QuickRecord> storedNotifs = conn.Query<QuickRecord>(
                "select UniqueId, ToastTag, ToastGroup, DateAdded, ExpirationTime, Status from ToastHistoryChangeRecord where DateAdded <= ? and Status != ?",
                now,
                ToastHistoryChangeRecordStatus.DismissedByUser)
                .ToList();

            // Find and remove the dupes
            List<QuickRecord> dupes = new List<QuickRecord>();
            for (int i = 0; i < storedNotifs.Count; i++)
            {
                var curr = storedNotifs[i];
                for (int x = i + 1; x < storedNotifs.Count; x++)
                {
                    var next = storedNotifs[x];
                    if (next.ToastTag.Equals(curr.ToastTag) && next.ToastGroup.Equals(curr.ToastGroup))
                    {
                        // If the current is newer, we'll preserve it
                        if (curr.DateAdded > next.DateAdded)
                        {
                            dupes.Add(next);
                            storedNotifs.RemoveAt(x);
                        }
                        else
                        {
                            // Otherwise we preseve the next one
                            dupes.Add(curr);
                            storedNotifs.RemoveAt(i);
                            i--;
                        }
                        break;
                    }
                }
            }

            var toBeRemoved = storedNotifs.ToList();

            // For each notification in the platform
            foreach (var notif in notifs)
            {
                // If we have this notification
                var existing = storedNotifs.FirstOrDefault(i => i.ToastTag.Equals(notif.Tag) && i.ToastGroup.Equals(notif.Group));
                if (existing != null)
                {
                    // Wek want to KEEP it in the database, so take it out of
                    // the list of notifications to remove
                    toBeRemoved.Remove(existing);
                }

                // Otherwise it's new (added via push)
                else
                {
                    toBeAdded.Add(notif);
                }
            }

            if (toBeAdded.Count > 0 || toBeRemoved.Count > 0 || dupes.Count > 0)
            {
                conn.RunInTransaction(() =>
                {
                    // We want to delete any of the dupes
                    List<long> uniqueIdsToDelete = new List<long>(dupes.Select(i => i.UniqueId));

                    // And we also delete any that were previously adds via push
                    // since if the app previously didn't accept that change, we
                    // don't even inform the app that the push was ever received (since it
                    // already got dismissed).
                    uniqueIdsToDelete.AddRange(toBeRemoved.Where(i => i.Status == ToastHistoryChangeRecordStatus.AddedViaPush).Select(i => i.UniqueId));

                    // Remove these items
                    if (uniqueIdsToDelete.Count > 0)
                    {
                        object[] args = uniqueIdsToDelete.OfType<object>().ToArray();
                        conn.Execute("delete from ToastHistoryChangeRecord where UniqueId " + In(uniqueIdsToDelete));
                    }

                    // See which were expired
                    long[] uniqueIdsToChangeToExpired = toBeRemoved
                        .Where(i => i.ExpirationTime <= now)
                        .Select(i => i.UniqueId)
                        .ToArray();
                    if (uniqueIdsToChangeToExpired.Length > 0)
                    {
                        conn.Execute(
                            $@"update ToastHistoryChangeRecord
                            set Status = {(int)ToastHistoryChangeRecordStatus.Expired}, DateRemoved = ?
                            where UniqueId " + In(uniqueIdsToChangeToExpired),
                            now);
                    }

                    // And everything else were just dismissed
                    long[] uniqueIdsToChangeToDismissed = toBeRemoved
                        .Where(i => i.ExpirationTime > now)
                        .Select(i => i.UniqueId)
                        .ToArray();
                    if (uniqueIdsToChangeToDismissed.Length > 0)
                    {
                        conn.Execute(
                            $@"update ToastHistoryChangeRecord
                            set Status = {(int)ToastHistoryChangeRecordStatus.DismissedByUser}, DateRemoved = ?
                            where UniqueId " + In(uniqueIdsToChangeToDismissed),
                            now);
                    }

                    // And add the new ones
                    var newRecords = new List<ToastHistoryChangeRecord>(toBeAdded.Count);
                    foreach (var newNotif in toBeAdded)
                    {
                        // Populate the record entry for it
                        var record = new ToastHistoryChangeRecord()
                        {
                            DateAdded = now,
                            Status = ToastHistoryChangeRecordStatus.AddedViaPush
                        };
                        PopulateRecord(record, newNotif, null);
                        newRecords.Add(record);
                    }

                    if (newRecords.Count > 0)
                    {
                        conn.InsertAll(newRecords, runInTransaction: false);
                    }
                });
            }
        }

        private static string In(IEnumerable<long> uniqueIds)
        {
            return "in (" + string.Join(",", uniqueIds) + ")";
        }

        private static string In(int count)
        {
            // Does not support 0, must be 1 or greater
            StringBuilder builder = new StringBuilder();
            builder.Append("in (");

            // Skip the first, since we'll add that at the end
            for (int i = 1; i < count; i++)
            {
                builder.Append("?,");
            }

            // Now add the first at the end
            builder.Append("?)");

            return builder.ToString();
        }

        /// <summary>
        /// Does not sort.
        /// </summary>
        /// <returns></returns>
        public static Task<ToastHistoryChangeRecord[]> GetChangesAsync()
        {
            DateTimeOffset now = DateTimeOffset.Now;

            return Execute((conn) =>
            {
                SyncWithPlatformHelper(now, conn);

                // No need to check DateAdded since any scheduled ones that haven't popped
                // yet will still be marked Committed
                return conn.Table<ToastHistoryChangeRecord>()
                    .Where(i => i.Status != ToastHistoryChangeRecordStatus.Committed)
                    .ToArray();
            });
        }

        public static Task AcceptChangesAsync(IEnumerable<ToastHistoryChangeRecord> changes)
        {
            // These are the potential paths of states (cannot flow backwards)

            // AddedViaPush -> Committed -> Deleted (dismissed)
            // Committed (added locally) -> Deleted (dismissed)
            return Execute((conn) =>
            {
                conn.RunInTransaction(() =>
                {
                    // For dismissals or expires, the only action is to delete from the database
                    long[] uniqueIdsToDelete = changes
                        .Where(i => i.Status == ToastHistoryChangeRecordStatus.DismissedByUser || i.Status == ToastHistoryChangeRecordStatus.Expired)
                        .Select(i => i.UniqueId)
                        .ToArray();
                    if (uniqueIdsToDelete.Length > 0)
                    {
                        conn.Execute("delete from ToastHistoryChangeRecord where UniqueId " + In(uniqueIdsToDelete));
                    }

                    // For AddedViaPush, we move it to the Committed state
                    long[] uniqueIdsToCommit = changes
                        .Where(i => i.Status == ToastHistoryChangeRecordStatus.AddedViaPush)
                        .Select(i => i.UniqueId)
                        .ToArray();
                    if (uniqueIdsToCommit.Length > 0)
                    {
                        conn.Execute(
                            $@"update ToastHistoryChangeRecord
                        set Status = {(int)ToastHistoryChangeRecordStatus.Committed}
                        where UniqueId " + In(uniqueIdsToCommit));
                    }
                });
            });
        }

        private static Task Execute(Action<SQLiteConnection> action)
        {
            return Execute<bool>((conn) =>
            {
                action(conn);
                return true;
            });
        }

        private static Task<T> Execute<T>(Func<SQLiteConnection, T> action)
        {
            try
            {
                // If we're not in a good state, we simply stop executing
                if (!GetIsInGoodState())
                {
                    return Task.FromResult(default(T));
                }

                return Task.Run(async delegate
                {
                    // We'll just always use a write lock, since 90% of the time we're always writing
                    // (even when reading, we'll be potentially creating the database for the first time)
                    using (Locks.LockForWrite())
                    {
                        // Initialize settings
                        await ToastHistoryChangeTrackerConfiguration.InitializeAsync();

                        // We'll also check after we've established lock
                        if (!GetIsInGoodState())
                        {
                            return default(T);
                        }

                        using (var conn = CreateConnection())
                        {
                            return action.Invoke(conn);
                        }
                    }
                });
            }

            catch
            {
                SetIsInGoodState(false);
                return Task.FromResult(default(T));
            }
        }

        private static async Task DeleteDatabaseAsync()
        {
            try
            {
                await (await ApplicationData.Current.LocalFolder.GetFileAsync(DATABASE_FILE_NAME)).DeleteAsync();
            }

            catch { }
        }

        private static void CreateDatabase()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            var notifs = ToastNotificationManager.History.GetHistory();
            var scheduledNotifs = ToastNotificationManager.CreateToastNotifier().GetScheduledToastNotifications();

            using (var conn = CreateConnection(enabling: true))
            {
                // We populate it with the initial toast notifications
                var newRecords = new List<ToastHistoryChangeRecord>(notifs.Count);
                foreach (var newNotif in notifs.Where(i => i.Tag.Length > 0))
                {
                    // Populate the record entry for it
                    var record = new ToastHistoryChangeRecord()
                    {
                        DateAdded = now,
                        Status = ToastHistoryChangeRecordStatus.Committed
                    };
                    PopulateRecord(record, newNotif, null);
                    newRecords.Add(record);
                }

                // And also the scheduled notifications
                foreach (var scheduled in scheduledNotifs.Where(i => i.Tag.Length > 0))
                {
                    var record = new ToastHistoryChangeRecord()
                    {
                        DateAdded = scheduled.DeliveryTime,
                        Status = ToastHistoryChangeRecordStatus.Committed
                    };
                    PopulateRecord(record, scheduled, null);
                    newRecords.Add(record);
                }

                conn.InsertAll(newRecords);
            }

            SetIsInGoodState(true);
        }

        public static Task EnableAsync()
        {
            return Task.Run(async delegate
            {
                using (Locks.LockForWrite())
                {
                    if (GetIsInGoodState())
                    {
                        return;
                    }

                    // Delete first
                    await DeleteDatabaseAsync();

                    // Then re-create
                    CreateDatabase();
                }
            });
        }

        public static Task ResetAsync()
        {
            return Task.Run(async delegate
            {
                using (Locks.LockForWrite())
                {
                    // Delete first
                    await DeleteDatabaseAsync();

                    // Then re-create
                    CreateDatabase();
                }
            });
        }

        internal class QuickRecord
        {
            public long UniqueId { get; set; }

            public string ToastTag { get; set; }

            public string ToastGroup { get; set; }

            public DateTimeOffset DateAdded { get; set; }

            public DateTimeOffset ExpirationTime { get; set; }

            public ToastHistoryChangeRecordStatus Status { get; set; }
        }
    }
}
