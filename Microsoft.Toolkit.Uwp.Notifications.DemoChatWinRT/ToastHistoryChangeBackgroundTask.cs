using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.Notifications.DemoChatLibrary.Helpers;
using Microsoft.Toolkit.Uwp.Notifications.DemoChatLibrary.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace Microsoft.Toolkit.Uwp.Notifications.DemoChatWinRT
{
    public sealed class ToastHistoryChangeBackgroundTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var viewModel = MainViewModel.Current;
            if (viewModel == null)
            {
                return;
            }

            var deferral = taskInstance.GetDeferral();

            try
            {
                var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
                var changes = await reader.ReadChangesAsync();
                bool madeChanges = false;

                foreach (var c in changes)
                {
                    if (c.ChangeType == ToastHistoryChangeType.Removed && c.Group.Equals("conversations"))
                    {
                        int convId = int.Parse(c.Tag);

                        var conv = viewModel.Conversations.FirstOrDefault(i => i.Id == convId);
                        if (conv != null)
                        {
                            conv.MarkNotificationDismissed();
                            madeChanges = true;
                        }
                    }
                }

                await reader.AcceptChangesAsync();

                if (madeChanges)
                {
                    TileHelper.Update();
                }
            }

            catch { }
            deferral.Complete();
        }
    }
}
