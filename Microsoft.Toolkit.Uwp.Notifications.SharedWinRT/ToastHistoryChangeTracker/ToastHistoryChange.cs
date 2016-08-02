using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    public sealed class ToastHistoryChange
    {
        internal ToastHistoryChange(ToastHistoryChangeRecord record)
        {
            switch (record.Status)
            {
                case ToastHistoryChangeRecordStatus.AddedViaPush:
                    ChangeType = ToastHistoryChangeType.AddedViaPush;
                    break;

                case ToastHistoryChangeRecordStatus.DismissedByUser:
                    ChangeType = ToastHistoryChangeType.DismissedByUser;
                    break;
            }

            Tag = record.ToastTag;
            Group = record.ToastGroup;
            DateAdded = record.DateAdded;
            DateRemoved = record.DateRemoved;
        }

        internal ToastHistoryChange() { }

        /// <summary>
        /// Gets a value that indicates the type of change that occurred.
        /// </summary>
        public ToastHistoryChangeType ChangeType { get; internal set; }

        /// <summary>
        /// Gets the tag from the notification of which the change occurred.
        /// </summary>
        public string Tag { get; internal set; }

        /// <summary>
        /// Gets the group from the notification of which the change occurred.
        /// </summary>
        public string Group { get; internal set; }

        /// <summary>
        /// Gets the date and time when the notification was originally added (for push notifications, this time 
        /// will be a rough estimate, and can be delayed if the background task does not run immediately).
        /// </summary>
        public DateTimeOffset DateAdded { get; internal set; }

        /// <summary>
        /// Gets the date and time when the notification was removed (this will only be set if the <see cref="ChangeType"/>
        /// is <see cref="ToastHistoryChangeType.DismissedByUser"/>. This time will be a rough estimate, and can be delayed if
        /// the background task does not run immediately.
        /// </summary>
        public DateTimeOffset DateRemoved { get; internal set; }
    }
}
