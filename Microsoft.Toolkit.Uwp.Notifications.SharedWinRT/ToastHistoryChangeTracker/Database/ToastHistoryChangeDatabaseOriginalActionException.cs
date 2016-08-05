using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    public class ToastHistoryChangeDatabaseOriginalActionException : Exception
    {
        public Exception OriginalException { get; private set; }

        public ToastHistoryChangeDatabaseOriginalActionException(Exception originalException)
        {
            OriginalException = originalException;
        }
    }
}
