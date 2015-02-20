using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Kanga
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<JiraIssue> Issues{ get; set; }
        bool _testing = false;
        string _filename;
        public MainWindow()
        {
            InitializeComponent();
            bool consoleMode = (Environment.GetCommandLineArgs().Length > 1);
            if (consoleMode)
            {
                var args = Environment.GetCommandLineArgs();
                var source = JiraSourceEnum.Omnitracs;
                var project = ProjectEnum.RTS;
                var version = string.Empty;
                if (args.Count() == 2)
                {
                    project = (ProjectEnum)Enum.Parse(typeof(ProjectEnum), args[1]);
                }
                else if (args.Count() == 3)
                {
                    source = (JiraSourceEnum)Enum.Parse(typeof(JiraSourceEnum), args[1]);
                    project = (ProjectEnum)Enum.Parse(typeof(ProjectEnum), args[2]);
                }
                else if (args.Count() == 4)
                {
                    source = (JiraSourceEnum)Enum.Parse(typeof(JiraSourceEnum), args[1]);
                    project = (ProjectEnum)Enum.Parse(typeof(ProjectEnum), args[2]);
                    version = args[3];
                }
                Kangate(source, project, version, consoleMode);
                Close();
            }
            if (_testing)
                Title += " TESTING";
            foreach (var e in Enum.GetNames(typeof(JiraSourceEnum)))
                cmbSource.Items.Add(e);
            cmbSource.Text = JiraSourceEnum.Omnitracs.ToString();
            foreach (var e in Enum.GetNames(typeof(ProjectEnum)))
                cmbProject.Items.Add(e);
            cmbProject.Text = ProjectEnum.RTS.ToString();
        }

        string _none = "-none-";

        private void Kangate(JiraSourceEnum source, ProjectEnum project, string version, bool consoleMode)
        {
            try
            {
                string str;
                var key = source + "-" + project;
                if (!string.IsNullOrWhiteSpace(version))
                    key += "v" + version;
                var filename = "kanga-" + key + ".json";
                if (_testing && File.Exists(filename))
                    str = File.ReadAllText(filename);
                else
                {
                    str = JiraHttpAccess.GetLive(source, project, version, true);
                    if (!string.IsNullOrWhiteSpace(str))
                        File.WriteAllText(filename, str);
                }
                if (str == null)
                {
                    if (!consoleMode)
                        MessageBox.Show("Nothing returned");
                    return;
                }
                var json = JObject.Parse(str);
                var jsonIssues = json["issues"];
                var issues = new List<JiraIssue>();
                foreach (var jsonIssue in jsonIssues)
                {
                    var issue = JiraIssue.Parse(jsonIssue);
                    issues.Add(issue);
                }

                Issues = issues.OrderBy(i => i.Key).ToList();

                var outs = new List<string>();
                outs.Add(JiraIssue.CsvHeader());
                foreach (var issue in Issues)
                    outs.Add(issue.ToCsv());
                _filename = "kanga " + key + " " + DateTime.Now.ToString("yyyyMMdd HHmmss") + ".csv";
                File.WriteAllLines(_filename, outs.ToArray());

                if (!consoleMode)
                {
                    if (source == JiraSourceEnum.Omnitracs)
                    {
                        ColVis("Story", false);
                        ColVis("Description", false);
                        ColVis("Read Me Notes", true);
                        ColVis("Resolution Notes", true);
                    }
                    else
                    {
                        ColVis("Story", true);
                        ColVis("Description", true);
                        ColVis("Read Me Notes", false);
                        ColVis("Resolution Notes", false);
                    }
                    lstIssues.ItemsSource = Issues.OrderByDescending(i => i.IssueType);
                    staInfo.Text = Issues.Count() + " Issues Found";
                    if (Issues.Count() == JiraAccess.MAX_RETURN_ROWS)
                        staInfo.Text += " MAXED";
                }
            }
            catch (Exception exc)
            {
                File.AppendAllText("error.log", DateTime.Now.ToString() + " " + exc.ToString() + Environment.NewLine);
                if (!consoleMode)
                    MessageBox.Show("Error- call Dan- " + exc.Message);
            }
        }

        private void ColVis(string header, bool visible)
        {
            var view = lstIssues.View as GridView;
            var columns = view.Columns;

            var col = columns.SingleOrDefault(c => c.Header.ToString() == header);
            if (visible)
                col.Width = 500;
            else
                col.Width = 0;
            return;
        }

        private void btnOpenCsv_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_filename))
                Process.Start(_filename);
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            var source = (JiraSourceEnum)Enum.Parse(typeof(JiraSourceEnum), cmbSource.Text);
            var project = (ProjectEnum)Enum.Parse(typeof(ProjectEnum), cmbProject.Text);
            Kangate(source, project, entVersion.Text, false);

        }
    }
    public class JiraIssue
    {
        static public string CsvHeader()
        {
            return "Key,Case,Summary,Description,Status,Issue Type,Assignee,Fixed Version(s),Fixed Build #,Story,Read Me Notes,Resolution Notes";
        }
        public string ToCsv()
        {
            return Key + "," + CaseNumber + "," + Fixup(Summary) + "," + Fixup(Description) + "," + Status + "," + IssueType + "," + Assignee + "," + VersionsString + "," + FixedBuild + "," + Fixup(Story) + "," + Fixup(ReadMeNotes) + "," + Fixup(ResolutionNotes);
        }
        static string MinorFixup(string str)
        {
            if (str == null)
                return null;
            var rv = str;
            rv = rv.Replace("\"", "'");
            rv = rv.Replace(Environment.NewLine, " ");
            return rv;
        }
        static string Fixup(string str)
        {
            return "\"" + MinorFixup(str) + "\"";
        }
        #region Fields
        public List<string> Versions { get; private set; } = new List<string>();

        public string IssueType { get; private set; }
        public string Key { get; private set; }
        public string CaseNumber { get; private set; }
        string _summary;
        public string Summary
        {
            get
            {
                return _summary;
            }
            set
            {
                _summary = MinorFixup(value);
            }
        }
        string _story;
        public string Story
        {
            get
            {
                return _story;
            }
            set
            {
                _story = MinorFixup(value);
            }
        }
        string _description;
        public string Description
        {
            get
            {
                return _description;
            }
            set
            {
                _description = MinorFixup(value);
            }
        }
        public StatusEnum Status { get; private set; }
        public string Assignee { get; private set; }
        string _readMeNotes;
        public string ReadMeNotes
        {
            get
            {
                return _readMeNotes;
            }
            set
            {
                _readMeNotes = MinorFixup(value);
            }
        }
        string _resolutionNotes;
        public string ResolutionNotes
        {
            get
            {
                return _resolutionNotes;
            }
            set
            {
                _resolutionNotes = MinorFixup(value);
            }
        }

        #endregion

        public static string ResolutionNotesField = "customfield_10000";
        public static string ReadMeNotesField = "customfield_10001";
        public static string CaseField = "customfield_10002";
        public static string FixedBuildField = "customfield_10117";
        public static string StoryField = "customfield_10100";
        public string VersionsString
        {
            get
            {
                return ArrayToString(Versions);
            }
        }

        public string FixedBuild { get; private set; }

        public static string ArrayToString(List<string> array)
        {
            if (array == null)
                return "-";
            return string.Join("|", array.OrderBy(a => a).ToArray());
        }

        internal static JiraIssue Parse(JToken json)
        {
            try
            {
                var rv = new JiraIssue();
                rv.Key = (string)json["key"];
                var fields = json["fields"];
                rv.Summary = (string)fields["summary"];
                var caseDir = (string)fields[CaseField];
                rv.CaseNumber = Path.GetFileNameWithoutExtension(caseDir);
                rv.ReadMeNotes = GetString(fields, ReadMeNotesField);
                rv.ResolutionNotes = GetString(fields, ResolutionNotesField);
                rv.IssueType = GetNameString(fields, "issuetype");;
                //var tokens = fields.Children();
                if (fields["status"] != null)
                {
                    var status = fields["status"];
                    rv.Status = GetStatusEnum((string)status["name"]);
                }

                rv.Assignee = GetNameString(fields, "assignee");
                GetArrayedItem(rv.Versions, fields["fixVersions"]);
                rv.FixedBuild = (string)fields[FixedBuildField];
                rv.Story = (string)fields[StoryField];
                rv.Description = (string)fields["description"];
                return rv;
            }
            catch (Exception exc)
            {
                throw;
            }
        }

        private static void GetArrayedItem(object versions, JToken jToken)
        {
            throw new NotImplementedException();
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
            var rv = StatusEnum.Unknown;
            status = status.Replace(" ", "");
            if (false == Enum.TryParse<StatusEnum>(status, out rv))
            {
                if (status == "ReadyforTesting")
                    rv = StatusEnum.ReadyForTesting;
            }
            return rv;
        }
        private static string GetString(JToken fields, string fieldName)
        {
            try
            {
                var field = fields[fieldName];
                if (field == null || field.Type != JTokenType.String)
                    return null;
                return (string)field;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private static string GetNameString(JToken fields, string fieldName)
        {
            try
            {
                var field1 = fields[fieldName];
                if (field1 == null || field1.HasValues == false)
                    return null;

                var field2 = field1["name"];
                if (field2 == null)
                    return null;
                return (string)field2;
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
                    var j = comp["name"];
                    var s = (string)j;
                    set.Add((string)comp["name"]);
                }
            }
        }



    }
    public static class JiraHttpAccess
    {
        public static string GetLive(JiraSourceEnum source, ProjectEnum project, string version, bool showError)
        {
            return HttpAccess.HttpGet(source, JiraAccess.GetSprintUri(source, project, version), showError);
        }
    }
    public static class HttpAccess
    {
        internal static string HttpGet(JiraSourceEnum source, string url, bool showError)
        {
            string result = null;
            try
            {
                var request = WebRequest.Create(url) as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "GET";
                request.Headers.Add("Authorization", "Basic " + EncodedCredentials(source));
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception exc)
            {
                if (showError)
                    MessageBox.Show(exc.Message);
            }

            return result;
        }

        static string EncodedCredentials(JiraSourceEnum source)
        {
            if (source == JiraSourceEnum.Omnitracs)
                return JiraAccess.GetEncodedCredentials("dfrancis", "mibos");
            else
                return JiraAccess.GetEncodedCredentials("orashkevych", "roadnet");
        }
    }
    public enum JiraSourceEnum
    {
        SDLC,
        Omnitracs
    }
    public enum ProjectEnum
    {
        RTS,
        RA
    }
    static public class JiraAccess
    {
        static public int MAX_RETURN_ROWS = 1000;
        static string GetUrl(JiraSourceEnum source)
        {
            if (source == JiraSourceEnum.Omnitracs)
                return @"https://jira.omnitracs.com/rest/api/latest";
            else
                return @"https://roadnetmobiledev.atlassian.net/rest/api/latest";
        }

        static public string GetEncodedCredentials(string username, string password)
        {
            string mergedCredentials = username + ":" + password;
            byte[] byteCredentials = UTF8Encoding.UTF8.GetBytes(mergedCredentials);
            return Convert.ToBase64String(byteCredentials);
        }

        static bool _getAllFields = false;  // gets all fields but takes forever

        internal static string GetSprintUri(JiraSourceEnum source, ProjectEnum project,string version)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            var jqlParts = new List<string>();
            string strProject;
            if (source == JiraSourceEnum.SDLC)
            {
                if (project == ProjectEnum.RTS)
                    strProject = "(project=MOB OR project=RP OR project=ATT)";
                else
                    strProject = "(project=Apex OR project=Insight)";
                jqlParts.Add(strProject);
                jqlParts.Add("(issuetype=Bug OR issuetype=Story OR issuetype=Task)");
                jqlParts.Add("(Status='Resolved' OR Status='Closed')");
            }
            else
            {
                jqlParts.Add("project=" + project.ToString());
                jqlParts.Add("Status='Resolved'");
            }
            if (!string.IsNullOrWhiteSpace(version))
                jqlParts.Add("fixVersion=" + version);

            var jql = string.Join(" AND ", jqlParts.ToArray());
            var request = @"/search?jql=" + jql + "&maxResults=" + MAX_RETURN_ROWS;
            if (!_getAllFields) // gets all fields but takes forever
            {
                var fields = new List<string>() { "summary", "assignee", "fixVersions", "status", "issuetype"};
                if (source == JiraSourceEnum.Omnitracs)
                {
                    fields.Add(JiraIssue.CaseField);
                    fields.Add(JiraIssue.ReadMeNotesField);
                    fields.Add(JiraIssue.ResolutionNotesField);
                    fields.Add(JiraIssue.FixedBuildField);
                }
                else
                {
                    fields.Add("description");
                    fields.Add(JiraIssue.StoryField);
                }
                request += "&fields=" + string.Join(",", fields);
            }

            var rv = GetUrl(source) + request;
            FileUtils.Log("GetSprint` " + rv);
            return rv;
        }
    }
    public static class FileUtils
    {
        internal static void Log(string str)
        {
            File.AppendAllText("endless.log", DateTimeOffset.Now + str + Environment.NewLine);
        }
    }

}
