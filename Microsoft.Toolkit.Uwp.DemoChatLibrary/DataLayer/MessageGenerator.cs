using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Microsoft.Toolkit.Uwp.DemoChatLibrary.DataLayer
{
    public static class MessageGenerator
    {
        private static readonly string[] _messages =
        {
            "Hey!",
            "No way!",
            "That's awesome!",
            "I can't believe that.",
            "Have you seen Notification Mirroring?",
            "Seattle is brutal during the winter.",
            "Jukebox The Ghost is an awesome band!",
            "Ever been to Three Fingers Lookout?",
            "If only there was an app for that!",
            "Every day should be Taco Tuesday.",
            "If you're afraid of failure, you'll never succeed.",
            "Live Tiles, bro.",
            "How about Discovery Park?",
            "We should go to Southern Utah...",
            "Xamarin is honestly the best thing in the world.",
            "The UWP Toolkit is great!"
        };

        public static string GetMessage()
        {
            const string key = "StoredMessageIndex";

            int indexValue = 0;
            object obj;
            ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out obj);
            if (obj != null)
            {
                indexValue = (int)obj;
            }

            string answer = _messages[indexValue % _messages.Length];

            indexValue++;
            ApplicationData.Current.LocalSettings.Values[key] = indexValue;
            return answer;
        }
    }
}
