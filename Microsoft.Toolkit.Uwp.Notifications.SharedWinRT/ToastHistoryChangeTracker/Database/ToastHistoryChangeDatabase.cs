using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    [DataContract]
    internal class ToastHistoryChangeDatabase
    {
        private const string DATABASE_FILE_NAME = "ToastHistoryChangeDatabase.dat";
        private const string DATABASE_TEMP_FILE_NAME = "ToastHistoryChangeDatabaseTemp.dat";
        private const string IS_IN_GOOD_STATE_SETTINGS_KEY = "ToastHistoryChangeIsInGoodState";

        public List<ToastHistoryChangeRecord> Records { get; set; } = new List<ToastHistoryChangeRecord>();

        private ToastHistoryChangeDatabase() { }

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
        /// <param name="notif">The notification to check.</param>
        /// <returns>Boolean representing whether the notification has a tag.</returns>
        internal static bool SupportsTracking(ToastNotification notif)
        {
            return notif.Tag.Length > 0;
        }

        internal static void EnsureHasTag(ToastNotification notif)
        {
            if (notif.Tag.Length == 0)
            {
                notif.Tag = GetAutoTag();
            }
        }

        internal static void EnsureHasTag(ScheduledToastNotification notif)
        {
            if (notif.Tag.Length == 0)
            {
                notif.Tag = GetAutoTag();
            }
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

        internal static Task ShowToastNotification(ToastNotifier notifier, ToastNotification notif, string additionalData)
        {
            return ExecuteEnhanced(
                (db) =>
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
                db.Records.RemoveAll(
                    r =>
                    r.ToastTag.Equals(notif.Tag)
                    && r.ToastGroup.Equals(notif.Group)
                    && r.DateAdded <= record.DateAdded);

                // And then insert this
                db.Records.Add(record);

                db.SaveDatabaseWithoutWaiting();
            }, () =>
            {
                notifier.Show(notif);
            });
        }

        public static Task AddScheduledToastNotification(ToastNotifier notifier, ScheduledToastNotification notif, string additionalData)
        {
            if (!GetIsInGoodState())
            {
                notifier.AddToSchedule(notif);
                return Task.FromResult(true);
            }

            return ExecuteEnhanced(
                (db) =>
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

                db.Records.Add(record);

                db.SaveDatabaseWithoutWaiting();
            }, () =>
            {
                notifier.AddToSchedule(notif);
            });
        }

        public static Task Remove(ToastNotificationHistory history, string tag)
        {
            return ExecuteEnhanced(
                (db) =>
            {
                bool removed = db.Records.RemoveAll(
                    r =>
                    r.ToastTag.Equals(tag)
                    && r.ToastGroup.Length == 0
                    && r.DateAdded <= DateTimeOffset.Now) > 0;

                if (removed)
                {
                    db.SaveDatabaseWithoutWaiting();
                }
            }, () =>
            {
                history.Remove(tag);
            });
        }

        private static void ExecuteOriginalAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // We wrap this in a custom exception, so that we can know if the
                // original action failed compared to something else failing.
                throw new ToastHistoryChangeDatabaseOriginalActionException(ex);
            }
        }

        /// <summary>
        /// Handles checking if database is in good state and loading the database, and then performs the provided database action.
        /// Then, after that completes, the original action (like sending a toast) will be performed.
        /// This ensures that the original action will ALWAYS be executed, regardless of database corruption or database exceptions.
        /// If there is an exception in the original action (like invalid tag provided when showing a notification), that platform exception will be surfaced.
        /// </summary>
        /// <param name="databaseAction"></param>
        /// <param name="originalAction"></param>
        /// <returns></returns>
        private static async Task ExecuteEnhanced(Action<ToastHistoryChangeDatabase> databaseAction, Action originalAction)
        {
            // We wrap our original action so that if it throws, we can
            // identify that it specifically threw, whereas if anything else
            // throws, we should silently continue
            var copiedOriginalAction = originalAction;
            originalAction = delegate { ExecuteOriginalAction(copiedOriginalAction); };

            try
            {
                // If we're not in a good state, we simply stop executing
                if (!GetIsInGoodState())
                {
                    // But we need to execute the original action
                    originalAction();
                    return;
                }

                await Task.Run(async delegate
                {
                    // We'll just use a generic lock rather than a read/write lock, since 90% of the time we're always writing
                    // (even when reading, we'll be potentially creating the database for the first time)
                    using (await Locks.LockAsync())
                    {
                        // We'll also check after we've established lock
                        if (!GetIsInGoodState())
                        {
                            // But we need to execute the original action
                            // before we stop.
                            originalAction();
                            return;
                        }

                        // Initialize settings
                        await ToastHistoryChangeTrackerConfiguration.InitializeAsync();

                        ToastHistoryChangeDatabase db;

                        try
                        {
                            db = await GetDatabaseAsync();
                        }
                        catch (ToastHistoryDatabaseCorruptException)
                        {
                            // Set bad state
                            try
                            {
                                SetIsInGoodState(false);
                            }
                            catch { }

                            // If corrupt we still execute original action before we stop
                            originalAction();
                            return;
                        }

                        // Execute the database action
                        databaseAction(db);

                        // And then execute the original action
                        originalAction();
                    }
                });
            }

            // If the original platform action failed (like showing a toast), we throw the platform exception,
            // which means that nothing's wrong with the data state itself.
            // The platform exception should be surfaced to the developer.
            catch (ToastHistoryChangeDatabaseOriginalActionException ex)
            {
                throw ex.OriginalException;
            }

            // Otherwise something else failed, which means we're in a bad
            // state. Regardless, we need to execute the original action
            // and then we silently fail (they'll know something's wrong when
            // they read changes).
            catch
            {
                try
                {
                    SetIsInGoodState(false);
                }
                catch { }

                originalAction();
            }
        }

        public static Task Remove(ToastNotificationHistory history, string tag, string group)
        {
            return ExecuteEnhanced(
                (db) =>
            {
                bool changed = db.Records.RemoveAll(
                    r =>
                    object.Equals(r.ToastTag, tag)
                    && object.Equals(r.ToastGroup, group)
                    && r.DateAdded <= DateTimeOffset.Now) > 0;

                if (changed)
                {
                    db.SaveDatabaseWithoutWaiting();
                }
            }, () =>
            {
                history.Remove(tag, group);
            });
        }

        public static Task RemoveGroup(ToastNotificationHistory history, string group)
        {
            return ExecuteEnhanced(
                (db) =>
            {
                bool changed = db.Records.RemoveAll(
                    r =>
                    object.Equals(r.ToastGroup, group)
                    && r.DateAdded <= DateTime.Now) > 0;

                if (changed)
                {
                    db.SaveDatabaseWithoutWaiting();
                }
            }, () =>
            {
                history.RemoveGroup(group);
            });
        }

        public static Task Clear(ToastNotificationHistory history)
        {
            return ExecuteEnhanced(
                (db) =>
            {
                bool changed = db.Records.RemoveAll(r => r.DateAdded <= DateTimeOffset.Now) > 0;

                if (changed)
                {
                    db.SaveDatabaseWithoutWaiting();
                }
            }, () =>
            {
                history.Clear();
            });
        }

        public static Task RemoveScheduled(ToastNotifier notifier, ScheduledToastNotification notif)
        {
            return ExecuteEnhanced(
                (db) =>
            {
                // If notif hasn't appeared yet, we'll remove it
                if (notif.DeliveryTime > DateTimeOffset.Now)
                {
                    bool changed = db.Records.RemoveAll(
                        r =>
                        object.Equals(r.ToastTag, notif.Tag)
                        && object.Equals(r.ToastGroup, notif.Group)
                        && r.DateAdded <= notif.DeliveryTime) > 0;

                    if (changed)
                    {
                        db.SaveDatabaseWithoutWaiting();
                    }
                }
            }, () =>
            {
                notifier.RemoveFromSchedule(notif);
            });
        }

        private static object _lock = new object();
        private static Task<ToastHistoryChangeDatabase> _getDatabaseTask;
        private static Task<ToastHistoryChangeDatabase> GetDatabaseAsync()
        {
            lock (_lock)
            {
                if (_getDatabaseTask != null)
                {
                    return _getDatabaseTask;
                }

                _getDatabaseTask = CreateGetDatabaseTask();
                return _getDatabaseTask;
            }
        }

        private static async Task<ToastHistoryChangeDatabase> CreateGetDatabaseTask()
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalCacheFolder.GetFileAsync(DATABASE_FILE_NAME);
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            return GetSerializer().Deserialize<ToastHistoryChangeDatabase>(jsonReader);
                        }
                    }
                }
            }
            catch
            {
                // Corrupt, file not found, etc
                throw new ToastHistoryDatabaseCorruptException();
            }
        }

        private static ToastHistoryChangeDatabase GetCurrDatabase()
        {
            if (_getDatabaseTask != null && _getDatabaseTask.IsCompleted && !_getDatabaseTask.IsFaulted)
            {
                return _getDatabaseTask.Result;
            }

            return null;
        }

        private void SaveDatabaseWithoutWaiting()
        {
            var dontWait = SaveDatabaseAsync();
        }

        private static Task _currSaveTask;
        private bool _needsAnotherSave;
        private Task SaveDatabaseAsync()
        {
            lock (_lock)
            {
                if (HasBeenDisposed())
                {
                    // If database has changed, we stop save operations on this database.
                    // This occurs when the database has been deleted.
                    return Task.FromResult(true);
                }

                if (_currSaveTask != null)
                {
                    _needsAnotherSave = true;
                    return _currSaveTask;
                }

                _currSaveTask = CreateSaveDatabaseTask();
                return _currSaveTask;
            }
        }

        public static Task SavingTask
        {
            get
            {
                var task = _currSaveTask;
                if (task != null)
                {
                    return task;
                }

                return Task.FromResult(true);
            }
        }

        private bool HasBeenDisposed()
        {
            return this != GetCurrDatabase();
        }

        private async Task CreateSaveDatabaseTask()
        {
            try
            {
                // TODO: Issue about serializing object while other manipulations to the records might be occuring
                // Serialize to the temp file
                StorageFile file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(DATABASE_TEMP_FILE_NAME, CreationCollisionOption.ReplaceExisting);
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        using (var jsonWriter = new JsonTextWriter(writer))
                        {
                            GetSerializer().Serialize(jsonWriter, this);
                        }
                    }
                }

                if (HasBeenDisposed())
                {
                    return;
                }

                // And then copy that to the final destination
                await file.RenameAsync(DATABASE_FILE_NAME, NameCollisionOption.ReplaceExisting);
            }
            catch
            {
                Debug.WriteLine("Failed to save");
            }

            lock (_lock)
            {
                if (HasBeenDisposed())
                {
                    return;
                }

                if (_needsAnotherSave)
                {
                    // If we need another save, we will continue and save again
                    _needsAnotherSave = false;
                }
                else
                {
                    // Otherwise, we're all done, can set the task to null and return
                    _currSaveTask = null;
                    return;
                }
            }

            await CreateSaveDatabaseTask();
        }

        private static JsonSerializer GetSerializer()
        {
            return JsonSerializer.Create();
        }

        /// <summary>
        /// Compares the current Toast notifications in order to obtain what has changed
        /// </summary>
        /// <returns>Async task</returns>
        public static Task SyncWithPlatformAsync()
        {
            DateTimeOffset now = DateTimeOffset.Now;

            return Execute((conn) =>
            {
                SyncWithPlatformHelper(now, conn);
            });
        }

        private static void SyncWithPlatformHelper(DateTimeOffset now, ToastHistoryChangeDatabase db)
        {
            // Get all current notifications from the platform
            var notifs = ToastNotificationManager.History.GetHistory().Where(i => i.Tag.Length > 0);

            var toBeAdded = new List<ToastNotification>();

            // TODO - we should probably manage dupes while inserting... then we can make the updating a lot more efficient too
            // for when we support expire. Although this may no longer be necessary since we are using a flat serialized file now rather than a DB

            // The only time we'll have dupes is from scheduled notifications that finally "popped"
            // (their DateAdded is finally less than current time)
            //
            // Obtain the notifications that we have stored
            // We skip notifications that have a DateAdded of future values, since
            // those are scheduled ones that haven't appeared yet.
            List<ToastHistoryChangeRecord> storedNotifs = db.Records.Where(
                r =>
                r.DateAdded <= now
                && r.Status != ToastHistoryChangeRecordStatus.Removed).ToList();

            // Find and remove the dupes
            List<ToastHistoryChangeRecord> dupes = new List<ToastHistoryChangeRecord>();
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
                // We want to delete any of the dupes
                if (dupes.Count > 0)
                {
                    db.Records.RemoveAll(r => dupes.Contains(r));
                }

                // And we also delete any that were previously adds via push
                // since if the app previously didn't accept that change, we
                // don't even inform the app that the push was ever received (since it
                // already got dismissed).
                db.Records.RemoveAll(r =>
                    r.Status == ToastHistoryChangeRecordStatus.AddedViaPush);

                // Notifications to change to Removed
                if (toBeRemoved.Count > 0)
                {
                    foreach (var r in db.Records.Where(r => toBeRemoved.Contains(r)))
                    {
                        r.Status = ToastHistoryChangeRecordStatus.Removed;
                        r.DateRemoved = now;
                    }
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
                    db.Records.AddRange(newRecords);
                }

                db.SaveDatabaseWithoutWaiting();
            }
        }

        /// <summary>
        /// Does not sort.
        /// </summary>
        /// <returns>Array of the change records.</returns>
        public static Task<ToastHistoryChangeRecord[]> GetChangesAsync()
        {
            DateTimeOffset now = DateTimeOffset.Now;

            return Execute((db) =>
            {
                SyncWithPlatformHelper(now, db);

                // No need to check DateAdded since any scheduled ones that haven't popped
                // yet will still be marked Committed
                return db.Records.Where(r => r.Status != ToastHistoryChangeRecordStatus.Committed).ToArray();
            });
        }

        public static Task AcceptChangesAsync(IEnumerable<ToastHistoryChangeRecord> changes)
        {
            // These are the potential paths of states (cannot flow backwards)

            // AddedViaPush -> Committed -> Deleted (dismissed)
            // Committed (added locally) -> Deleted (dismissed)
            return Execute((db) =>
            {
                bool changed = false;

                // For removals, the only action is to delete from the database
                ToastHistoryChangeRecord[] toRemove = changes.Where(i => i.Status == ToastHistoryChangeRecordStatus.Removed).ToArray();
                if (toRemove.Length > 0)
                {
                    changed = db.Records.RemoveAll(r => toRemove.Contains(r)) > 0;
                }

                // For AddedViaPush, we move it to the Committed state
                ToastHistoryChangeRecord[] toCommit = changes.Where(r => r.Status == ToastHistoryChangeRecordStatus.AddedViaPush).ToArray();
                if (toCommit.Length > 0)
                {
                    foreach (var r in db.Records.Where(r => toCommit.Contains(r) && r.Status != ToastHistoryChangeRecordStatus.Committed))
                    {
                        r.Status = ToastHistoryChangeRecordStatus.Committed;
                        changed = true;
                    }
                }

                if (changed)
                {
                    db.SaveDatabaseWithoutWaiting();
                }
            });
        }

        public static Task<ToastHistoryChangeRecord> MarkChasedAsync(string arguments)
        {
            DateTimeOffset now = DateTimeOffset.Now;

            return Execute((db) =>
            {
                ToastHistoryChangeRecord[] records = db.Records.Where(i => i.PayloadArguments.Equals(arguments) && i.DateAdded <= now).ToArray();

                if (records.Length == 0)
                {
                    return null;
                }

                ToastHistoryChangeRecord newest = records[0];
                for (int i = 1; i < records.Length; i++)
                {
                    if (records[i].DateAdded >= newest.DateAdded)
                    {
                        newest = records[i];
                    }
                }

                db.Records.RemoveAll(r => object.Equals(r.PayloadArguments, arguments));

                db.SaveDatabaseWithoutWaiting();

                return newest;
            });
        }

        private static Task Execute(Action<ToastHistoryChangeDatabase> action)
        {
            return Execute<bool>((db) =>
            {
                action(db);
                return true;
            });
        }

        private static Task<T> Execute<T>(Func<ToastHistoryChangeDatabase, T> action)
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
                    using (await Locks.LockAsync())
                    {
                        // Initialize settings
                        await ToastHistoryChangeTrackerConfiguration.InitializeAsync();

                        // We'll also check after we've established lock
                        if (!GetIsInGoodState())
                        {
                            return default(T);
                        }

                        ToastHistoryChangeDatabase db;

                        try
                        {
                            db = await GetDatabaseAsync();
                        }
                        catch (ToastHistoryDatabaseCorruptException)
                        {
                            // Set bad state
                            try
                            {
                                SetIsInGoodState(false);
                            }
                            catch { }

                            // If database corrupt, we return default result
                            return default(T);
                        }

                        return action.Invoke(db);
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
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.LocalCacheFolder.GetFileAsync(DATABASE_FILE_NAME);
            }
            catch (FileNotFoundException)
            {
                // Continue
            }
            catch
            {
                throw;
            }

            if (file != null)
            {
                await file.DeleteAsync();
            }

            lock (_lock)
            {
                if (_getDatabaseTask != null)
                {
                    if (_getDatabaseTask.IsCompleted && !_getDatabaseTask.IsFaulted && _getDatabaseTask.Result != null)
                    {
                        _getDatabaseTask.Result._needsAnotherSave = false;
                    }
                    _getDatabaseTask = null;
                }
            }
        }

        private static async Task CreateDatabaseAsync()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            var notifs = ToastNotificationManager.History.GetHistory();
            var scheduledNotifs = ToastNotificationManager.CreateToastNotifier().GetScheduledToastNotifications();

            ToastHistoryChangeDatabase db = new ToastHistoryChangeDatabase();
            _getDatabaseTask = Task.FromResult(db);

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

            db.Records.AddRange(newRecords);
            await db.SaveDatabaseAsync();

            SetIsInGoodState(true);
        }

        public static Task EnableAsync()
        {
            return Task.Run(async delegate
            {
                using (await Locks.LockAsync())
                {
                    if (GetIsInGoodState())
                    {
                        return;
                    }

                    // Delete first
                    await DeleteDatabaseAsync();

                    // Then re-create
                    await CreateDatabaseAsync();
                }
            });
        }

        public static Task ResetAsync()
        {
            return Task.Run(async delegate
            {
                using (await Locks.LockAsync())
                {
                    // Delete first
                    await DeleteDatabaseAsync();

                    // Then re-create
                    await CreateDatabaseAsync();
                }
            });
        }

        internal static void ClearCache()
        {
            _getDatabaseTask = null;
        }
    }
}
