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

            // Store payload if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayload)
            {
                record.Payload = notif.Content.GetXml();
            }

            // Pull out arguments if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayloadArguments)
            {
                PopulatePayloadArguments(record, notif.Content);
            }

            record.AdditionalData = additionalData;
        }

        internal static void PopulateRecord(ToastHistoryChangeRecord record, ScheduledToastNotification notif, string additionalData)
        {
            record.ToastTag = notif.Tag;
            record.ToastGroup = notif.Group;

            // Store payload if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayload)
            {
                record.Payload = notif.Content.GetXml();
            }

            // Pull out arguments if we're doing that
            if (ToastHistoryChangeTrackerConfiguration.Current.IncludePayloadArguments)
            {
                PopulatePayloadArguments(record, notif.Content);
            }

            record.AdditionalData = additionalData;
        }

        private static void PopulatePayloadArguments(ToastHistoryChangeRecord record, XmlDocument content)
        {
            record.PayloadArguments = string.Empty;
            var toastNode = content.SelectSingleNode("/toast");
            if (toastNode != null)
            {
                var launchAttr = toastNode.Attributes.FirstOrDefault(i => i.LocalName.Equals("launch"));
                if (launchAttr != null)
                {
                    record.PayloadArguments = launchAttr.InnerText;
                }
            }
        }

        /// <summary>
        /// Used for programmatic adds (or removes/replaces)
        /// </summary>
        /// <param name="record">Record to add.</param>
        /// <returns>Async task.</returns>
        internal static Task AddRecord(ToastHistoryChangeRecord record)
        {
            return Execute((conn) =>
            {
                conn.Insert(record);
            });
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
                ToastHistoryChangeRecord record = new Notifications.ToastHistoryChangeRecord()
                {
                    DateAdded = notif.DeliveryTime,
                    Status = ToastHistoryChangeRecordStatus.Committed
                };
                ToastHistoryChangeDatabase.PopulateRecord(record, notif, additionalData);

                conn.Insert(record);
            });
        }

        public static Task Remove(string tag, string group)
        {
            return Execute((conn) =>
            {
                // Only remove the latest (preserve the previous ones)
                conn.Execute($@"delete from ToastHistoryChangeRecord where UniqueId = (select max(UniqueId) from ToastHistoryChangeRecord where ToastTag = ? and ToastGroup = ?)", tag, group);
            });
        }

        public static Task RemoveGroup(string group)
        {
            return Execute((conn) =>
            {
                conn.Execute($@"delete from ToastHistoryChangeRecord where ToastGroup = ?", group);
            });
        }

        public static Task Clear()
        {
            return Execute((conn) =>
            {
                conn.Execute("delete from ToastHistoryChangeRecord");
            });
        }

        public static Task RemoveScheduled(string tag, string group, DateTimeOffset deliveryTime)
        {
            return Execute((conn) =>
            {
                conn.Execute($@"delete from ToastHistoryChangeRecord where ToastTag = ? and ToastGroup = ? and DateAdded = ?",
                    tag, group,
                    deliveryTime);
            });
        }

        private static SQLiteConnection CreateConnection(bool enabling = false)
        {
            var conn = new SQLiteConnection(

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
            var notifs = ToastNotificationManager.History.GetHistory();

            var toBeAdded = new List<ToastNotification>();
            
            // Obtain the notifications that we have stored
            // We skip notifications that have a DateAdded of future values, since
            // those are scheduled ones that haven't appeared yet.
            List<JustTagAndGroup> storedNotifs = conn.Query<JustTagAndGroup>(
                "select distinct ToastTag, ToastGroup from ToastHistoryChangeRecord where DateAdded < ? and Status != ?", now, ToastHistoryChangeRecordStatus.DismissedByUser)
                .ToList();

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

            if (toBeAdded.Count > 0 || toBeRemoved.Count > 0)
            {
                try
                {
                    conn.BeginTransaction();

                    // Mark these as dismissed
                    foreach (var notif in toBeRemoved)
                    {
                        // Update the last record only
                        conn.Execute(@"update ToastHistoryChangeRecord set Status = ?, DateRemoved = ?
                        where UniqueId = (select max(UniqueId) from ToastHistoryChangeRecord where ToastTag = ? and ToastGroup = ?) and Status != ?",
                            ToastHistoryChangeRecordStatus.DismissedByUser,
                            now,
                            notif.ToastTag,
                            notif.ToastGroup,
                            ToastHistoryChangeRecordStatus.DismissedByUser);
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

                    // Notification could have been dismissed but not accepted by app yet,
                    // and then new push notification with same tag comes in. Hence we need to
                    // replace existing (or insert if no existing).
                    if (newRecords.Count > 0)
                    {
                        conn.InsertAll(newRecords);
                    }

                    conn.Commit();
                }
                catch
                {
                    conn.Rollback();
                    throw;
                }
            }
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

                return conn.Table<ToastHistoryChangeRecord>()
                    .Where(i => i.Status != ToastHistoryChangeRecordStatus.Committed && i.DateAdded <= now)
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
                var transactionPoint = conn.SaveTransactionPoint();

                try
                {
                    foreach (var acceptedChange in changes)
                    {
                        if (acceptedChange.Status == ToastHistoryChangeRecordStatus.AddedViaPush)
                        {
                            // For something that was AddedViaPush, the only next state is Committed,
                            // so don't even need to check whether the state isn't Committed
                            conn.Execute(@"update ToastHistoryChangeRecord
                            set Status = ?
                            where UniqueId = ?",
                                ToastHistoryChangeRecordStatus.Committed,
                                acceptedChange.UniqueId);
                        }
                        else if (acceptedChange.Status == ToastHistoryChangeRecordStatus.DismissedByUser)
                        {
                            // For something that was dismissed by user, the only next state is to delete the row
                            conn.Execute(@"delete from ToastHistoryChangeRecord
                            where UniqueId = ?",
                                acceptedChange.UniqueId);
                        }
                    }

                    conn.Commit();
                }

                catch
                {
                    conn.RollbackTo(transactionPoint);
                    throw;
                }
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

            using (var conn = CreateConnection(enabling: true))
            {
                // We populate it with the initial toast notifications
                var newRecords = new List<ToastHistoryChangeRecord>(notifs.Count);
                foreach (var newNotif in notifs)
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

        internal class JustTagAndGroup
        {
            public string ToastTag { get; set; }
            public string ToastGroup { get; set; }
        }
    }
}
