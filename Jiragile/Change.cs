using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace JiraShare
{
    public class ChangeSet
    {
        List<Change> _changes = new List<Change>();
        public ChangeSet()
        {   // only for copy constructor
        }

        public ChangeSet(List<Change> changes)
        {
            _changes = changes.OrderBy(c => c.Timestamp).ToList();
        }

        public IEnumerable<Change> AllChanges
        {
            get
            {
                return _changes;
            }
        }

        public IEnumerable<Change> FieldChanges(string field)
        {
            return _changes.Where(c => c.Field == field);
        }

        internal void Add(Change change)
        {
            _changes.Add(change);
        }

        internal Change LastChange(string field)
        {
            var theseChanges = FieldChanges(field);
            if (theseChanges.Count() == 0)
                return null;
            return theseChanges.Last();
        }
        internal Change FirstChange(string field)
        {
            var theseChanges = FieldChanges(field);
            if (theseChanges.Count() == 0)
                return null;
            return theseChanges.First();
        }
    }
    public class Change
    {
        public Change(string field, DateTimeOffset timestamp, string oldValue, string newValue)
        {
            Field = field;
            Timestamp = timestamp;
            OldValue = oldValue;
            NewValue = newValue;
        }
        public string Field { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }
}