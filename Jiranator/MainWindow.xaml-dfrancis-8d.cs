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
using System.Windows.Data;

namespace Jiranator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<JiraIssue> Issues { get; set; }

        string _project;
        public MainWindow()
        {
            Issues = new ObservableCollection<JiraIssue>();
            _project = Properties.Settings.Default.Project;
            InitializeComponent();
            ReadControl(entSprint);
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
        // based on resolved
        private bool ShowIssue(JiraIssue issue)
        {
            var rv = true;
            if (!string.IsNullOrWhiteSpace(_filter))
                rv = issue.Contains(_filter);
            else if (ShowResolved)
                rv = true;
            else if (issue.IsResolved && issue.WasResolved)
                rv = false;
            else
                rv = true;

            return rv;
        }
        private void AddIssue(JiraIssue issue)
        {
            Issues.Add(issue);
            //lstIssues.Items.Add(issue);
            //lst.Items.Insert(0, o.ToString());
        }

        static string EncodedCredentials
        {
            get
            {
                return JiraAccess.GetEncodedCredentials("orashkevych", "roadnet");
            }
        }
        static string HttpGet(string url)
        {
            string result = null;
            try
            {
                var request = WebRequest.Create(url) as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "GET";
                request.Headers.Add("Authorization", "Basic " + EncodedCredentials);
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
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

                request.Headers.Add("Authorization", "Basic " + EncodedCredentials);
                string result = null;
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }

                return result;

            }
            catch (Exception exc)
            {
                return exc.Message;
            }
        }

        static string HttpPost(string url, string json)
        {
            try
            {
                var request = WebRequest.Create(url)
                                     as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "POST";
                var credentials = JiraAccess.GetEncodedCredentials("ACc956df05e21814b549cf86aecef061f0", "9f1154b6bd7d37ee75926bc41a1c7014");
                request.Headers.Add("Authorization", "Basic " + credentials);

                //request.Credentials = new NetworkCredential("ACc956df05e21814b549cf86aecef061f0", "9f1154b6bd7d37ee75926bc41a1c7014");
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                string result = null;
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }

                return result;

            }
            catch (Exception exc)
            {
                return exc.Message;
            }
        }

        JiraIssue GetIssue(string key)
        {
            var str = HttpGet(JiraAccess.GetIssueUri(key));
            if (str == null)
                return null;
            return JiraIssue.Parse(JObject.Parse(str));
        }
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }

        private void Search()
        {
            btnSearch.IsEnabled = false;
            SaveControl(entSprint);
            var bw = new BackgroundWorker();
            bw.DoWork += bw_DoWork;
            bw.RunWorkerCompleted += bw_RunWorkerCompleted;
            dynamic obj = new { Sprints = entSprint.Text, Current = LoadEnum.Live, Old = LoadEnum.Yesterday};
            bw.RunWorkerAsync(obj);
        }

        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

            _jiraSprint = e.Result as JiraSprint;

            sta.Text = _jiraSprint.RetrieveTime + " vs " + _jiraSprint.OldRetrieveTime;

            RefreshIssueList();
            btnSearch.IsEnabled = true;
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            dynamic dyn = e.Argument;
            var rv = GetIssues(dyn.Sprints, dyn.Current, dyn.Old);
            var bw = sender as BackgroundWorker;
            e.Result = rv;
        }

        IEnumerable<string> GetSprintList(string sprintText)
        {
            IList<string> rv;
            if (sprintText.Contains(","))
            {
                rv = sprintText.Split(",".ToCharArray()).Select(s => s.Trim()).ToList();
            }
            else if (sprintText.Contains("-"))
            {
                var parts = sprintText.Split(" -".ToCharArray());
                var start = int.Parse(parts[parts.Count() - 2]);
                var end = int.Parse(parts[parts.Count() - 1]);
                rv = new List<string>();
                for (int i = start; i <= end; i++)
                    rv.Add(parts[0] + " " + i);
            }
            else
                rv = new List<string>() { sprintText };
            return rv;
        }
        private JiraSprint GetIssues(string sprintText, LoadEnum latest, LoadEnum old)
        {
            var parts = GetSprintList(sprintText);

            var jiraSprintOld = new JiraSprint();

            if (parts.Count() == 1)
            {
                var single = parts.First();
                jiraSprintOld.Issues = new List<JiraIssue>();
                jiraSprintOld.Merge(GetJiraSprint(_project, single, old));
            }
            var jiraSprint = new JiraSprint();
            jiraSprint.Issues = new List<JiraIssue>();
            foreach (var part in parts)
                jiraSprint.Merge(GetJiraSprint(_project, part, latest));

            if (parts.Count() == 1)
            {
                if (jiraSprintOld.Issues.Count > 0)
                    jiraSprint.UpdateOldStatus(jiraSprintOld);
            }
            
            return jiraSprint;
        }

        private void SaveControl(TextBox ent)
        {
            File.WriteAllText(ent.Name + ".cfg", ent.Text);
        }

        private void ReadControl(TextBox ent)
        {
            if (File.Exists(ent.Name + ".cfg"))
                ent.Text = File.ReadAllText(ent.Name + ".cfg");
        }

        JiraSprint _jiraSprint;
        private string GetNewFileName(string project, string sprint)
        {
            Directory.CreateDirectory(_dir);
            return Path.Combine(_dir, project + "-" + sprint + "-" + DateTime.Now.ToString("yyyyMMdd HHmmss") + ".json");
        }
        string _dir = @"C:\Users\Daniel\SkyDrive\Data\Jiranator";
        private string GetLatestFileName(string project, string sprint, LoadEnum load)
        {
            Directory.CreateDirectory(_dir);
            var files = Directory.GetFiles(_dir, project + "-" + sprint + "-" + "*" + ".json");
            IEnumerable<string> subFiles;
            if (load == LoadEnum.Yesterday)
                subFiles = files.Where(f => DateTimeFromFileName(f) < DateTime.Today);
            else
                subFiles = files;

            if (subFiles == null || subFiles.Count() == 0)
                return null;
            return subFiles.OrderByDescending(f => DateTimeFromFileName(f)).First();
        }
        
        private static DateTime? DateTimeFromFileName(string filename)
        {
            var parts = Path.GetFileName(filename).Split("-.".ToCharArray());
            DateTime? rv = null;
            try
            {
                rv = DateTime.ParseExact(parts[2], "yyyyMMdd HHmmss", null);
            }
            catch (Exception)
            {
            }
            return rv;
        }

        private JiraSprint GetJiraSprint(string project, string sprint, LoadEnum load)
        {
            string str;
            DateTime dt;
            if (load == LoadEnum.Live)
            {
                //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
                str = HttpGet(JiraAccess.GetSprintUri(project, sprint));
                dt = DateTime.Now;
                //File.WriteAllLines("search.fancy.json", SplitLinesDeep(str));
                File.WriteAllText(GetNewFileName(project, sprint), str);
            }
            else
            {
                var filename = GetLatestFileName(project, sprint, load);
                if (filename == null)
                    return null;
                dt = DateTimeFromFileName(filename) ?? DateTime.MinValue;
                try
                {
                    str = File.ReadAllText(filename);

                }
                catch (IOException)
                {
                    return null;
                }
            }
            var jiraSprint = JiraSprint.Parse(JObject.Parse(str));
            foreach (var issue in jiraSprint.Issues)
                issue.Sprint = sprint;
            jiraSprint.RetrieveTime = dt;
            return jiraSprint;
        }

        bool ShowSubtasks
        {
            get
            {
                return chkShowSubtasks.IsChecked == true;
            }
        }

        bool ShowResolved
        {
            get
            {
                return chkShowResolved.IsChecked == true;
            }
        }

        private void RefreshIssueList()
        {
            Issues.Clear();
            if (_jiraSprint == null || _jiraSprint.Issues == null)
                return;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lstIssues.ItemsSource);
            var hasFilter = !string.IsNullOrWhiteSpace(_filter);
            if (ShowSubtasks || hasFilter)
                view.SortDescriptions.Clear();
            else
                view.SortDescriptions.Add(new SortDescription("FauxStatus", ListSortDirection.Ascending));


            foreach (var issue in _jiraSprint.Issues)   //.Where(i => i.IsSubtask == false))
            {
                if (ShowIssue(issue))
                    AddIssue(issue);
                if (ShowSubtasks || hasFilter)
                {
                    foreach (var subtask in issue.SubTasks)
                    {
                        if (ShowIssue(issue))
                            AddIssue(subtask);
                    }
                }
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
            var issue = lstIssues.SelectedItem as JiraIssue;

            CopyToClipboard(issue);

        }

        private static void CopyToClipboard(JiraIssue item)
        {
            DataObject dataObject = new DataObject();
            dataObject.SetText(item.ToString());
            dataObject.SetText(item.HtmlDescription, TextDataFormat.Html);
            Clipboard.SetDataObject(dataObject);
        }
        private void lstIssues_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            btnOpen_Click(null, null);
        }

        private void btnUnassign_Click(object sender, RoutedEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;
            var str = HttpPut(JiraAccess.GetUnassignUri(issue.Key), JiraAccess.GetUnassignBody());
            Search();
            //Refresh(issue.Key);
        }
        /*
        private void Refresh(string key)
        {
            var issue = GetIssue(key);
            var oldIssue = Issues.Single(i => i.Key == key);
            oldIssue.Assignee = issue.Assignee;
            oldIssue.OnPropertyChanged("Assignee");
        }
        */
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
            File.WriteAllLines("issues.csv", outs.ToArray());
            Process.Start("issues.csv");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _jiraSprint = GetIssues(entSprint.Text, LoadEnum.Latest, LoadEnum.Yesterday);
            sta.Text = _jiraSprint.RetrieveTime + " vs " + _jiraSprint.OldRetrieveTime;
            RefreshIssueList();
            Search(); // kick off async refresh
        }

        private void chkShow_Click(object sender, RoutedEventArgs e)
        {
            RefreshIssueList();
        }

        private void btnOpenOnBoard_Click(object sender, RoutedEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;
            Process.Start(issue.LinkOnBoard);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;
            Process.Start(issue.LinkDirect);
        }

        string _filter;
        private void entFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filter = entFilter.Text;
            RefreshIssueList();
        }

        private void btnClean_Click(object sender, RoutedEventArgs e)
        {
            var files = Directory.GetFiles(_dir, "*" + ".json");
            var datedFiles = files.Where(f => DateTimeFromFileName(f) != null).ToArray();
            var groupedDatedFiles = datedFiles.GroupBy(f =>Path.GetFileName(f).Substring(0,22));
            var saved = new List<string>();
            foreach(var kvp in groupedDatedFiles)
            {
                saved.Add(kvp.First());
                if (!saved.Contains(kvp.Last()))
                    saved.Add(kvp.Last());
            }
            foreach (var datedFile in datedFiles)
            {
                if (!saved.Contains(datedFile))
                    File.Delete(datedFile);
            }
        }

        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            var issue = GetIssue(entFilter.Text);
            if (issue == null)
                return;
            _jiraSprint.Issues.Add(issue);
            RefreshIssueList();
            sta.Text = "Searched for " + entFilter.Text;
        }

        private void btnMail_Click(object sender, RoutedEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;
            CopyToClipboard(issue);
            Process.Start(issue.MailToLink);

        }
        private void btnText_Click(object sender, RoutedEventArgs e)
        {
            var issue = lstIssues.SelectedItem as JiraIssue;

            var baseUrl = @"https://api.twilio.com/2010-04-01";
            var sid = "ACc956df05e21814b549cf86aecef061f0";
            var token = "9f1154b6bd7d37ee75926bc41a1c7014";
            var client = new Twilio.TwilioRestClient(sid, token);
            var from = "+14433932877";
            var to = "+14109608923";
            var body = DateTime.Now.ToLongTimeString() + " " + issue.ToString();
            var rv = client.SendMessage(from, to, body);
        }
    }
}
