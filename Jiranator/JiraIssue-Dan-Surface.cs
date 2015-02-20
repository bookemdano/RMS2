using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Windows.Media;

namespace Jiranator
{
    public enum LoadEnum
    {
        Live,
        Latest,
        Yesterday
    }

    static public class JiraAccess
    {
        static string _latestApi = @"https://roadnetmobiledev.atlassian.net/rest/api/latest";

        static string _linkOnBoard = @"https://roadnetmobiledev.atlassian.net/secure/RapidBoard.jspa?rapidView=3&view=detail&selectedIssue=";
        static string _linkDirect = @"https://roadnetmobiledev.atlassian.net/browse/";
        static public string LinkDirect
        {
            get
            {
                return _linkDirect;
            }
        }
        static public string LinkOnBoard
        {
            get
            {
                return _linkOnBoard;
            }
        }
        static public string GetEncodedCredentials(string username, string password)
        {
            string mergedCredentials = username + ":" + password;
            byte[] byteCredentials = UTF8Encoding.UTF8.GetBytes(mergedCredentials);
            return Convert.ToBase64String(byteCredentials);
        }

        internal static string GetIssueUri(string key)
        {
            return _latestApi + @"/issue/" + key + ".json";
        }

        internal static string GetSprintUri(string project, string sprint)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            var request = @"/search?jql=project=" + project + " AND Sprint='" + sprint + "'&maxResults=200";
            if (true)   // setting to false gets all fields but takes forever
                request += "&fields=parent,summary,assignee,status," + JiraIssue.StoryPointField + "," + JiraIssue.IssueTypeField;
            return _latestApi + request;
        }

        internal static string GetUnassignUri(string key)
        {
            return _latestApi + @"/issue/" + key;
        }
        internal static string GetUnassignBody()
        {
            return "{\"fields\": {\"assignee\":{\"name\":\"\"}}}";
        }
    }

    public class JiraIssue : INotifyPropertyChanged
    {
        public string FontWeight
        {
            get
            {
                if (IsSubtask)
                    return "Normal";
                else
                    return "Bold";
            }
        }
        Brush _greenBrush;
        private Brush GreenBrush
        {
            get
            {
                if (_greenBrush == null)
                    _greenBrush = new SolidColorBrush(Colors.Green);
                return _greenBrush;
            }
        }
        Brush _redBrush;
        private Brush RedBrush
        {
            get
            {
                if (_redBrush == null)
                    _redBrush = new SolidColorBrush(Colors.Red);
                return _redBrush;
            }
        }        
        Brush _blackBrush;
        private Brush BlackBrush
        {
            get
            {
                if (_blackBrush == null)
                    _blackBrush = new SolidColorBrush(Colors.Black);
                return _blackBrush;
            }
        }
        public Brush Foreground
        {
            get
            {
                var delta = GetStatusPriority(FauxStatus) - GetStatusPriority(OldFauxStatus);
                if (delta > 0)
                    return GreenBrush;
                else if (delta < 0)
                    return RedBrush;

                return BlackBrush;
            }
        }
        public static string IssueTypeField = "issuetype";
        public static string StoryPointField = "customfield_10004";

        public override int GetHashCode()
        {
            return ToCsv().GetHashCode();
        }

        public string Key { get; private set; }
        public bool IsSubtask { get; private set; }
        public string Summary { get; private set; }
        public DateTime? CreatedDate { get; private set; }
        public int Progress { get; private set; }
        public List<JiraIssue> SubTasks { get; private set; }
        public string IssueType { get; private set; }
        public string Status { get; private set; }
        public string FauxStatus
        {
            get
            {
                if (SubTasks.Count() == 0)
                    return Status;
                var status = "Faux";
                foreach (var subTask in SubTasks)
                {
                    if (subTask.IsDocTask)
                        continue;
                    status = CompareStatus(status, subTask.Status);
                }
                // if everything else is done, check for hanging doc task
                if (GetStatusPriority(status) >= 10)
                {
                    var docTask = SubTasks.SingleOrDefault(t => t.IsDocTask);
                    if (docTask != null)
                    {
                        if (GetStatusPriority(docTask.Status) < 10)
                            status = "Doc";
                    }
                }
                return status;
            }
        }
        private bool IsDocTask
        {
            get
            {
                return IssueType.Contains("Doc") || Summary == "Doc";
            }
        }

        private string CompareStatus(string status, string subStatus)
        {
            if (GetStatusPriority(subStatus) < GetStatusPriority(status))
                return subStatus;
            return status;
        }
        public bool IsResolved
        {
            get
            {
                return GetStatusPriority(FauxStatus) >= 10;
            }
        }

        public bool WasResolved
        {
            get
            {
                return GetStatusPriority(OldFauxStatus) >= 10;
            }
        }

        public bool IsInProgress
        {
            get
            {
                return GetStatusPriority(FauxStatus) == 1;
            }
        }
        public bool IsOpen
        {
            get
            {
                return GetStatusPriority(FauxStatus) <= 0;
            }
        }
        public bool IsTesting
        {
            get
            {
                var p = GetStatusPriority(FauxStatus);
                return p == 2 || p == 3;
            }
        }
        public bool IsOnHold
        {
            get
            {
                var p = GetStatusPriority(FauxStatus);
                return p == 4 || p == 5;
            }
        }
        public static int GetStatusPriority(string status)
        {
            if (status == "Faux")
                return 99;
            else if (status == "Reopened")
                return -1;
            else if (status == "Open")
                return 0;
            else if (status == "In Progress")
                return 1;
            else if (status == "On Hold")
                return 2;
            else if (status == "Code Review")
                return 3;
            else if (status == "Ready for Testing")
                return 5;
            else if (status == "In Testing")
                return 6;
            else if (status == "Doc")
                return 10;
            else if (status == "Resolved")
                return 20;
            else if (status == "Closed")
                return 21;
            return 99;
        }

        public string Assignee { get; set; }
        public string Sprint { get; set; }

        public string OldStatus { get; set; }
        public string OldFauxStatus { get; set; }

        public override string ToString()
        {
            var rv = "";
            if (IssueType.ToLower().Contains("sub"))
                rv += "-";
            rv += Key;
            if (!string.IsNullOrWhiteSpace(Summary))
                rv += " " + Summary;
            if (!string.IsNullOrWhiteSpace(Assignee))
                rv += " by " + Assignee;
            return rv;
        }
        public string Details()
        {
            var rv = "Key:" + Key;
            if (!string.IsNullOrWhiteSpace(Summary))
                rv += " Summary:" + Summary;
            if (!string.IsNullOrWhiteSpace(IssueType))
                rv += " IssueType:" + IssueType;
            if (CreatedDate.HasValue)
                rv += " CreatedDate:" + CreatedDate + " Progress:" + Progress;
            return rv;
        }
        internal static JiraIssue Parse(JToken json)
        {
            try
            {
                var rv = new JiraIssue();
                rv.Key = (string)json["key"];
                var fields = json["fields"];
                rv.Summary = (string)fields["summary"];
                if (fields[StoryPointField] != null)
                {
                    var storyPoints = fields[StoryPointField];
                    var str = (string)fields[StoryPointField];
                    double d;
                    if (double.TryParse(str, out d))
                        rv.StoryPoints = d;

                }
                var issueType = fields[IssueTypeField];
                rv.IssueType = (string)issueType["name"];
                rv.IsSubtask = (bool)issueType["subtask"];
                if (fields["created"] != null)
                    rv.CreatedDate = (DateTime)fields["created"];
                //var tokens = fields.Children();
                if (fields["status"] != null)
                {
                    var status = fields["status"];
                    rv.Status = (string)status["name"];
                }

                if (fields["progress"] != null)
                {
                    var progress = fields["progress"];
                    rv.Progress = (int)progress["progress"];
                }
                var parent = fields["parent"];
                if (parent != null)
                    rv.Parent = (string)parent["key"];
                rv.Assignee = GetString(fields, "assignee", "name");
                var subs = fields["subtasks"];
                rv.SubTasks = new List<JiraIssue>();
                if (subs != null && subs.Count() > 0)
                {
                    foreach (var issue in subs)
                    {
                        rv.SubTasks.Add(JiraIssue.Parse(issue));
                    }
                }
                return rv;
            }
            catch (Exception exc)
            {
                throw;
            }
        }

        private static string GetString(JToken fields, string p1, string p2)
        {
            try
            {
                var field1 = fields[p1];
                if (field1 == null || field1.HasValues == false)
                    return null;

                var field2 = field1[p2];
                if (field2 == null)
                    return null;
                return (string)field2;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public string Parent { get; set; }

        public string LinkOnBoard
        {
            get
            {
                return JiraAccess.LinkOnBoard + Key;
            }
        }
        public string LinkDirect
        {
            get
            {
                return JiraAccess.LinkDirect + Key;
            }
        }
        public string HtmlDescription
        {
            get
            {
                string link = "<A HREF=" + LinkDirect + ">" + Key + "</A>";
                return HtmlWrap(link + "-" + Summary);
            }
        }
        static public string HtmlWrap(string text)
        {

            string template = "Version:1.0" + System.Environment.NewLine +
                    @"StartHTML:000125" + System.Environment.NewLine +
                    @"EndHTML:00EH00" + System.Environment.NewLine +
                    @"StartFragment:000209" + System.Environment.NewLine +
                    @"EndFragment:00EF00" + System.Environment.NewLine +
                    @"SourceURL:file:///C:/temp/test.htm" + System.Environment.NewLine +
                    @"<HTML>" + System.Environment.NewLine +
                    @"<head>" + System.Environment.NewLine +
                    @"<title>HTML clipboard</title>" + System.Environment.NewLine +
                    @"</head>" + System.Environment.NewLine +
                    @"<body>" + System.Environment.NewLine +
                    @"<!--StartFragment-->THE TEXT<!--EndFragment-->" + System.Environment.NewLine +
                    @"</body>" + System.Environment.NewLine +
                    "</html>";

            string rv = template.Replace("THE TEXT", text);
            //rv = template.Replace("THE TEXT", @"<b>Hello!</b>");

            rv = rv.Replace("00EH00", rv.Length.ToString("000000"));
            rv = rv.Replace("00EF00", rv.IndexOf("<!--EndFragment-->").ToString("000000"));
            return rv;

        }

        internal void AddSubtask(JiraIssue subtask)
        {
            if (SubTasks == null)
                SubTasks = new List<JiraIssue>();
            SubTasks.Add(subtask);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }


        internal string ToCsv()
        {
            return Sprint + "," + Assignee + "," + Key + "," + Summary.Replace(",", "-") + "," + IssueType + "," + StoryPoints + "," + Status;
        }

        public double StoryPoints { get; set; }


        public string ShortFauxStatus
        {
            get
            {
                var status = FauxStatus;
                if (status == "On Hold")
                    return "H";
                else
                    return Status[0].ToString();
            }
        }

        internal bool Contains(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;
            filter = filter.ToUpper();
            if (Key.ToUpper().Contains(filter))
                return true;
            if (Summary.ToUpper().Contains(filter))
                return true;
            if (Status.ToUpper().Contains(filter))
                return true;
            if (Assignee != null && Assignee.ToUpper().Contains(filter))
                return true;

            return false;
        }

        internal JiraIssue UpdateFromOld(List<JiraIssue> oldIssues)
        {
            var oldIssue = oldIssues.SingleOrDefault(i => i.Key == Key);
            if (oldIssue != null)
            {
                OldStatus = oldIssue.Status;
                OldFauxStatus = oldIssue.FauxStatus;
            }
            return oldIssue;
        }

    }

    public class JiraSprint
    {
        public int Total
        {
            get
            {
                return Issues.Count();
            }
        }
        public List<JiraIssue> Issues { get; set; }
        internal static JiraSprint Parse(JObject json)
        {
            var rv = new JiraSprint();
            //rv.Total = (int) json["total"];
            var issues = json["issues"];
            var jIssues = new List<JiraIssue>();
            foreach (var issue in issues)
            {
                jIssues.Add(JiraIssue.Parse(issue));
            }
            rv.Issues = new List<JiraIssue>();
            rv.Issues.AddRange(jIssues.Where(i => i.IsSubtask == false));
            foreach (var subtask in jIssues.Where(i => i.IsSubtask))
            {
                var parent = rv.Issues.SingleOrDefault(i => i.Key == subtask.Parent);
                if (parent != null)
                    parent.AddSubtask(subtask);
            }
            return rv;
        }

        internal void Merge(JiraSprint jiraSprint)
        {
            if (jiraSprint == null || jiraSprint.Issues.Count() == 0)
                return;
            if (jiraSprint.RetrieveTime > RetrieveTime)
                RetrieveTime = jiraSprint.RetrieveTime;
            Issues.AddRange(jiraSprint.Issues);
        }
        public override int GetHashCode()
        {
            int rv = 0;
            foreach(var issue in Issues)
            {
                rv ^= issue.GetHashCode();
                foreach(var subissue in issue.SubTasks)
                    rv ^= subissue.GetHashCode();
            }
            return rv;
        }
        public void UpdateOldStatus(JiraSprint old)
        {
            foreach (var issue in Issues)
            {
                var oldIssue = issue.UpdateFromOld(old.Issues);
                if (oldIssue != null)
                {
                    foreach (var subTask in issue.SubTasks)
                        subTask.UpdateFromOld(oldIssue.SubTasks);
                }
            }
            OldRetrieveTime = old.RetrieveTime;
        }

        public DateTime RetrieveTime { get; set; }
        public DateTime OldRetrieveTime { get; set; }
    }

}
