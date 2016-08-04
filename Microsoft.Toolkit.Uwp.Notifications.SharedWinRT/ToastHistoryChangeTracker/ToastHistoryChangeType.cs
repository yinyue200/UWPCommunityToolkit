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
        /// A Toast notification was either (1) dismissed by the user, (2) clicked on by the user, (3) expired based on the exipration time,
        /// or (4) removed via push. You will NOT receive a change if you remove the notification programmatically via the local API's.
        /// </summary>
        Removed,

        /// <summary>
        /// Change tracking was lost. Call <see cref="ToastHistoryChangeTracker.ResetAsync"/> to reestablish continuity with the Toast history.
        /// </summary>
        ChangeTrackingLost
    }
}
