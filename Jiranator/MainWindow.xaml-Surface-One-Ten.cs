using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Jiranator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODONE 12/12/13 Require project name in Sprint entry box, which allows showing many projects together
        ObservableCollection<JiraIssueViewModel> Issues { get; set; }
        public MainWindow()
        {
            Issues = new ObservableCollection<JiraIssueViewModel>();
            InitializeComponent();
            ReadControl(entSprint);
            ReadControl(chkAutoRefresh);
            ReadControl(chkShowResolved);
            ReadControl(chkShowSubtasks);
            ReadControl(chkShowTesting);
            ReadControl(tglChartType);
            lstIssues.ItemsSource = Issues;
            Test();
        }

        private void Test()
        {
            if (IsIssueKey("MOB-1") != true)
                throw new Exception();
            if (IsIssueKey("MOB-123") != true)
                throw new Exception();
            if (IsIssueKey("MOB-1234") != true)
                throw new Exception();
            if (IsIssueKey("MOB-12345") != true)
                throw new Exception();
            if (IsIssueKey("AP-1") != true)
                throw new Exception();
            if (IsIssueKey("AP-23411") != true)
                throw new Exception();
            if (IsIssueKey("Bob") != false)
                throw new Exception();
            if (IsIssueKey("iOS") != false)
                throw new Exception();
            if (IsIssueKey("enabled rmv") != false)
                throw new Exception();

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
            else if (issue.IsResolved)
                rv = false;
            else
                rv = true;
            if (rv && !ShowTesting && (issue.IsTesting || issue.IsDoc))
                rv = false;

            return rv;
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

        JiraIssue GetIssue(string key)
        {
            var str = HttpGet(JiraAccess.GetIssueJsonUri(key));
            if (str == null)
                return null;
            return JiraIssue.Parse(JObject.Parse(str));

        }

        List<JiraIssue> FindIssues(string text)
        {
            string url;
            if (IsIssueKey(text))
                url = JiraAccess.FindIssuesUri(text);
            else
                url = JiraAccess.SearchIssuesUri(text);
            var str = HttpGet(url);
            if (str == null)
                return null;
            try
            {
                //File.WriteAllText(Path.Combine(Dir, text + ".json"), str);
            }
            catch (Exception)
            {
                // just try it
            }

            var jiraSprint = JiraSprint.Parse(JObject.Parse(str));

            return jiraSprint.Issues;

        }
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            Update(LoadEnum.LiveAlways);
        }

        private void Update(LoadEnum liveLoad)
        {
            btnUpdate.IsEnabled = false;
            SaveControl(entSprint);
            var bw = new BackgroundWorker() { WorkerReportsProgress = true };
            bw.DoWork += bw_DoWork;
            bw.ProgressChanged += bw_ProgressChanged;
            bw.RunWorkerCompleted += bw_RunWorkerCompleted;
            dynamic obj = new { Sprints = entSprint.Text, Current = liveLoad, Old = LoadEnum.Yesterday };
            bw.RunWorkerAsync(obj);
        }

        void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            sta.Content = e.UserState as string;
        }

        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _jiraSprint = e.Result as JiraSprint;
            RefreshIssueList(false);
            btnUpdate.IsEnabled = true;
            btnUpdate.Foreground = Brushes.Black;
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            dynamic dyn = e.Argument;
            var bw = sender as BackgroundWorker;
            var rv = GetIssues(bw, dyn.Sprints, dyn.Current, dyn.Old);
            e.Result = rv;
        }

        IEnumerable<SprintKey> GetSprintList(string sprintText)
        {
            IList<SprintKey> rv;
            if (sprintText.Contains(","))
            {
                rv = sprintText.Split(",".ToCharArray()).Select(s => new SprintKey(s.Trim())).ToList();
            }
            else if (sprintText.Contains("-"))
            {
                var parts = sprintText.Split(" -".ToCharArray());
                var project = parts[0];
                var start = int.Parse(parts[parts.Count() - 2]);
                var end = int.Parse(parts[parts.Count() - 1]);
                rv = new List<SprintKey>();
                for (int i = start; i <= end; i++)
                    rv.Add(new SprintKey(project, parts[1] + " " + i));
            }
            else
                rv = new List<SprintKey>() { new SprintKey(sprintText) };
            return rv;
        }

        private string Project
        {
            get
            {
                var parts = entSprint.Text.Split(" -".ToCharArray());
                return parts[0];
            }
        }

        private JiraSprint GetIssues(BackgroundWorker bw, string sprintText, LoadEnum latest, LoadEnum old)
        {
            var parts = GetSprintList(sprintText);

            var jiraSprintOld = new JiraSprint();

            if (parts.Count() == 1)
            {
                var single = parts.First();
                jiraSprintOld.Issues = new List<JiraIssue>();
                jiraSprintOld.Merge(GetJiraSprint(bw, single, old));
            }
            var jiraSprint = new JiraSprint();
            jiraSprint.Issues = new List<JiraIssue>();
            foreach (var part in parts)
                jiraSprint.Merge(GetJiraSprint(bw, part, latest));

            if (parts.Count() == 1)
                jiraSprint.UpdateOldStatus(jiraSprintOld);
            else
                FileUtils.Log("Old not found");
            return jiraSprint;
        }

        private void SaveControl(TextBox ent)
        {
            SaveString(ent.Name, ent.Text);
        }

        private void SaveControl(ToggleButton tgl)
        {
            SaveString(tgl.Name, tgl.IsChecked);
        }

        private void SaveControl(CheckBox chk)
        {
            SaveString(chk.Name, chk.IsChecked);
        }

        private void ReadControl(TextBox ent)
        {
            ent.Text = ReadString(ent.Name);
        }

        private void ReadControl(ToggleButton tgl)
        {
            bool b = false;
            bool.TryParse(ReadString(tgl.Name), out b);
            tgl.IsChecked = b;
        }

        private void ReadControl(CheckBox chk)
        {
            bool b = false;
            bool.TryParse(ReadString(chk.Name), out b);
            chk.IsChecked = b;
        }

        private string ReadString(string name)
        {
            if (File.Exists(name + ".cfg"))
            {
                return File.ReadAllText(name + ".cfg");
            }
            return "";
        }

        private void SaveString(string name, object value)
        {
            File.WriteAllText(name + ".cfg", value.ToString());
        }

        JiraSprint _jiraSprint;

        DateTimeOffset _lastLive = DateTimeOffset.MinValue;
        private JiraSprint GetJiraSprint(BackgroundWorker bw, SprintKey sprintKey, LoadEnum load)
        {
            try
            {
                var project = sprintKey.Project;
                var sprint = sprintKey.Sprint;
                string str;
                DateTimeOffset dt;
                bool loadLive = false;
                if (load == LoadEnum.LiveAlways)
                    loadLive = true;
                else if (load == LoadEnum.LiveOnlyIfOld)
                {
                    var diff = DateTime.Now - _lastLive;
                    if (diff.TotalMinutes > 5)
                    {
                        loadLive = true;
                    }
                    else
                    {
                        loadLive = false;
                        load = LoadEnum.Latest;
                    }
                }
                if (loadLive)
                {

                    //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
                    str = HttpGet(JiraAccess.GetSprintUri(project, sprint));
                    dt = DateTimeOffset.Now;
                    //File.WriteAllLines("search.fancy.json", SplitLinesDeep(str));
                    JiraAccessFile.Write(new SprintKey(project, sprint), str);
                }
                else
                {
                    try
                    {
                        str = JiraAccessFile.Read(new SprintKey(project, sprint), load, out dt);
                    }
                    catch (IOException ioexc)
                    {
                        ReportProgress(bw, "GetJiraSprint(" + load + ")" + " Exception: " + ioexc.Message);
                        return new JiraSprint() { ErrorStatus = "Load: " + load + " Exception: " + ioexc.Message };
                    }
                }
                var jiraSprint = JiraSprint.Parse(JObject.Parse(str));
                jiraSprint.RetrieveTime = dt;
                jiraSprint.SetSprintName(sprintKey);

                if (load == LoadEnum.LiveAlways || load == LoadEnum.LiveOnlyIfOld || load == LoadEnum.Latest)
                    _lastLive = dt;

                return jiraSprint;
            }
            catch (Exception e)
            {
                ReportProgress(bw, "GetJiraSprint(" + load + ") Exception " + e.Message);
                return new JiraSprint() { ErrorStatus = "Load: " + load + " Exception: " + e.Message };
            }
        }

        private void ReportProgress(BackgroundWorker bw, string str)
        {
            if (bw == null)
                sta.Content = str;
            else
                bw.ReportProgress(0, str);
        }

        bool AutoRefresh
        {
            get
            {
                return chkAutoRefresh.IsChecked == true;
            }
        }

        bool BallChart
        {
            get
            {
                return tglChartType.IsChecked == true;
            }
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

        bool ShowTesting
        {
            get
            {
                return chkShowTesting.IsChecked == true;
            }
        }
        private void RefreshIssueList(bool filterChangeOnly)
        {
            bool logSpeed = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Issues.Clear();
            if (logSpeed)
                FileUtils.Log("Cleared", sw);
            if (_jiraSprint == null || _jiraSprint.Issues == null)
                return;

            if (_jiraSprint.Key == null)
                _jiraSprint.Key = new SprintKey(entSprint.Text);
            if (!filterChangeOnly)
            {
                _stats = JiraAccessFile.ReadStats(_jiraSprint);
                if (logSpeed)
                    FileUtils.Log("Stats", sw);
                UpdateOlds();
                if (logSpeed)
                    FileUtils.Log("Olds", sw);
                cmbCompare.Text = _jiraSprint.RetrieveTime.ToString(JiraSprint.DateFormatString) + " v. " + _jiraSprint.OldRetrieveTime.ToString(JiraSprint.DateFormatString);
                sta.Content = _jiraSprint.SprintStatus + " v. " + _jiraSprint.OldSprintStatus;
                if (logSpeed)
                    FileUtils.Log("sta.Text", sw);
                UpdateTotals();
            }

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lstIssues.ItemsSource);
            var hasFilter = !string.IsNullOrWhiteSpace(_filter);
            if (ShowSubtasks || hasFilter)
            {
                view.SortDescriptions.Clear();
            }
            else
            {
                Sort(_lastHeaderClicked, ListSortDirection.Ascending);
                //view.SortDescriptions.Add(new SortDescription(, ListSortDirection.Ascending));
            }
            if (logSpeed)
                FileUtils.Log("Sort", sw);
            foreach (var issue in _jiraSprint.Issues)   //.Where(i => i.IsSubtask == false))
            {
                try
                {
                    var showParent = ShowIssue(issue);
                    if (showParent)
                        Issues.Add(new JiraIssueViewModel(issue));
                    if (ShowSubtasks || hasFilter)
                    {
                        foreach (var subtask in issue.SubTasks)
                        {
                            if (showParent || ShowIssue(subtask))
                                Issues.Add(new JiraIssueViewModel(subtask));
                        }
                    }
                }
                catch (Exception exc)
                {
                    FileUtils.ErrorLog("Problem with issue " + issue, exc);
                }
            }
            if (logSpeed)
                FileUtils.Log("Done", sw);
        }

        SprintStats _stats;
        private void UpdateOlds()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var key = new SprintKey(entSprint.Text);
                cmbCompare.Items.Clear();
                var currentStat = _stats.Current;
                foreach (var stat in _stats.Stats.Where(s => s.Special == SprintStat.SpecialEnum.Significant).OrderByDescending(s => s.Timestamp))
                {
                    var item = new ComboBoxItem();
                    item.Content = currentStat.DateString() + " v. " + stat.DateString();
                    item.Tag = stat.Timestamp;
                    cmbCompare.Items.Add(item);
                }

                GraphIt();

            }
            catch (Exception exc)
            {
                //cmbCompare.Items.Clear();
                cmbCompare.Items.Add(exc);
            }
            if (cmbCompare.Items.Count == 0)
                cmbCompare.Items.Add("Nothing compares 2 U.");
            var left = sw.Elapsed;
        }

        private void UpdateTotals()
        {
            //var totalResolvedPoints = resolveds.Sum(i => i.StoryPoints);
            staTotals.Content = new SprintStat(_jiraSprint, SprintStat.SpecialEnum.Current).ToString();
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
            var issue = SelectedIssue;

            CopyToClipboard(issue);

        }

        private void CopyToClipboard(JiraIssueViewModel item)
        {
            DataObject dataObject = new DataObject();
            var str = "";
            var html = "";
            if (item.IsSubtask)
            {
                var parent = new JiraIssueViewModel(item.ParentIssue);
                str += parent.Description(true) + "sub  ";
                html += parent.HtmlDescription(true) + "sub  ";
            }
            str += item.Description(false);
            html += item.HtmlDescription(false);

            dataObject.SetText(str);
            dataObject.SetText(HtmlWrap(html), TextDataFormat.Html);
            Clipboard.SetDataObject(dataObject);
        }

        static private string HtmlWrap(string text)
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

        private void lstIssues_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            btnOpen_Click(null, null);
        }

        private void btnUnassign_Click(object sender, RoutedEventArgs e)
        {
            var issues = SelectedIssues;
            foreach (var issue in issues)
                HttpPut(JiraAccess.GetIssueUri(issue.Key), JiraAccess.GetComponentBody(""));
            NewStuff();
            //Refresh(issue.Key);
        }
        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            var issues = SelectedIssues;
            var rv = new EditDetails(issues);
            if (rv.ShowDialog() != true)
                return;
            if (rv.Assignee != null)
            {
                foreach (var issue in issues)
                    HttpPut(JiraAccess.GetIssueUri(issue.Key), JiraAccess.GetAssignBody(rv.Assignee));
            }
            if (rv.Component != null)
            {
                foreach (var issue in issues)
                    HttpPut(JiraAccess.GetIssueUri(issue.Key), JiraAccess.GetComponentBody(rv.Component));
            }
            NewStuff();
        }

        private void btnAddComponent_Click(object sender, RoutedEventArgs e)
        {
            var issues = SelectedIssues;
            string component;
            var rv = new EditDetails(issues);
            if (rv.ShowDialog() != true)
                return;
            component = rv.Component;
            foreach (var issue in issues)
                HttpPut(JiraAccess.GetIssueUri(issue.Key), JiraAccess.GetComponentBody(component));
            NewStuff();
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
            entFilter.Text = "";
            RefreshIssueList(true);
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var outs = new List<string>();
            outs.Add(JiraIssue.ToCsvHeader());
            foreach (var issue in Issues)
            {
                outs.Add(issue.ToCsv());
            }
            var file = Path.Combine(JiraAccessFile.Dir, "issues.csv");
            File.WriteAllLines(file, outs.ToArray());
            StartProcess(file);


            file = Path.Combine(JiraAccessFile.Dir, "sprints.csv");
            if (File.Exists(file))
                outs = File.ReadAllLines(Path.Combine(JiraAccessFile.Dir, "sprints.csv")).ToList();
            else
                outs = new List<string>();    

            foreach (var sprint in SprintParameters.Params)
            {
                if (outs.Any(o => o.Contains(sprint.Key)))
                    continue;

                outs.Add(sprint.Key + "," + sprint.Value.StartDate + "," + sprint.Value.EndDate + "," + sprint.Value.State);
            }
            File.WriteAllLines(file, outs.OrderBy(o => o).ToArray());


            StartProcess(file);
        }
        void StartProcess(string uri)
        {
            System.Diagnostics.Process.Start(uri);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _jiraSprint = GetIssues(null, entSprint.Text, LoadEnum.Latest, LoadEnum.Yesterday);
            RefreshIssueList(false);
            Update(LoadEnum.LiveOnlyIfOld); // kick off async refresh
        }

        private void Size_Changed(object sender, RoutedEventArgs e)
        {
            GraphIt();
        }

        private void GraphIt()
        {
            if (_stats == null)
                return;
            if (BallChart)
                new SprintBallGrapher(canvas, _stats.Current);
            else
                new SprintGrapher(canvas, _stats);
        }

        private void chkShow_Click(object sender, RoutedEventArgs e)
        {
            SaveControl(chkShowResolved);
            SaveControl(chkShowSubtasks);
            SaveControl(chkAutoRefresh);
            SaveControl(chkShowTesting);

            RefreshIssueList(true);
        }

        private void tglShow_Click(object sender, RoutedEventArgs e)
        {
            SaveControl(tglChartType);

            RefreshIssueList(false);
        }

        private void btnOpenOnBoard_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            StartProcess(issue.LinkOnBoard);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            StartProcess(issue.LinkDirect);
        }

        string _filter;
        private void entFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _filter = entFilter.Text;
            if (string.IsNullOrWhiteSpace(_filter))
                _jiraSprint.Issues.RemoveAll(i => string.IsNullOrWhiteSpace(i.Sprint));

            RefreshIssueList(true);

            FileUtils.Log("entFilter_TextChanged Complete", sw);
        }

        private void btnClean_Click(object sender, RoutedEventArgs e)
        {
            JiraAccessFile.CleanUp();
        }

        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(entFilter.Text))
                return;

            var issues = FindIssues(entFilter.Text);
            if (issues == null)
                return;
            foreach (var issue in issues)
                _jiraSprint.Issues.Add(issue);
            RefreshIssueList(true);
            sta.Content = "Searched for " + entFilter.Text;
        }

        private bool IsIssueKey(string str)
        {
            bool b;
            var subex = @"\w{2,6}-\d{1,5}";
            b = Regex.IsMatch("1112", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("123-1234", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("DEF-1234", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("123-", subex);         //    "[^0-9](2,6)-[0-9](1,5)");
            b = Regex.IsMatch("abc", subex);         //    "[^0-9](2,6)-[0-9](1,5)");

            var ex = subex;
            return Regex.IsMatch(str, ex);         //    "[^0-9](2,6)-[0-9](1,5)");
        }

        private void btnMail_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            CopyToClipboard(issue);
            StartProcess(issue.MailToLink());

        }
        private void btnText_Click(object sender, RoutedEventArgs e)
        {
            var phoneNumber = PromptPhoneNumber();
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return;
            SendSmsMessage(phoneNumber, SelectedIssue);
        }
        static bool _useRoadnetPhoneNumbers = false;
        static string _fromNumber = "+14433932877";
        static public string[] _senderPhoneNumbers;
        static int _fromNumberIndex;
        private static string GetSenderNumber()
        {
            if (_useRoadnetPhoneNumbers)
            {
                if (_senderPhoneNumbers == null)
                {
                    var file = @"d:\homebase\config\senderPhoneNumbers.txt";
                    if (File.Exists(file))
                    {
                        _senderPhoneNumbers = File.ReadAllLines(file);
                        FileUtils.Log("Sender Phone Numbers Loaded " + _senderPhoneNumbers.Count());
                    }
                    else
                    {
                        FileUtils.Log("Sender phone numbers not found at " + file);
                    }
                }
                if (_fromNumberIndex >= _senderPhoneNumbers.Count())
                    _fromNumberIndex = 0;
                return _senderPhoneNumbers[_fromNumberIndex++];
            }
            else
                return _fromNumber;
        }

        private static void SendSmsMessage(string phoneNumber, JiraIssue issue)
        {
            var sw = Stopwatch.StartNew();
            var client = GetTwilioClient();
            client.SendSmsMessage(GetSenderNumber(), phoneNumber, issue.Key + " " + issue.Summary + " " + issue.LinkDirect, StatusCallback);
            FileUtils.Log("Send", sw);
        }

        private static void StatusCallback(Twilio.SMSMessage msg)
        {
            FileUtils.Log("StatusCallback " + msg.Status, null);
        }
        private static string ToPhoneNumber 
        {
            get
            {
                return Properties.Settings.Default.ToPhoneNumber;
            }
        }
        private static Twilio.TwilioRestClient GetTwilioClient()
        {
            string sid;
            string token;
            if (_useRoadnetPhoneNumbers)
            {
                sid = "ACcdb62da98f2a776eb3f40094be93dcb2"; // mobiledev@roadnet.com 
                token = "9de81740e501f49d216ab058f84590b7"; // mobiledev@roadnet.com 
            }
            else
            {
                sid = "ACc956df05e21814b549cf86aecef061f0"; // dfrancis@roadnet.com 
                token = "9f1154b6bd7d37ee75926bc41a1c7014"; // dfrancis@roadnet.com 
            }

            var client = new Twilio.TwilioRestClient(sid, token);
            return client;
        }

        private void btnVoice_Click(object sender, RoutedEventArgs e)
        {
            var phoneNumber = PromptPhoneNumber();
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return;
            SendVoiceMessage(phoneNumber, SelectedIssue.ToString());
        }
        private static void SendVoiceMessage(string to, string body)
        {
            SendVoiceMessageUrl(to, "http://twimlets.com/message" + "?" + JiraIssue.ConvertToUrl("Message[0]=" + body));
            //SendVoiceMessageUrl(to, "http://twimlets.com/message" + "?Message%5B0%5D=" + @"http://849fairmount.com/apps/dev/Demo/dude.mp3&Message%5B1%5D=Thank+You+For+Calling");
        }

        private static void SendVoiceMessageUrl(string to, string url)
        {
            //Hello%20Brian%20I%20am%20your%20new%20robot%20overlord";
            // to the outbound call
            var options = new Twilio.CallOptions();

            // Set the call From, To, and URL values to use for the call.
            // This sample uses the sandbox number provided by
            // Twilio to make the call.
            options.From = GetSenderNumber();
            options.To = to;
            options.Url = url;

            // Make the call.
            var client = GetTwilioClient();
            var call = client.InitiateOutboundCall(options);
        }


        JiraIssueViewModel SelectedIssue
        {
            get
            {
                var rv = lstIssues.SelectedItem as JiraIssueViewModel;
                if (rv == null)
                    rv = lstIssues.Items[0] as JiraIssueViewModel;
                return rv;
            }
        }

        IList<JiraIssue> SelectedIssues
        {
            get
            {
                var rv = new List<JiraIssue>();
                foreach (var item in lstIssues.SelectedItems)
                    rv.Add(item as JiraIssue);
                return rv;
            }
        }
        private void btnAddSubtask_Click(object sender, RoutedEventArgs e)
        {
            var project = Project;
            var rv = new EditDetails();
            if (rv.ShowDialog() != true)
                return;
            var issues = SelectedIssues;
            foreach (var issue in issues.Where(i => !i.IsSubtask))
                HttpPost(JiraAccess.GetIssuesUri(), JiraAccess.GetNewSubtaskBody(project, issue, rv.Summary, rv.Estimate, rv.Assignee));

            NewStuff();
        }

        private void btnAddUsuals_Click(object sender, RoutedEventArgs e)
        {
            var issues = SelectedIssues;
            foreach (var issue in issues.Where(i => !i.IsSubtask))
            {
                HttpPost(JiraAccess.GetIssuesUri(), JiraAccess.GetNewSubtaskBody(Project, issue, "Implement", null, null));
                HttpPost(JiraAccess.GetIssuesUri(), JiraAccess.GetNewSubtaskBody(Project, issue, "Doc", null, "lstevens"));
            }

            NewStuff();
        }

        private void NewStuff()
        {
            if (AutoRefresh)
                Update(LoadEnum.LiveAlways);
            else
                btnUpdate.Foreground = Brushes.OrangeRed;
        }
        
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            //TestVoice();
            //SendVoiceMessage(rv.Response, "Greetings, friend. Do you wish to look as happy as me? Well, you've got the power inside you right now. So use it. And send one dollar to Happy Dude, 742 Evergreen Terrace, Springfield. Don't delay! Eternal happiness is just a dollar away.");
        }

        private static void TestVoice()
        {
            var phoneNumber = PromptPhoneNumber();
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return;
            SendVoiceMessageUrl(phoneNumber, "http://twimlets.com/message" + "?Message%5B0%5D=" + @"http://849fairmount.com/apps/dev/Demo/dude.mp3&Message%5B1%5D=Thank+You+For+Calling");
        }

        private static string PromptPhoneNumber()
        {
            var rv = new Prompt("Phone Number?", ToPhoneNumber);
            rv.ShowDialog();
            return rv.Response;
        }
        #region Header Sorting 
        // from http://msdn.microsoft.com/en-us/library/ms745786(v=vs.110).aspx
        // TODONE 11-23-2013 Added header sorting support
        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private void Header_Click(object sender, RoutedEventArgs e)
        {
            if (ShowSubtasks)
                return;
            GridViewColumnHeader headerClicked =
                  e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }
                    string header = headerClicked.Column.Header as string;
                    Sort(headerClicked, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header 
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }


                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }
        private void Sort(GridViewColumnHeader headerClicked, ListSortDirection direction)
        {
            string sortBy;
            if (headerClicked != null)
                sortBy = (headerClicked.Column.DisplayMemberBinding as System.Windows.Data.Binding).Path.Path;
            else
                sortBy = ReadString("Sort");

            ICollectionView dataView = CollectionViewSource.GetDefaultView(lstIssues.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
            SaveString("Sort", sortBy);
        }
        #endregion

        private void staStatus_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dlg = new PickList();
            dlg.ShowDialog();
        }

        private void cmb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 1)
                return;

            //var selected = e.AddedItems[0].ToString();
            //var index = selected.IndexOf(" v. ");
            //var dtStr = selected.Substring(index + " v. ".Length);
            var dt = (DateTimeOffset) (e.AddedItems[0] as ComboBoxItem).Tag;
            if (JiraAccessFile.OldCompare != dt)
            {
                JiraAccessFile.OldCompare = dt;
                Update(LoadEnum.LiveOnlyIfOld);
            }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            StartProcess(@"..\..\help.docx");
        }

        private void btnFindIssue_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            entFilter.Text = issue.Key;
        }

        private void btnPlanning_Click(object sender, RoutedEventArgs e)
        {
            StartProcess("https://roadnetmobiledev.atlassian.net/secure/RapidBoard.jspa?rapidView=3&view=planning");
        }

        private void btnWorking_Click(object sender, RoutedEventArgs e)
        {
            StartProcess("https://roadnetmobiledev.atlassian.net/secure/RapidBoard.jspa?rapidView=3&view=working");
        }

        private void entFilter_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnFind_Click(null, null);
            }
        }

        private void entSprint_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnUpdate_Click(null, null);
            }
        }

        private void sta_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            sta.ToolTip = sta.Content;
        }
    }
}
