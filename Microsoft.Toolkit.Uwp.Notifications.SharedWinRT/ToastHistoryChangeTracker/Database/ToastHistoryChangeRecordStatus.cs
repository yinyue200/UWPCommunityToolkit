using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    internal enum ToastHistoryChangeRecordStatus
    {
        AddedViaPush,
        Committed,
        DismissedByUser,
        Expired
    }
}
