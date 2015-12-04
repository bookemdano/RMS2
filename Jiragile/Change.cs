using System;
using Newtonsoft.Json.Linq;

namespace JiraShare
{
    public class Change
    {
        public Change(DateTimeOffset timestamp, string oldValue, string newValue)
        {
            Timestamp = timestamp;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public DateTimeOffset Timestamp { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }
}