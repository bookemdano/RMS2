using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Diagnostics;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Xml;

namespace Jiranator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<JiraIssue> Issues { get; set; }
        
        public MainWindow()
        {
            Issues = new ObservableCollection<JiraIssue>();
            InitializeComponent();
            lstIssues.ItemsSource = Issues;
        }

        protected override void OnInitialized(EventArgs e)
        {
            try
            {
                //QuickTest();
            }
            catch (Exception)
            {
            }

            base.OnInitialized(e);
        }

        public class Runner
        {
            public Runner(string name, int time, int age)
            {
                Name = name;
                Time = time;
                Age = age;
            }
            public string Name { get; set; }
            public int Time { get; set; }
            public int Age { get; set; }
        }
        public class Race
        {
            public string RaceName { get; set; }
            public List<Runner> Runners { get; set; }
        }
        private void QuickTest()
        {
            var race = new Race();
            race.RaceName = "The Big Race";
            race.Runners = new List<Runner>();
            race.Runners.Add(new Runner("Steve", 12, 33));
            race.Runners.Add(new Runner("Tim", 34, 28));
            race.Runners.Add(new Runner("Mark", 22, 37));
            race.Runners.Add(new Runner("Tom", 21, 30));
            race.Runners.Add(new Runner("Cliff", 13, 33));
            race.Runners.Add(new Runner("Vini", 17, 28));
            race.Runners.Add(new Runner("Matt", 10, 28));
            race.Runners.Add(new Runner("Ben", 9, 29));
            race.Runners.Add(new Runner("Brandon", 15, 14));
            var str = JsonConvert.SerializeObject(race);

            using (FileStream fs = File.Open(@"f:\temp\newhire.json", FileMode.OpenOrCreate))
            using (StreamWriter sw = new StreamWriter(fs))
            using (JsonWriter jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Newtonsoft.Json.Formatting.Indented;

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(jw, race);
            }

            str = @"{'?xml': {'@version': '1.0', '@standalone': 'no' },'root':" + str + "}";
            var doc = (XmlDocument)JsonConvert.DeserializeXmlNode(str);
            doc.Save(@"f:\temp\newhire.xml");

            //File.WriteAllText(@"f:\temp\newhire.json", str);
            //var rv = HttpPut(@"https://roadnetmobiledev.atlassian.net/rest/api/latest/issue/MOB-1051/assignee");
            //var rv = HttpPut(@"https://roadnetmobiledev.atlassian.net/rest/api/latest/issue/MOB-1051");

            QuickSearchTest();
            //QuickIssueTest();
        }
        private void QuickSearchTest()
        {
            var str = File.ReadAllText("search.json").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            var json = JObject.Parse(str);
            var issues = JiraSprint.Parse(json);
        }

        private void QuickIssueTest()
        {
            var str = File.ReadAllText("MOB.json").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            var json = JObject.Parse(str);
            //var json = Json.Parse(str);
            var issue = JiraIssue.Parse(json);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Issues.Clear();
            var issue = GetIssue(ent.Text);
            AddIssue(issue);
        }

        private void AddIssue(JiraIssue issue)
        {
            Issues.Add(issue);
            //lstIssues.Items.Add(issue);
            //lst.Items.Insert(0, o.ToString());
        }
        static private string GetEncodedCredentials(string username, string password)
        {
            string mergedCredentials = username + ":" + password;
            byte[] byteCredentials = UTF8Encoding.UTF8.GetBytes(mergedCredentials);
            return Convert.ToBase64String(byteCredentials);
        }

        static string HttpGet(string url)
        {
            var request = WebRequest.Create(url)
                                 as HttpWebRequest;
            request.ContentType = "application/json";
            request.Method = "GET";
            request.Headers.Add("Authorization", "Basic " + GetEncodedCredentials("unassigneddev", "roadnet"));
            string result = null;
            using (var resp = request.GetResponse() as HttpWebResponse)
            {
                var reader = new StreamReader(resp.GetResponseStream());
                result = reader.ReadToEnd();
                //File.WriteAllText("MOB.json", result);
            }

            return result;
        }
        static string HttpPut(string url, string json)
        {
            try
            {
                var request = WebRequest.Create(url)
                                     as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "PUT";

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                request.Headers.Add("Authorization", "Basic " + GetEncodedCredentials("unassigneddev", "roadnet"));
                string result = null;
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                    //File.WriteAllText("MOB.json", result);
                }

                return result;

            }
            catch (Exception exc)
            {

                return "nothing";
            }
        }

        string _latestApi = @"https://roadnetmobiledev.atlassian.net/rest/api/latest";
        JiraIssue GetIssue(string key)
        {
            string str;
            if (chkReally.IsChecked == true)
            {
                str = HttpGet(_latestApi + @"/issue/" + key + ".json");
                File.WriteAllLines(key + ".json", SplitLinesDeep(str));
            }
            else
                str = File.ReadAllText("MOB.json").Replace("\n", "").Replace("\r", "").Replace("\t", "");

            var issue = JiraIssue.Parse(JObject.Parse(str));
            return issue;
        }
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            var sprint = entSprint.Text;
            var parts = sprint.Split(",".ToCharArray());
            var jiraSprint = new JiraSprint();
            jiraSprint.Issues = new List<JiraIssue>();
            foreach (var part in parts)
                jiraSprint.Issues.AddRange(GetSprintItems(part.Trim()).Issues);
            _jiraSprint = jiraSprint;
            RefreshIssueList();
        }
        JiraSprint _jiraSprint;

        private JiraSprint GetSprintItems(string sprint)
        {
            string str;
            if (chkReally.IsChecked == true)
            {
                //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
                var request = @"/search?jql=project=MOB AND Sprint='" + sprint + "'&maxResults=200";
                if (false)
                    request += "&fields=parent,summary,assignee,status,estimate," + JiraIssue.IssueTypeField;
                str = HttpGet(_latestApi + request);
                //File.WriteAllLines("search.fancy.json", SplitLinesDeep(str));
                File.WriteAllText("search.json", str);
            }
            else
                str = File.ReadAllText("search.json");

            var jiraSprint = JiraSprint.Parse(JObject.Parse(str));
            foreach (var issue in jiraSprint.Issues)
                issue.Sprint = sprint;
            return jiraSprint;
        }

        private void RefreshIssueList()
        {
            Issues.Clear();
            if (_jiraSprint == null || _jiraSprint.Issues == null)
                return;
            foreach (var issue in _jiraSprint.Issues)   //.Where(i => i.IsSubtask == false))
            {
                AddIssue(issue);
                if (chkHideSubtasks.IsChecked != true)
                    foreach (var subtask in issue.SubTasks)
                        AddIssue(subtask);
            }
        }
        public static string[] SplitLinesDeep(string str)
        {
            var rv = "";
            int indent = 0;
            bool inQuotes = false;
            int iChar = 0;
            int quoteEnd = int.MinValue;
            char lastChar = char.MinValue;
            foreach (var c in str)
            {
                if (inQuotes)
                {
                    if (c == '\"')
                    {
                        inQuotes = false;
                        quoteEnd = iChar;
                        if (lastChar == '\\')
                            rv = rv.Trim('\\');
                    }
                }
                else
                {
                    if (c == '\"')
                    {
                        if (quoteEnd == iChar - 1)
                            continue;

                        inQuotes = true;
                    }
                }
                lastChar = c;
                rv += c;
                iChar++;
                if (inQuotes)
                    continue;
                if (c == '{' || c == '[')
                    indent++;
                else if (c == '}' || c == ']')
                    indent--;
                if (c == ',' || c == '{' || c == '[')
                    rv += Environment.NewLine + new string('\t', indent);
            }
            return rv.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            var item = lstIssues.SelectedItem as JiraIssue;

            DataObject dataObject = new DataObject();
            dataObject.SetText(item.ToString());
            dataObject.SetText(item.HtmlDescription, TextDataFormat.Html);
            Clipboard.SetDataObject(dataObject);

        }
        private void lstIssues_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;
            
            Process.Start(issue.Link(true));
        }

        private void btnUnassign_Click(object sender, RoutedEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;
            var str = HttpPut(_latestApi + @"/issue/" + issue.Key, "{\"fields\": {\"assignee\":{\"name\":\"\"}}}");
            Refresh(issue.Key);
        }

        private void Refresh(string key)
        {

            var issue = GetIssue(key);
            var oldIssue = Issues.Single(i => i.Key == key);
            oldIssue.Assignee = issue.Assignee;
            oldIssue.OnPropertyChanged("Assignee");
        }

        private void chkHideSubtasks_Click(object sender, RoutedEventArgs e)
        {
            RefreshIssueList();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            _jiraSprint = new JiraSprint();
            RefreshIssueList();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var outs = new List<string>();
            foreach(var issue in Issues)
            {
                outs.Add(issue.ToCsv());
            }
            File.WriteAllLines("outs.csv", outs.ToArray());
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
                var parent = rv.Issues.Single(i => i.Key == subtask.Parent);
                parent.AddSubtask(subtask);
            }
            return rv;
        }
    }

    public class JiraIssue : INotifyPropertyChanged
    {
        string _linkOnBoard = @"https://roadnetmobiledev.atlassian.net/secure/RapidBoard.jspa?rapidView=3&view=detail&selectedIssue=";
        string _linkDirect = @"https://roadnetmobiledev.atlassian.net/browse/";
        
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
        public static string IssueTypeField = "issuetype";
        public string Key { get; private set; }
        public bool IsSubtask { get; private set; }
        public string Summary { get; private set; }
        public DateTime? CreatedDate { get; private set; }
        public int Progress { get; private set; }
        public List<JiraIssue> SubTasks { get; private set; }
        public string IssueType { get; set; }
        public string Assignee { get; set; }
        public string Status { get; set; }
        public string Sprint { get; set; }
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

        public string Link(bool onBoard)
        {
            if (onBoard)
                return _linkOnBoard + Key;
            else
                return _linkDirect + Key;
        }
        public string HtmlDescription
        {
            get
            {
                string link = "<A HREF=" + Link(false) + ">" + Key + "</A>";
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
            return Sprint + "," + Assignee + "," + Key + "," + Summary.Replace(",","-") + "," + IssueType + "," + Status;
        }
    }
}
