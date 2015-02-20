using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Windows.UI.Xaml.Media;
using Windows.UI;
// v1.0
namespace Jiranator
{

    // TODO Allow setting and linking to salesforce item
    // TODO Show by Epic
    // TODO Show by compenent
    // TODO allow setting component
    // TODONE 3/5/2014 make sort column sticky but not persisted
    // TODONE 11/25/13 Show Partial and In Progress at master task level
    // TODONE 11/25/13 Make status enums

    public enum LoadEnum
    {
        LiveAlways,
        LiveOnlyIfOld,
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

        internal static string GetIssueJsonUri(string key)
        {
            return _latestApi + @"/issue/" + key + ".json";
        }

        internal static string FindIssuesUri(string text)
        {
            return _latestApi + @"/search?jql=text ~ '" + text + "'&maxResults=200";
        }
        
        internal static string GetSprintUri(string project, string sprint)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            var request = @"/search?jql=project=" + project + " AND Sprint='" + sprint + "'&maxResults=200";
            if (true)   // setting to false gets all fields but takes forever
            {
                var fields = new List<string>(){"parent", "summary", "assignee", "status", "timetracking", JiraIssue.StoryPointField, JiraIssue.IssueTypeField};
                request += "&fields=" + string.Join(",", fields);
            }
            return _latestApi + request;
        }

        internal static string GetIssueUri(string key)
        {
            return _latestApi + @"/issue/" + key;
        }
        internal static string GetIssuesUri()
        {
            return _latestApi + @"/issue";
        }
        internal static JObject MakeJobj(string name, JToken token)
        {
            var rv = new JObject();
            rv.Add(name, token);
            return rv;
        }

        internal static string GetAssignBody(string name)
        {
            var jobj1 = MakeJobj("name", name);
            var jobj2 = MakeJobj("assignee", jobj1);
            var fields = MakeJobj("fields", jobj2);

            // "{\"fields\": {\"assignee\":{\"name\":\"\"}}}";
            return fields.ToString();
        }
        internal static string GetNewSubtaskBody(string project, JiraIssue issue, string summary, string originalEstimate, string assignee)
        {
            dynamic subtask = new JObject();
            /*
            subtask.summary = "armstrong";
            subtask.description = "described";
            subtask.project = MakeJobj("key", project);
            subtask.issuetype = MakeJobj("name", "Bug");
            */
            subtask.summary = summary;
            subtask.description = "added by Jiranator";
            subtask.project = MakeJobj("key", project);
            if (summary.Contains("DOC"))
                subtask.issuetype = MakeJobj("name", "Doc Sub-task");
            else
                subtask.issuetype = MakeJobj("name", "Sub-task");
            subtask.parent = MakeJobj("id", issue.Key);
            subtask.assignee = MakeJobj("name", assignee);
            subtask.timetracking = MakeJobj("originalEstimate", originalEstimate);

            var rv = MakeJobj("fields", subtask);
            return rv.ToString();
        }
    }

    public class JiraIssueViewModel : JiraIssue, INotifyPropertyChanged
    {
        #region Ctor

        public JiraIssueViewModel(JiraIssue issue) :
            base(issue)
        {
        }

        #endregion

        #region Display Related

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
                if (OldCalcedStatus == StatusEnum.Unknown || OldCalcedStatus == StatusEnum.Unknown)
                    return BlackBrush;

                var delta = CalcedStatus - OldCalcedStatus;
                if (delta > 0)
                    return GreenBrush;
                else if (delta < 0)
                    return RedBrush;

                return BlackBrush;
            }
        }

        public string SubTaskCount
        {
            get
            {
                if (SubTasks.Count() == 0)
                    return "-";
                return SubTasks.Count().ToString();
            }
        }

        public string ShortStatus
        {
            get
            {
                if (CalcedStatus == StatusEnum.OnHold)
                    return "H";
                else if (IsTesting)
                    return "T";
                else
                    return CalcedStatus.ToString()[0].ToString();
            }
        }

        #endregion

        #region Interfaces

        public override string ToString()
        {
            var rv = "";
            if (IssueType.ToLower().Contains("sub"))
                rv += "-";
            rv += Key;
            rv += " [" + ShortStatus + "]";
            if (!string.IsNullOrWhiteSpace(Summary))
                rv += " " + Summary;
            if (!string.IsNullOrWhiteSpace(Assignee))
                rv += " by " + Assignee;
            return rv;
        }

        #endregion

        #region Interfaces

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
    public class JiraSubTask : JiraIssue
    {

    }

    public class JiraIssue : IComparable
    {
        #region Ctor
        public JiraIssue()
        {
        }

        public JiraIssue(JiraIssue other)
        {
            Key = other.Key;
            IsSubtask = other.IsSubtask;
            Summary = other.Summary;
            CreatedDate = other.CreatedDate;
            Progress = other.Progress;
            SubTasks = other.SubTasks;
            IssueType = other.IssueType;
            Remaining = other.Remaining;
            Status = other.Status;
            Assignee = other.Assignee;
            Sprint = other.Sprint;
            OldStatus = other.OldStatus;
            OldCalcedStatus = other.OldCalcedStatus;
            Parent = other.Parent;
            StoryPoints = other.StoryPoints;
        }

        #endregion

        #region Public Methods

        public void AddSubtask(JiraSubTask subtask)
        {
            if (SubTasks == null)
                SubTasks = new List<JiraSubTask>();
            SubTasks.Add(subtask);
            _calcedStatus = StatusEnum.CalcRequired;
        }

        internal static string ToCsvHeader()
        {
            return "Sprint,Assignee,Key,Summary,IssueType,StoryPoints,Status,CalcedStatus,IsSubtask";
        }

        internal string ToCsv()
        {
            return Sprint + "," + Assignee + "," + Key + "," + Summary.Replace(",", "-") + "," + IssueType + "," + StoryPoints + "," + Status + "," + CalcedStatus + "," + IsSubtask;
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
            if (CalcedStatus.ToString().ToUpper().Contains(filter))
                return true;
            if (Assignee != null && Assignee.ToUpper().Contains(filter))
                return true;

            return false;
        }

        internal JiraIssue UpdateFromOld(IEnumerable<JiraIssue> oldIssues)
        {
            var oldIssue = oldIssues.SingleOrDefault(i => i.Key == Key);
            if (oldIssue != null)
            {
                OldStatus = oldIssue.Status;
                OldCalcedStatus = oldIssue.CalcStatus();
            }
            return oldIssue;
        }

        #endregion

        #region Members/Public Auto-Properties

        public string Key { get; private set; }
        public bool IsSubtask { get; private set; }
        public string Summary { get; private set; }
        public DateTime? CreatedDate { get; private set; }
        public int Progress { get; private set; }
        public List<JiraSubTask> SubTasks { get; private set; }
        public string IssueType { get; private set; }
        public string Remaining { get; set; }
        public StatusEnum Status { get; private set; }
        public string Assignee { get; set; }
        public string Sprint { get; set; }

        public StatusEnum OldStatus { get; set; }
        public StatusEnum OldCalcedStatus { get; set; }

        public string Parent { get; set; }
        public double StoryPoints { get; set; }

        #endregion

        #region Calced Properties

        StatusEnum _calcedStatus = StatusEnum.CalcRequired;
        public StatusEnum CalcedStatus
        {
            get
            {
                if (_calcedStatus == StatusEnum.CalcRequired)
                    _calcedStatus = CalcStatus();
               return _calcedStatus;
            }
        }

        private StatusEnum CalcStatus()
        {
            var rv = StatusEnum.Unknown;
            if (SubTasks.Count() == 0)
            {
                rv = Status;
            }
            else
            {
                 foreach (var subTask in SubTasks)
                {
                    if (subTask.IsDocTask)
                        continue;
                    rv = CompareStatus(rv, subTask.Status);
                }
                if (rv == StatusEnum.Open)
                {
                    // if I was going to say open, check if there is any progress
                    if (SubTasks.Any(t => t.Status == StatusEnum.InProgress))
                        rv = StatusEnum.InProgress;
                    else if (SubTasks.Any(t => t.IsResolved))
                        rv = StatusEnum.Partial;
                }
                else if (IsResolvedEnum(rv))
                {
                    // if everything else is done, check for hanging doc tasks
                    var docTask = SubTasks.FirstOrDefault(t => t.IsDocTask);
                    // if there are more than one than randomly pick one
                    if (docTask != null)
                    {
                        if (!IsResolvedEnum(docTask.Status))
                            rv = StatusEnum.Doc;
                    }
                }
                if (rv == StatusEnum.Closed)
                    rv = StatusEnum.Resolved;
            }
            return rv;
        }

        private bool IsDocTask
        {
            get
            {
                return IssueType.Contains("Doc") || Summary == "Doc";
            }
        }

        static private bool IsResolvedEnum(StatusEnum statusEnum)
        {
            return statusEnum == StatusEnum.Resolved || statusEnum == StatusEnum.Closed;
        }

        private StatusEnum CompareStatus(StatusEnum status, StatusEnum subStatus)
        {
            if (subStatus < status)
                return subStatus;
            return status;
        }

        public bool IsResolved
        {
            get
            {
                return IsResolvedEnum(CalcedStatus);
            }
        }

        public bool WasResolved
        {
            get
            {
                return IsResolvedEnum(OldCalcedStatus);
            }
        }

        public bool IsOpen
        {
            get
            {
                return CalcedStatus <= StatusEnum.Open;
            }
        }
        public bool IsTesting
        {
            get
            {
                return CalcedStatus == StatusEnum.ReadyForTesting || CalcedStatus == StatusEnum.InTesting;
            }
        }
        public bool IsDoc
        {
            get
            {
                return CalcedStatus == StatusEnum.Doc;
            }
        }
        public bool IsOnHold
        {
            get
            {
                return CalcedStatus == StatusEnum.OnHold || CalcedStatus == StatusEnum.CodeReview;
            }
        }

        public bool IsInProgress
        {
            get
            {
                return CalcedStatus == StatusEnum.InProgress || CalcedStatus == StatusEnum.Partial;
            }
        }

        public enum StatusEnum
        {
            CalcRequired = -2,
            Reopened = -1,
            Open = 0,
            InProgress,
            Partial,
            OnHold,
            CodeReview,
            ReadyForTesting,
            InTesting,
            Doc,
            Resolved = 10,
            Closed,
            Unknown = 99
        }
        public static StatusEnum GetStatusEnum(string status)
        {
            if (status == "Unknown")
                return StatusEnum.Unknown;
            else if (status == "Doc")
                return StatusEnum.Doc;
            else if (status == "Reopened")
                return StatusEnum.Reopened;
            else if (status == "Open")
                return StatusEnum.Open;
            else if (status == "Partial")
                return StatusEnum.Partial;
            else if (status == "In Progress")
                return StatusEnum.InProgress;
            else if (status == "On Hold")
                return StatusEnum.OnHold;
            else if (status == "Code Review")
                return StatusEnum.CodeReview;
            else if (status == "Ready for Testing")
                return StatusEnum.ReadyForTesting;
            else if (status == "In Testing")
                return StatusEnum.InTesting;
            else if (status == "Resolved")
                return StatusEnum.Resolved;
            else if (status == "Closed")
                return StatusEnum.Closed;

            return StatusEnum.Unknown;

        }

        #endregion

        #region JSON

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
                    rv.Status = GetStatusEnum((string) status["name"]);
                }

                if (fields["progress"] != null)
                {
                    var progress = fields["progress"];
                    rv.Progress = (int)progress["progress"];
                }
                var time = fields["timetracking"];
                if (time != null)
                {
                    rv.Remaining = (string)time["remainingEstimate"];
                    if (rv.Remaining == "0m")
                        rv.Remaining = "-";
                }
                var parent = fields["parent"];
                if (parent != null)
                    rv.Parent = (string)parent["key"];
                rv.Assignee = GetString(fields, "assignee", "name");
                var subs = fields["subtasks"];
                rv.SubTasks = new List<JiraSubTask>();
                if (subs != null && subs.Count() > 0)
                {
                    foreach (var issue in subs)
                    {
                        rv.SubTasks.Add(JiraIssue.Parse(issue) as JiraSubTask);
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

        #endregion

        #region HTML

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

        public string MailToLink
        {
            get
            {
                return "mailto:?subject=" + Key + ":" + ConvertToUrl(Summary);
            }
        }

        public string UniqueId
        {
            get
            {
                return Key;
            }
        }

        public static string ConvertToUrl(string text)
        {
            //text = text.Replace("%20", " ");
            text = text.Replace("<", "%3C");
            text = text.Replace(">", "%3E");
            text = text.Replace("/", "%2F");
            text = text.Replace(":", "%3A");
            text = text.Replace(" ", "%20");
            text = text.Replace("\"", "%22");
            text = text.Replace("[", "%5B");
            text = text.Replace("]", "%5D");
            return text;
        }

        #endregion

        #region Overrides

        public override int GetHashCode()
        {
            return ToCsv().GetHashCode();
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

        #endregion

        #region Interfaces

        public int CompareTo(JiraIssue other)
        {
            return Key.CompareTo(other.Key);
        }

        public int CompareTo(object obj)
        {
            return CompareTo(obj as JiraIssue);
        }

        static int _forTesting = 100;
        public static JiraIssue ForTesting(string key, string summary, StatusEnum status, string type)
        {
            var rv = new JiraIssue();
            if (key == null)
                key = "TST-" + _forTesting++;
            rv.Key = key;
            rv.Summary = summary;
            rv.Status = status;
            rv.IssueType = type;
            return rv;
        }

        #endregion

        #region Static Members

        public static string IssueTypeField = "issuetype";
        public static string StoryPointField = "customfield_10004";

        #endregion

    }

    public class JiraSprint
    {
        public JiraSprint()
        {
            Issues = new List<JiraIssue>();
        }
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
            rv.Issues.AddRange(jIssues.Where(i => i.IsSubtask == false).Select(i => new JiraIssue(i)));
            foreach (var subtask in jIssues.Where(i => i.IsSubtask))
            {
                var parent = rv.Issues.SingleOrDefault(i => i.Key == subtask.Parent);
                if (parent != null)
                    parent.AddSubtask(subtask as JiraSubTask);
            }
            return rv;
        }

        internal void Merge(JiraSprint jiraSprint)
        {
            if (jiraSprint == null)
            {
                return;
            }
            else if (jiraSprint.Issues.Count() == 0 || jiraSprint.ErrorStatus != null)
            {
                if (ErrorStatus == null)
                    ErrorStatus = jiraSprint.ErrorStatus;
            }
            else
            {
                if (jiraSprint.RetrieveTime > RetrieveTime)
                    RetrieveTime = jiraSprint.RetrieveTime;
                Issues.AddRange(jiraSprint.Issues);
            }
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
            if (old.Issues.Count() > 0)
            {
                foreach (var issue in Issues)
                {
                    var oldIssue = issue.UpdateFromOld(old.Issues.Select(i => (JiraIssue) i));
                    if (oldIssue != null)
                    {
                        foreach (var subTask in issue.SubTasks)
                            subTask.UpdateFromOld(oldIssue.SubTasks);
                    }
                }
            }
            OldRetrieveTime = old.RetrieveTime;
            OldErrorStatus = old.ErrorStatus;
        }

        public DateTime RetrieveTime { get; set; }
        public DateTime OldRetrieveTime { get; set; }

        public string ErrorStatus { get; set; }

        public string Status
        {
            get
            {
                if (ErrorStatus != null)
                    return ErrorStatus;
                return RetrieveTime.ToString(DateFormatString);
            }
        }
        public string OldStatus
        {
            get
            {
                if (OldErrorStatus != null)
                    return OldErrorStatus;
                return OldRetrieveTime.ToString(DateFormatString);
            }
        }
        public static string DateFormatString
        {
            get
            {
                return @"M/d HH:mm";
            }
        }
        public string OldErrorStatus { get; set; }
    }
    public class SprintKey
    {
        public SprintKey(string sprintText)
        {
            var parts = sprintText.Split(" ".ToCharArray());
            if (parts.Count() == 3) // doesn't handle one-word sprint names right now
            {
                Project = parts[0];
                Sprint = parts[1] + " " + parts[2];
            }
            else
            {
                //MessageBox.Show("Bad Sprint Text Format- should be like 'MOB Sprint 25' not " + sprintText);
                Project = "MOB";
                Sprint = "Sprint 1";
            }
        }
        public SprintKey(string project, string sprint)
        {
            Project = project;
            Sprint = sprint;
        }
        public string Project { get; set; }
        public string Sprint { get; set; }

        internal string ToFilename()
        {
            return Project + "-" + Sprint;
        }
    }

}
