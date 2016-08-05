using Microsoft.Toolkit.Uwp.DemoChatLibrary.DataLayer;
using Microsoft.Toolkit.Uwp.DemoChatLibrary.Helpers;
using Microsoft.Toolkit.Uwp.DemoChatLibrary.ViewLayer;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;

namespace Microsoft.Toolkit.Uwp.DemoChatLibrary.ViewModel
{
    public class MainViewModel
    {
        public static MainViewModel Current { get; private set; }

        private static ViewItemPerson[] People =
        {
            new ViewItemPerson()
            {
                Name = "Thomas"
            },

            new ViewItemPerson()
            {
                Name = "Kelsey"
            },

            new ViewItemPerson()
            {
                Name = "Matt"
            },

            new ViewItemPerson()
            {
                Name = "Lei"
            }
        };

        private CoreDispatcher _dispatcher;

        private MainViewModel(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            InitializeInternal();
        }

        public static void Initialize(CoreDispatcher dispatcher)
        {
            Current = new MainViewModel(dispatcher);
        }

        private async void InitializeInternal()
        {
            await ToastHistoryChangeTracker.Current.EnableAsync();

            await ToastNotificationManager.History.ClearEnhanced();
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();

            await Task.Delay(new Random().Next(2000, 3000));

            UpdateWithNewMessage();
        }

        public ObservableCollection<ViewItemConversation> Conversations { get; private set; } = new ObservableCollection<ViewItemConversation>();

        private async void UpdateWithNewMessage()
        {
            await Dispatch(async delegate
            {
                var messageContent = MessageGenerator.GetMessage();

                var person = People[new Random().Next(0, People.Length)];

                var message = new ViewItemMessage()
                {
                    Content = messageContent,
                    From = person
                };

                var conversation = Conversations.FirstOrDefault(i => i.With == person);
                if (conversation == null)
                {
                    conversation = new ViewItemConversation()
                    {
                        With = person
                    };
                }

                await AddMessage(conversation, message);

                await Task.Delay(new Random().Next(6000, 16000));

                UpdateWithNewMessage();
            });
        }

        public ViewItemConversation CurrentOpenConversation { get; private set; }

        public async Task MarkOpenedConversation(ViewItemConversation conversation)
        {
            CurrentOpenConversation = conversation;
            conversation.MarkNotificationDismissed();
            await ToastNotificationManager.History.RemoveEnhanced(conversation.Id.ToString(), "conversations");
            TileHelper.Update();
        }

        public async Task MarkOpenedMainPage()
        {
            CurrentOpenConversation = null;

            foreach (var conv in Conversations)
            {
                conv.MarkNotificationDismissed();
            }

            TileHelper.Update();
            await ToastNotificationManager.History.RemoveGroupEnhanced("conversations");
        }

        public async Task OnWindowActivated()
        {
            if (CurrentOpenConversation == null)
            {
                await MarkOpenedMainPage();
            }
            else
            {
                await MarkOpenedConversation(CurrentOpenConversation);
            }
        }

        private async Task Dispatch(Action action)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate { action.Invoke(); });
        }

        public async Task AddMessage(ViewItemConversation conversation, ViewItemMessage message)
        {
            conversation.AddMessage(message);

            int index = Conversations.IndexOf(conversation);
            if (index == -1)
            {
                Conversations.Insert(0, conversation);
            }
            else if (index > 0)
            {
                Conversations.Move(index, 0);
            }

            if (message.From != null)
            {
                bool needsNotifications = false;
                if (!WindowHelper.IsActive)
                {
                    // Whenever window isn't active, we always send notifications
                    needsNotifications = true;
                }
                else if (CurrentOpenConversation != conversation && CurrentOpenConversation != null)
                {
                    // If we're not on this conversation, and not on the main page, we send notifications
                    needsNotifications = true;
                }

                if (!needsNotifications)
                {
                    conversation.MarkNotificationDismissed();
                }
                else
                {
                    await SendToast(conversation);
                    TileHelper.Update();
                }
            }
        }

        private async Task SendToast(ViewItemConversation conversation)
        {
            var messagesToNotifyAbout = conversation.GetMessagesThatShouldHaveNotifications();
            if (messagesToNotifyAbout.Count == 0)
            {
                return;
            }

            ToastContent content = new ToastContent()
            {
                Launch = conversation.Id.ToString(),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = conversation.With.Name
                            }
                        }
                    }
                }
            };

            foreach (var m in messagesToNotifyAbout.Take(7))
            {
                content.Visual.BindingGeneric.Children.Add(new AdaptiveText()
                {
                    Text = m.Content
                });
            }

            var notif = new ToastNotification(content.GetXml())
            {
                Tag = conversation.Id.ToString(),
                Group = "conversations"
            };

            await ToastNotificationManager.CreateToastNotifier().ShowEnhanced(notif);
        }

        private void Timer_Tick(object sender, object e)
        {
            throw new NotImplementedException();
        }
    }
}
