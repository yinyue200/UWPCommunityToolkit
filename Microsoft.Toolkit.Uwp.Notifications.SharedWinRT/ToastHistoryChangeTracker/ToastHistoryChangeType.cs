using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    /// <summary>
    /// Specifies the type of change that occurred.
    /// </summary>
    public enum ToastHistoryChangeType
    {
        /// <summary>
        /// A Toast notification was added via push.
        /// </summary>
        AddedViaPush,

        /// <summary>
        /// A Toast notification was added via push and replaced an existing Toast notification that had the same tag/group.
        /// </summary>
        ReplacedViaPush,

        /// <summary>
        /// A Toast notification was dismissed by the user, by either (1) dismissing the single notification, 
        /// (2) dismissing an entire group of notifications, or (3) clicking on the notification or one of its buttons.
        /// </summary>
        DismissedByUser,

        /// <summary>
        /// A toast notification was expired. This value is determined best-effort (it could accidently appear instead of DismissedByUser
        /// if the trigger task doesn't fire soon enough).
        /// </summary>
        Expired,

        /// <summary>
        /// Change tracking was lost. Call <see cref="ToastHistoryChangeTracker.ResetAsync"/> to reestablish continuity with the Toast history.
        /// </summary>
        ChangeTrackingLost
    }
}
