using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Toolkit.Uwp.DemoChatApp.ViewLayer
{
    public class ViewItemMessage
    {
        private static int _currMessageId = 1;

        public ViewItemPerson From { get; set; }

        public string Content { get; set; }

        public int Id { get; private set; } = _currMessageId++;
    }
}
