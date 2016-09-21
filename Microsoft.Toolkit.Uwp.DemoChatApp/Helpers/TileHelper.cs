using Microsoft.Toolkit.Uwp.DemoChatApp.ViewModel;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace Microsoft.Toolkit.Uwp.DemoChatApp.Helpers
{
    public class TileHelper
    {
        public static void Update()
        {
            var badgeCount = MainViewModel.Current.Conversations.Where(i => i.GetMessagesThatShouldHaveNotifications().Any()).Count();
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(new BadgeNumericContent((uint)badgeCount).GetXml()));

            foreach (var conversation in MainViewModel.Current.Conversations)
            {
                var messagesToNotify = conversation.GetMessagesThatShouldHaveNotifications();
                if (messagesToNotify.Count > 0)
                {
                    var contentAdaptive = new TileBindingContentAdaptive()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = conversation.With.Name
                            }
                        }
                    };
                    foreach (var m in messagesToNotify.Take(7))
                    {
                        contentAdaptive.Children.Add(new AdaptiveText()
                        {
                            // TODO: Need to trim content to ensure we don't exceed the 5 KB limit
                            Text = m.Content,
                            HintWrap = true,
                            HintStyle = AdaptiveTextStyle.CaptionSubtle
                        });
                    }

                    TileContent content = new TileContent()
                    {
                        Visual = new TileVisual()
                        {
                            TileMedium = new TileBinding()
                            {
                                Content = contentAdaptive
                            }
                        }
                    };
                    TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(content.GetXml()));
                    System.Diagnostics.Debug.WriteLine("Tile: " + messagesToNotify.First().Content);
                    return;
                }
            }

            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }
    }
}
