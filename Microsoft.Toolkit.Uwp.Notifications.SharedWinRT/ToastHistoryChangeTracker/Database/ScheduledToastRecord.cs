using SQLite.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    internal class ScheduledToastRecord
    {
        [PrimaryKey]
        [AutoIncrement]
        public long UniqueId { get; set; }
        
        [MaxLength(ToastHistoryChangeRecord.MAX_LENGTH_OF_TAG_AND_GROUP)]
        [NotNull]
        public string ToastTag { get; set; }
        
        [MaxLength(ToastHistoryChangeRecord.MAX_LENGTH_OF_TAG_AND_GROUP)]
        [NotNull]
        public string ToastGroup { get; set; }
        
        public DateTimeOffset DeliveryTime { get; set; }

        public string AdditionalData { get; set; }

        public string PayloadArguments { get; set; }

        public string Payload { get; set; }
    }
}
