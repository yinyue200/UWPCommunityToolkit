using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    internal class ToastHistoryChangeRecord : IComparable<ToastHistoryChangeRecord>
    {
        /// <summary>
        /// 64 chars for tag/group
        /// </summary>
        internal const int MAX_LENGTH_OF_TAG_AND_GROUP = 64;

        public string ToastTag { get; set; }

        public string ToastGroup { get; set; }

        public ToastHistoryChangeRecordStatus Status { get; set; }

        public DateTimeOffset DateAdded { get; set; }

        public DateTimeOffset DateRemoved { get; set; }

        public DateTimeOffset ExpirationTime { get; set; } = DateTimeOffset.Now.AddDays(7);

        public string AdditionalData { get; set; }

        public string PayloadArguments { get; set; }

        public string Payload { get; set; }

        public int CompareTo(ToastHistoryChangeRecord other)
        {
            int compareByDate = GetDateForComparison().CompareTo(other.GetDateForComparison());
            if (compareByDate != 0)
            {
                return compareByDate;
            }

            return Status.CompareTo(other.Status);
        }

        private DateTimeOffset GetDateForComparison()
        {
            if (DateRemoved != DateTimeOffset.MinValue)
            {
                return DateRemoved;
            }

            return DateAdded;
        }
    }
}
