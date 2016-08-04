using Microsoft.Toolkit.Uwp.Notifications.DemoChatLibrary.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Toolkit.Uwp.Notifications.DemoChatLibrary.ViewLayer
{
    public class ViewItemConversation : BindableBase
    {
        private static int _currId = 1;

        public ViewItemPerson With { get; set; }

        public ObservableCollection<ViewItemMessage> Messages { get; private set; } = new ObservableCollection<ViewItemMessage>();

        private ViewItemMessage _latestMessage;
        public ViewItemMessage LatestMessage
        {
            get { return _latestMessage; }
            set { Set(ref _latestMessage, value); }
        }

        public void AddMessage(ViewItemMessage message)
        {
            Messages.Add(message);

            LatestMessage = message;

            if (message.From != null)
            {
                HasUnread = true;
            }
        }

        public int Id { get; private set; } = _currId++;

        private int _idOfDismissedNotificationMessage;
        private bool _hasUnread;

        public bool HasUnread
        {
            get { return _hasUnread; }
            set { Set(ref _hasUnread, value); }
        }

        public void MarkRead()
        {
            _hasUnread = false;
            MarkNotificationDismissed();
        }

        public void MarkNotificationDismissed()
        {
            // Store the ID of the latest message in the conversation
            if (Messages.Count == 0)
            {
                _idOfDismissedNotificationMessage = 0;
            }
            else
            {
                _idOfDismissedNotificationMessage = Messages.Last().Id;
            }
        }

        public List<ViewItemMessage> GetMessagesThatShouldHaveNotifications()
        {
            List<ViewItemMessage> answer = new List<ViewLayer.ViewItemMessage>();

            foreach (var message in Messages.Reverse())
            {
                if (message.Id <= _idOfDismissedNotificationMessage)
                {
                    break;
                }

                if (message.From != null)
                {
                    answer.Add(message);
                }
            }

            return answer;
        }
    }
}
