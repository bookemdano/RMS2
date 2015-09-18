using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
// v1.0
namespace JiraShare
{
    [Flags]
    public enum ShowStatusEnum
    {
        None = 0x0,
        Other = 0x1,
        OnHold = 0x2,
        Testing = 0x4,
        Resolved = 0x8,
        Labels = 0x10
    }

    // TODO Allow setting and linking to salesforce item
    // TODO Show by Epic
    // TODO Show by compenent
    // TODO allow setting component
    // TODONE 3/5/2014 make sort column sticky but not persisted
    // TODONE 11/25/13 Show Partial and In Progress at master task level
    // TODONE 11/25/13 Make status enums
    // TODONE 7/28/14 Chart

    public enum LoadEnum
    {
        LiveAlways,
        LiveOnlyIfOld,
        Latest,
        Yesterday
    }

    public class JiraIssue : IComparable
    {
        #region Ctor
        public JiraIssue()
        {

        }

        public JiraIssue(JiraIssue other) : this()
        {
            Key = other.Key;
            IsSubtask = other.IsSubtask;
            Summary = other.Summary;
            CreatedDate = other.CreatedDate;
            Source = other.Source;
            Progress = other.Progress;
            EpicStatus = other.EpicStatus;
            EpicLink = other.EpicLink;
            CaseFiles = other.CaseFiles;
            Components = other.Components;
            FixVersions = other.FixVersions;
            AffectsVersions = other.AffectsVersions;
            Labels = other.Labels;
            SubTasks = other.SubTasks;
            IssueType = other.IssueType;
            Remaining = other.Remaining;
            Status = other.Status;
            Assignee = other.Assignee;
            Sprint = other.Sprint;
            OldStatus = other.OldStatus;
            OldCalcedStatus = other.OldCalcedStatus;
            Parent = other.Parent;
            ParentIssue = other.ParentIssue;
            StoryPoints = other.StoryPoints;
            foreach (var name in other.Sprints)
                Sprints.Add(name);

            {
                var namedDoc = Summary.ToUpper() == "DOC";
                var typedDoc = IssueType.Contains("Doc");

                if (typedDoc != namedDoc)
                    IssueType = "Kinda Doc";
            }
        }

        #endregion

        #region Public Methods

        public void AddSubtask(JiraIssue subtask)
        {
            if (SubTasks == null)
                SubTasks = new List<JiraIssue>();
            SubTasks.Add(subtask);
            _calcedStatus = StatusEnum.CalcRequired;
        }

        internal static string ToCsvHeader()
        {
            return "Sprint,Assignee,Key,Summary,IssueType,StoryPoints,Status,CalcedStatus,IsSubtask,Source,Versions";
        }

        internal string ToCsv()
        {
            return Sprint + "," + Assignee + "," + Key + "," + Summary.Replace(",", "-") + "," + IssueType + "," + StoryPoints + "," + Status + "," + CalcedStatus + "," + IsSubtask + "," + Source + "," + FixVersionsString;
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
            else
                return null;
            return oldIssue;
        }

        #endregion

        #region Members/Public Auto-Properties

        public string Key { get; private set; }
        public bool IsSubtask { get; private set; }
        public string Summary { get; private set; }
        public string EpicLink { get; private set; }
        public string EpicStatus { get; private set; }
        public DateTime? CreatedDate { get; private set; }
        public int Progress { get; private set; }
        public List<JiraIssue> SubTasks { get; private set; }
        public string IssueType { get; private set; }
        public string Remaining { get; set; }
        public StatusEnum Status { get; private set; }
        public string Assignee { get; set; }
        public string Sprint { get; set; }
        public string CaseFiles { get; private set; }

        public StatusEnum OldStatus { get; set; }
        public StatusEnum OldCalcedStatus { get; set; }

        public string Parent { get; set; }
        public double StoryPoints { get; set; }

        public string ShortIssueType
        {
            get
            {
                if (IssueType == "Hardware Test Request")
                    return "Hardware";
                else if (IssueType == "Improvement")
                    return "Improv";
                else
                    return IssueType;
            }
        }

        #endregion

        #region Calced Properties

        static Stopwatch _cummulative = new Stopwatch();

        StatusEnum _calcedStatus = StatusEnum.CalcRequired;
        public StatusEnum CalcedStatus
        {
            get
            {
                _cummulative.Start();
                if (_calcedStatus == StatusEnum.CalcRequired)
                    _calcedStatus = CalcStatus();
                _cummulative.Stop();
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
                var namedDoc = Summary.ToUpper() == "DOC";
                var typedDoc = IssueType.Contains("Doc");

                if (typedDoc != namedDoc)
                    return true;
                return typedDoc || namedDoc;
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
                return CalcedStatus == StatusEnum.TestReady || CalcedStatus == StatusEnum.InTesting;
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
                return CalcedStatus == StatusEnum.OnHold || CalcedStatus == StatusEnum.Blocked || CalcedStatus == StatusEnum.CodeReview;
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
            New = -3,
            CalcRequired = -2,
            Reopened = -1,
            Open = 0,
            InProgress,
            Partial,
            OnHold,
            Blocked,
            CodeReview,
            TestReady,
            InTesting,
            Doc,
            Resolved = 10,
            Closed,
            Unknown = 99
        }
        public static StatusEnum GetStatusEnum(string status)
        {
            var rv = StatusEnum.Unknown;
            status = status.Replace(" ", "");
            if (false == Enum.TryParse<StatusEnum>(status, out rv))
            {
                if (status == "ReadyforTesting")
                    rv = StatusEnum.TestReady;
            }
            return rv;
        }

        #endregion

        #region JSON
        internal static JiraIssue Parse(string str)
        {
            return Parse(JObject.Parse(str));
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
                    var str = (string)fields[StoryPointField];
                    double d;
                    if (double.TryParse(str, out d))
                        rv.StoryPoints = d;
                }
                rv.EpicLink = GetString(fields, EpicLinkField);
                rv.EpicStatus = GetString(fields, EpicStatusField);
                rv.CaseFiles = GetString(fields, CaseFilesField);
                if (fields[SprintField] != null)
                {
                    foreach (var sprintPart in fields[SprintField])
                    {
                        try
                        {
                            var contents = sprintPart.ToString();
                            var name = StringUtils.GetStringBetween(contents, "name=", ",");
                            SprintParameters.AddOrUpdateSprint(name, contents);
                            rv.Sprints.Add(name);
                        }
                        catch (Exception)
                        {
                            // skip it
                        }
                    }
                    //var start = (string) sprintField["startdate"];
                }
                var issueType = fields["issuetype"];
                rv.IssueType = (string)issueType["name"];
                var self = (string)issueType["self"];
                if (self.Contains(JiraAccess.SourceUrl(JiraSourceEnum.Omnitracs)))
                    rv.Source = JiraSourceEnum.Omnitracs;
                else
                    rv.Source = JiraSourceEnum.SDLC;
                rv.IsSubtask = (bool)issueType["subtask"];
                if (fields["created"] != null)
                    rv.CreatedDate = (DateTime)fields["created"];
                //var tokens = fields.Children();
                if (fields["status"] != null)
                {
                    var status = fields["status"];
                    rv.Status = GetStatusEnum((string)status["name"]);
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
                GetArrayedItem(rv.Components, fields["components"]);
                GetArrayedItem(rv.FixVersions, fields["fixVersions"]);
                GetArrayedItem(rv.AffectsVersions, fields["versions"]);
                GetArrayedItem(rv.Labels, fields["labels"]);

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
            catch (Exception)
            {
                throw;
            }
        }
        private static string GetString(JToken fields, string fieldName)
        {
            try
            {
                var field = fields[fieldName];
                if (field == null)
                    return null;

                if (field.Type == JTokenType.String)
                    return (string)field;
                if (field.HasValues)
                {
                    var valueField = field["value"];
                    if (valueField != null)
                        return (string)valueField;
                    var nameField = field["name"];
                    if (nameField != null)
                        return (string)nameField;
                }   
                return null;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void GetArrayedItem(List<string> set, JToken tokens)
        {
            if (tokens != null && tokens.Count() > 0)
            {
                foreach (var comp in tokens)
                {
                    if (comp.HasValues == false)
                    {
                        set.Add((string)comp);
                    }
                    else
                    {
                        var j = comp["name"];
                        var s = (string)j;
                        set.Add((string)comp["name"]);
                    }
                }
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
                return JiraAccess.LinkOnBoard(Source) + Key;
            }
        }
        public string LinkDirect
        {
            get
            {
                return JiraAccess.LinkDirect(Source) + Key;
            }
        }
        public List<string> Sprints { get; private set; } = new List<string>();
        public static object SprintParamters { get; private set; }
        public List<string> Components { get; private set; } = new List<string>();
        public List<string> FixVersions { get; private set; } = new List<string>();
        public List<string> AffectsVersions { get; private set; } = new List<string>();
        public List<string> Labels { get; private set; } = new List<string>();
        // don't forget to add to copy cont

        public string ComponentsString
        {
            get
            {
                _cummulative.Start();
                var rv = StringUtils.ArrayToString(Components);
                _cummulative.Stop();
                return rv;
            }
        }

        public string FixVersionsString
        {
            get
            {
                _cummulative.Start();
                var rv = StringUtils.ArrayToString(FixVersions);
                _cummulative.Stop();
                return rv;
            }
        }

        public string LabelsString
        {
            get
            {
                _cummulative.Start();
                var rv = StringUtils.ArrayToString(Labels);
                _cummulative.Stop();
                return rv;
            }
        }

        public string AffectsVersionsString
        {
            get
            {
                _cummulative.Start();
                var rv = StringUtils.ArrayToString(AffectsVersions);
                _cummulative.Stop();
                return rv;
            }
        }

        public string ToolTip
        {
            get
            {
                if (SubTasks.Count() == 0)
                    return Status.ToString();
                var rv = string.Join(Environment.NewLine, SubTasks.Select(s => s.Key + ":" + s.CalcedStatus + ", " + s.Summary));
                return rv;
            }
        }
        public JiraIssue ParentIssue { get; internal set; }
        public JiraSourceEnum Source { get; private set; } = JiraSourceEnum.SDLC;

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

        public static bool IsIssueKey(string str)
        {
            var subex = @"\w{2,6}-\d{1,5}";
            /*
            bool b;
            b = Regex.IsMatch("1112", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("123-1234", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("DEF-1234", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("123-", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("abc", subex);         //    "[^0-9](2,6)-[0-9](1,5)");

            var ex = subex;
            */
            return Regex.IsMatch(str, subex);         //    "[^0-9](2,6)-[0-9](1,5)");
        }


        #region Static Members

        public static string StoryPointField = "customfield_10004";
        public static string EpicLinkField = "customfield_10008";
        public static string EpicStatusField = "customfield_10010";
        public static string SprintField = "customfield_10007";
        public static string CaseFilesField = "customfield_10002";
        
        #endregion

    }

    public class JiraSet
    {
        public JiraSet()
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
        internal static JiraSet Parse(string str)
        {
            return Parse(JObject.Parse(str));
        }

        private static JiraSet Parse(JObject json)
        {
            var rv = new JiraSet();
            if (json["total"] == null)
            {
                rv.Issues.Add(JiraIssue.Parse(json));
                return rv;
            }
            var total = (int)json["total"];
            var max = (int)json["maxResults"];
            if (total == max)
                throw new Exception("Results maxed out. There may be more in Jira.");
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
                {
                    parent.AddSubtask(subtask);
                    subtask.ParentIssue = parent;
                }
            }
            return rv;
        }

        internal void Merge(JiraSet jiraSprint)
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
                if (Key == null)
                    Key = jiraSprint.Key;
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
        public void UpdateOldStatus(JiraSet old)
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
                    else
                    {
                        issue.OldStatus = JiraIssue.StatusEnum.New;
                        issue.OldCalcedStatus = JiraIssue.StatusEnum.New;
                    }
                }
            }
            OldRetrieveTime = old.RetrieveTime;
            _oldSprintStatus = old._oldSprintStatus;
        }

        internal void SetSprintName(string sprint)
        {
        }

        internal void SetSprintName(SprintKey sprintKey)
        {
            Key = sprintKey; 
            foreach (var issue in Issues)
                issue.Sprint = sprintKey.Sprint;
        }

        public DateTimeOffset RetrieveTime { get; set; }
        public DateTimeOffset OldRetrieveTime { get; set; }

        public string ErrorStatus { get; set; }

        public string SprintStatus
        {
            get
            {
                if (ErrorStatus != null)
                    return ErrorStatus;
                return RetrieveTime.ToString(DateFormatString);
            }
        }
        string _oldSprintStatus;
        public string OldSprintStatus
        {
            get
            {
                if (_oldSprintStatus != null)
                    return _oldSprintStatus;
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

        public SprintKey Key { get; set; }
    }

    public class SprintKey
    {
        public SprintKey()
        {

        }
        public SprintKey(string sprintText)
        {
            var parts = sprintText.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (parts.Count() == 3) // doesn't handle one-word sprint names right now
            {
                Project = parts[0];
                Sprint = parts[1] + " " + parts[2];
            }
            else if (parts.Count() == 2) // doesn't handle one-word sprint names right now
            {
                Project = parts[0];
                Sprint = parts[1];
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
        public override string ToString()
        {
            return ToFilename();
        }
        internal string ToFilename()
        {
            return Project + "-" + Sprint;
        }
    }
    public class SprintParameters
    {
        #region Static Part

        static public Dictionary<string, SprintParameters> Params { get; private set; } = new Dictionary<string, SprintParameters>();

        internal static bool ContainsSprint(string name)
        {
            return Params.ContainsKey(name);
        }

        internal static SprintParameters GetSprintParameters(string name)
        {
            if (Params.ContainsKey(name))
                return Params[name];
            else
                return null;
        }

        internal static void AddOrUpdateSprint(string name, string contents)
        {
            if (!SprintParameters.ContainsSprint(name))
                Params.Add(name, new SprintParameters(contents));
            else
                Params[name] = new SprintParameters(contents);
        }

        #endregion

        #region Instance Part

        public SprintParameters(string contents)
        {
            Contents = contents;
        }

        public string Contents{ get; set; }
        public DateTimeOffset StartDate
        {
            get
            {
                var str = StringUtils.GetStringBetween(Contents, "startDate=", ",");
                return DateTimeOffset.Parse(str);
            }
        }

        public DateTimeOffset EndDate
        {
            get
            {
                var str = StringUtils.GetStringBetween(Contents, "endDate=", ",");
                return DateTimeOffset.Parse(str);
            }
        }

        public string State
        {
            get
            {
                return StringUtils.GetStringBetween(Contents, "state=", ",");
            }
        }

        #endregion
    }
}
