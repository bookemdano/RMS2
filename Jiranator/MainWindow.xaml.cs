using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
            ControlHelper.Read(entSprint);
            ControlHelper.Read(chkAutoRefresh);
            ControlHelper.Read(chkShowOther);
            ControlHelper.Read(chkShowLabels);
            ControlHelper.Read(chkShowResolved);
            ControlHelper.Read(chkShowSubtasks);
            ControlHelper.Read(chkShowTesting);
            ControlHelper.Read(chkShowOnHold);
            ControlHelper.Read(cmbChartType);
            ControlHelper.Read(chkChartStoryPoints);
            ControlHelper.Read(chkChartTasks);
            UpdateShowByStatus();
            lstIssues.ItemsSource = Issues;
            var dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromHours(1);
            dt.Tick += Dt_Tick;
            dt.Start();

            Test();
        }

        private CheckBox[] GetShowByStatus()
        {
            var rv = new List<CheckBox>();
            foreach (var item in cmbShowByStatus.Items)
            {
                if (item is CheckBox)
                {
                    var chk = item as CheckBox;
                    if (!(chk.Content as string).StartsWith("-"))
                        rv.Add(item as CheckBox);
                }
            }
            return rv.ToArray();
        }

        private void btnShowAll_Click(object sender, RoutedEventArgs e)
        {
            var chks = GetShowByStatus();
            foreach (var chk in chks)
                chk.IsChecked = true;
            chkShow_Click(null, null);
        }

        private void chkShowNone_Click(object sender, RoutedEventArgs e)
        {
            var chks = GetShowByStatus();
            foreach (var chk in chks)
                chk.IsChecked = false;
            chkShow_Click(null, null);
        }

        private void UpdateShowByStatus()
        {
            var chks = GetShowByStatus();

            int nChecks = chks.Count();
            int nChecked = chks.Count(c => c.IsChecked == true);

            if (nChecked == 0)
                cmbShowByStatus.Text = "-None-";
            else if (nChecked == nChecks)
                cmbShowByStatus.Text = "All";
            else if (nChecked > 1)
                cmbShowByStatus.Text = "-legion-";
            else
                cmbShowByStatus.Text = chks.SingleOrDefault(c => c.IsChecked == true)?.Content as string;
        }

        private void Dt_Tick(object sender, EventArgs e)
        {
            Update(LoadEnum.LiveAlways);
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
        private static bool ShowIssue(JiraIssue issue, string filter, ShowStatusEnum showStatus)
        {
            var rv = true;
            if (!string.IsNullOrWhiteSpace(filter))
                rv = issue.Contains(filter);
            else if (showStatus.HasFlag(ShowStatusEnum.Resolved))
                rv = true;
            else if (issue.IsResolved)
                rv = false;
            else
                rv = true;

            if (rv && !showStatus.HasFlag(ShowStatusEnum.OnHold) && issue.IsOnHold)
                rv = false;
            if (rv && !showStatus.HasFlag(ShowStatusEnum.Testing) && (issue.IsTesting || issue.IsDoc))
                rv = false;
            if (rv && !showStatus.HasFlag(ShowStatusEnum.Other) && !(issue.IsTesting || issue.IsDoc || issue.IsResolved || issue.IsOnHold))
                rv = false;

            if (rv && !showStatus.HasFlag(ShowStatusEnum.Labels) && issue.Labels.Count() == 0)  // hide stuff without a label
                rv = false;

            return rv;
        }
        JiraIssue GetIssue(JiraSourceEnum source, string key)
        {
            var str = HttpAccess.HttpGet(JiraAccess.GetIssueJsonUri(source, key), true);
            if (str == null)
                return null;
            return JiraIssue.Parse(str);

        }

        List<JiraIssue> FindIssues(JiraSourceEnum source, string text)
        {
            string url;
            if (IsIssueKey(text))
                url = JiraAccess.FindIssueByKey(source, text);
            else
                url = JiraAccess.SearchIssuesUri(source, text);
            var str = HttpAccess.HttpGet(url, true);
            if (str == null)
                return null;
            try
            {
                JiraAccessFile.WriteResults(text + ".json", str);
            }
            catch (Exception)
            {
                // just try it
            }
            var jiraSet = JiraSet.Parse(str);

            return jiraSet.Issues;

        }
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            Update(LoadEnum.LiveAlways);
        }

        private void Update(LoadEnum liveLoad)
        {
            btnUpdate.IsEnabled = false;
            ControlHelper.Save(entSprint);
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
            _jiraSet = e.Result as JiraSet;
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
            IList<SprintKey> rv = null;
            try
            {
                if (sprintText.Contains(","))
                {
                    rv = sprintText.Split(",".ToCharArray()).Select(s => new SprintKey(s.Trim())).ToList();
                }
                else if (sprintText.Contains("*"))
                {
                    rv = new List<SprintKey>();
                    for (int i = 1; i <= 9; i++)
                        rv.Add(new SprintKey(sprintText.Replace("*", i.ToString())));
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
            }
            catch(Exception exc)
            {
                //sta.Content = "Sprint parse error " + exc.Message;
            }

            finally
            {
                if (rv == null)
                    rv = new List<SprintKey>() { new SprintKey(sprintText) };
            }

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

        private JiraSet GetIssues(BackgroundWorker bw, string sprintText, LoadEnum latest, LoadEnum old)
        {
            var parts = GetSprintList(sprintText);

            var jiraSetOld = new JiraSet();

            if (parts.Count() == 1)
            {
                var single = parts.First();
                jiraSetOld.Issues = new List<JiraIssue>();
                jiraSetOld.Merge(GetJiraSet(bw, single, old));
            }
            var jiraSet = new JiraSet();
            jiraSet.Issues = new List<JiraIssue>();
            foreach (var part in parts)
                jiraSet.Merge(GetJiraSet(bw, part, latest));

            if (parts.Count() == 1)
                jiraSet.UpdateOldStatus(jiraSetOld);
            else
                FileUtils.Log("Old not found");
            return jiraSet;
        }

        JiraSet _jiraSet;

        DateTimeOffset _lastLive = DateTimeOffset.MinValue;
        private JiraSet GetJiraSet(BackgroundWorker bw, SprintKey sprintKey, LoadEnum load)
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
                    dt = DateTimeOffset.Now;
                    str = JiraHttpAccess.GetSprintLive(project, sprint, false);
                }

                else
                {
                    try
                    {
                        str = JiraAccessFile.Read(new SprintKey(project, sprint), load, out dt);
                    }
                    catch (IOException ioexc)
                    {
                        ReportProgress(bw, "GetJiraSet(" + load + ")" + " Exception: " + ioexc.Message);
                        return new JiraSet() { ErrorStatus = "Load: " + load + " Exception: " + ioexc.Message };
                    }
                }
                var jiraSet = JiraSet.Parse(str);
                jiraSet.RetrieveTime = dt;
                jiraSet.SetSprintName(sprintKey);

                if (load == LoadEnum.LiveAlways || load == LoadEnum.LiveOnlyIfOld || load == LoadEnum.Latest)
                    _lastLive = dt;

                return jiraSet;
            }
            catch (Exception e)
            {
                ReportProgress(bw, "GetJiraSet(" + load + ") Exception " + e.Message);
                return new JiraSet() { ErrorStatus = "Load: " + load + " Exception: " + e.Message };
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
                return cmbChartType.Text == "Ball";
            }
        }

        bool ShowSubtasks
        {
            get
            {
                return chkShowSubtasks.IsChecked == true;
            }
        }
        [Flags]
        public enum ShowStatusEnum
        {
            None = 0,
            Other = 1,
            OnHold = 2,
            Testing = 4,
            Resolved = 8,
            Labels = 16
        }
        ShowStatusEnum ShowStatus
        {
            get
            {
                var rv = ShowStatusEnum.None;
                if (chkShowLabels.IsChecked == true)
                    rv |= ShowStatusEnum.Labels;
                if (chkShowOther.IsChecked == true)
                    rv |= ShowStatusEnum.Other;
                if (chkShowOnHold.IsChecked == true)
                    rv |= ShowStatusEnum.OnHold;
                if (chkShowTesting.IsChecked == true)
                    rv |= ShowStatusEnum.Testing;
                if (chkShowResolved.IsChecked == true)
                    rv |= ShowStatusEnum.Resolved;
                return rv;
            }
        }

        private void RefreshIssueList(bool filterChangeOnly)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (_jiraSet == null || _jiraSet.Issues == null)
            {
                Issues.Clear();
                return;
            }

            if (_jiraSet.Key == null)
                _jiraSet.Key = new SprintKey(entSprint.Text);

            if (!filterChangeOnly)
                UpdateStatsForOld();

            var issues = RefreshIssues(_jiraSet, _stats, _filter, ShowSubtasks, ShowStatus);
            if (!filterChangeOnly || IssuesChanged(issues))
            {
                Issues = issues;
                lstIssues.ItemsSource = Issues;
            }

            UpdateFilter();

            if (_logSubSpeeds)
                FileUtils.Log("RefreshIssueList", sw);
        }

        private bool IssuesChanged(ObservableCollection<JiraIssueViewModel> issues)
        {
            if (issues.Count() != Issues.Count())
                return true;
            for (int i = 0; i < issues.Count(); i++)
            {
                if (issues[i].Key != Issues[i].Key)
                    return true;
            }
            return false;
        }

        private void UpdateFilter()
        {
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lstIssues.ItemsSource);
            var hasFilter = !string.IsNullOrWhiteSpace(_filter);
            if (ShowSubtasks || hasFilter)
            {
                if (view.SortDescriptions.Any())
                    view.SortDescriptions.Clear();
            }
            else
            {
                Sort(_lastHeaderClicked, ListSortDirection.Ascending);
                //view.SortDescriptions.Add(new SortDescription(, ListSortDirection.Ascending));
            }
        }
        static readonly bool _logSubSpeeds = true;

        private void UpdateStatsForOld()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _stats = SprintStats.ReadStats(_jiraSet);
            if (_logSubSpeeds)
                FileUtils.Log("Stats", sw);
            UpdateOlds();
            if (_logSubSpeeds)
                FileUtils.Log("Olds", sw);
            cmbCompare.Text = _jiraSet.RetrieveTime.ToString(JiraSet.DateFormatString) + " v. " + _jiraSet.OldRetrieveTime.ToString(JiraSet.DateFormatString);
            sta.Content = _jiraSet.SprintStatus + " v. " + _jiraSet.OldSprintStatus;
            if (_logSubSpeeds)
                FileUtils.Log("sta.Text", sw);
            UpdateTotals();
        }

        private static ObservableCollection<JiraIssueViewModel> RefreshIssues(JiraSet jiraSet, SprintStats stats, string filter, bool showSubtasks, ShowStatusEnum showStatus)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var rv = new ObservableCollection<JiraIssueViewModel>();
            foreach (var issue in jiraSet.Issues)   //.Where(i => i.IsSubtask == false))
            {
                try
                {
                    var showParent = ShowIssue(issue, filter, showStatus);
                    if (showParent)
                        rv.Add(new JiraIssueViewModel(issue));
                    if (showSubtasks || !string.IsNullOrWhiteSpace(filter))
                    {
                        foreach (var subtask in issue.SubTasks)
                        {
                            if (showParent || ShowIssue(subtask, filter, showStatus))
                                rv.Add(new JiraIssueViewModel(subtask));
                        }
                    }
                }
                catch (Exception exc)
                {
                    FileUtils.ErrorLog("Problem with issue " + issue, exc);
                }
            }
            if (_logSubSpeeds)
                FileUtils.Log("Done", sw);

            return rv;
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
            staTotals.Content = new SprintStat(_jiraSet, SprintStat.SpecialEnum.Current).ToString();
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

            CopyToClipboard(issue, true);
        }

        private void btnCopyForP4_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;

            CopyToClipboard(issue, false);
        }

        private void CopyToClipboard(JiraIssueViewModel item, bool includeMeta)
        {
            DataObject dataObject = new DataObject();
            var html = item.HtmlDescription(true, true);

            dataObject.SetText(item.Description(true, true, includeMeta));
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

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            var issues = SelectedIssues;
            if (issues.Any(i => i.Source == JiraSourceEnum.Omnitracs))
                return; // can't edit new Jira stuff
            var rv = new EditDetails(issues);
            if (rv.ShowDialog() != true)
                return;
            if (rv.Assignee != null)
            {
                NewStuffPending();
                foreach (var issue in issues)
                    HttpAccess.HttpPut(JiraAccess.IssueUri(issue.Source, issue.Key), JiraAccess.GetAssignBody(rv.Assignee));
            }
            if (rv.Components != null)
            {
                NewStuffPending();
                foreach (var issue in issues)
                    HttpAccess.HttpPut(JiraAccess.IssueUri(issue.Source, issue.Key), JiraAccess.GetComponentBody(rv.Components));
            }
            if (rv.Version != null)
            {
                NewStuffPending();
                foreach (var issue in issues)
                    HttpAccess.HttpPut(JiraAccess.IssueUri(issue.Source, issue.Key), JiraAccess.GetFixVersionBody(rv.Version));
            }
            if (rv.Labels != null)
            {
                NewStuffPending();
                foreach (var issue in issues)
                    HttpAccess.HttpPut(JiraAccess.IssueUri(issue.Source, issue.Key), JiraAccess.GetLabelsBody(rv.Labels));
            }
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
            _jiraSet = GetIssues(null, entSprint.Text, LoadEnum.Latest, LoadEnum.Yesterday);
            RefreshIssueList(false);
            Update(LoadEnum.LiveOnlyIfOld); // kick off async refresh
        }

        private void Size_Changed(object sender, RoutedEventArgs e)
        {
            GraphIt();
        }

        private void GraphIt()
        {
            if (chkChartStoryPoints.IsChecked != true && chkChartTasks.IsChecked != true)
            {
                canvas.Visibility = Visibility.Hidden;
                Grid.SetRowSpan(lstIssues, 2);
                return;
            }
            else
            {
                canvas.Visibility = Visibility.Visible;
                Grid.SetRowSpan(lstIssues, 1);
            }

            if (_stats == null)
                return;
            if (BallChart)
                new SprintBallGrapher(canvas, _stats.Current);
            else
                new SprintGrapher(canvas, _stats, chkChartStoryPoints.IsChecked == true, chkChartTasks.IsChecked == true);
        }

        private void chkShow_Click(object sender, RoutedEventArgs e)
        {
            ControlHelper.Save(chkShowLabels);
            ControlHelper.Save(chkShowOther);
            ControlHelper.Save(chkShowResolved);
            ControlHelper.Save(chkShowSubtasks);
            ControlHelper.Save(chkAutoRefresh);
            ControlHelper.Save(chkShowTesting);
            ControlHelper.Save(chkShowOnHold);
            UpdateShowByStatus();
            RefreshIssueList(true);
        }

        private void chkChart_Click(object sender, RoutedEventArgs e)
        {
            ControlHelper.Save(chkChartStoryPoints);
            ControlHelper.Save(chkChartTasks);

            GraphIt();
        }

        private void cmbChartType_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (cmbChartType.SelectedItem == null)
                return;
            cmbChartType.Text = (cmbChartType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            ControlHelper.Save(cmbChartType);

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
                _jiraSet.Issues.RemoveAll(i => string.IsNullOrWhiteSpace(i.Sprint));

            RefreshIssueList(true);

            FileUtils.Log("entFilter_TextChanged Complete", sw);
        }

        private void btnClean_Click(object sender, RoutedEventArgs e)
        {
            var nDeleted = JiraAccessFile.CleanUp();
            MessageBox.Show(nDeleted.ToString() + " files deleted");
        }

        private void FindFromFilter(JiraSourceEnum source)
        {
            if (string.IsNullOrWhiteSpace(entFilter.Text))
                return;

            var issues = FindIssues(source, entFilter.Text);
            if (issues == null)
                return;
            AddIssues(issues);
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
            CopyToClipboard(issue, true);
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
            NewStuffPending();
            var issues = SelectedIssues;
            foreach (var issue in issues.Where(i => !i.IsSubtask))
                HttpAccess.HttpPost(JiraAccess.IssueUri(issue.Source), JiraAccess.GetNewSubtaskBody(project, issue, rv.Summary, rv.Estimate, rv.Assignee));

            NewStuff();
        }

        private void btnAddUsuals_Click(object sender, RoutedEventArgs e)
        {
            NewStuffPending();
            var issues = SelectedIssues;
            foreach (var issue in issues.Where(i => !i.IsSubtask))
            {
                HttpAccess.HttpPost(JiraAccess.IssueUri(issue.Source), JiraAccess.GetNewSubtaskBody(Project, issue, "Implement", null, null));
                HttpAccess.HttpPost(JiraAccess.IssueUri(issue.Source), JiraAccess.GetNewSubtaskBody(Project, issue, "Doc", null, "lstevens"));
            }

            NewStuff();
        }

        private void btnAddFromOmni_Click(object sender, RoutedEventArgs e)
        {
            var issues = SelectedIssues;
            foreach (var omniIssue in issues.Where(i => !i.IsSubtask))
            {
                var str = HttpAccess.HttpPost(JiraAccess.IssueUri(JiraSourceEnum.SDLC), JiraAccess.GetNewTaskBody(Project, omniIssue));
                JiraAccessFile.WriteResults("new.json", str);
                var json = JObject.Parse(str);
                var self = (string)json["self"];
                HttpAccess.HttpPost(self + @"/remotelink", JiraAccess.GetNewLinkBody(omniIssue));
            }
            NewStuff();
        }

        private void NewStuff()
        {
            NewStuffPending();
            if (AutoRefresh)
                Update(LoadEnum.LiveAlways);
        }
        private void NewStuffPending()
        {
            if (AutoRefresh)
                btnUpdate.IsEnabled = false;
            else
                btnUpdate.Foreground = Brushes.OrangeRed;
        }

        private void btnShowOmni_Click(object sender, RoutedEventArgs e)
        {
            var issues = new List<JiraIssue>();
            foreach (var issue in Issues.Where(i => i.Summary.StartsWith("RA-") || i.Summary.StartsWith("RTS-")))
            {
                var summary = issue.Summary;
                var parts = summary.Split(" -".ToCharArray());
                var key = parts[0] + "-" + parts[1];
                if (IsIssueKey(key))
                {
                    issues.AddRange(FindIssues(JiraSourceEnum.Omnitracs, key));
                }
            }
            if (issues.Count() == 0)
                return;

            AddIssues(issues);

            RefreshIssueList(true);
            sta.Content = "Added " + issues.Count() + " from new Jira";
        }
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            //var dlg = new PickList();
            //dlg.ShowDialog();

            //btnText_Click(sender, e);
            TestVoice();
            //SendVoiceMessage(rv.Response, "Greetings, friend. Do you wish to look as happy as me? Well, you've got the power inside you right now. So use it. And send one dollar to Happy Dude, 742 Evergreen Terrace, Springfield. Don't delay! Eternal happiness is just a dollar away.");
        }


        private void AddIssues(IEnumerable<JiraIssue> issues)
        {
            foreach (var issue in issues)
                AddIssue(issue);
        }
        private void AddIssue(JiraIssue issue)
        {
            if (!_jiraSet.Issues.Any(i => i.Key == issue.Key))
                _jiraSet.Issues.Add(issue);
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
                sortBy = ControlHelper.ReadString("Sort");

            ICollectionView dataView = CollectionViewSource.GetDefaultView(lstIssues.ItemsSource);

            if (dataView.SortDescriptions.Count() == 1)
            {
                if (dataView.SortDescriptions[0].PropertyName == sortBy && dataView.SortDescriptions[0].Direction == direction)
                {
                    dataView.Refresh();
                    return;
                }
            }

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
            ControlHelper.SaveString("Sort", sortBy);
        }
        #endregion

        private void cmbCompare_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 1)
                return;

            //var selected = e.AddedItems[0].ToString();
            //var index = selected.IndexOf(" v. ");
            //var dtStr = selected.Substring(index + " v. ".Length);
            var dt = (DateTimeOffset)(e.AddedItems[0] as ComboBoxItem).Tag;
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
                FindFromFilter(JiraSourceEnum.SDLC);
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

        private void btnFindOld_Click(object sender, RoutedEventArgs e)
        {
            FindFromFilter(JiraSourceEnum.SDLC);
        }

        private void btnFindNew_Click(object sender, RoutedEventArgs e)
        {
            FindFromFilter(JiraSourceEnum.Omnitracs);
        }

        private void lstIssues_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            lstIssues.ToolTip = DateTimeOffset.Now;
        }
    }
    public static class ControlHelper
    {

        internal static void Save(TextBox ent)
        {
            SaveString(ent.Name, ent.Text);
        }

        internal static void Save(ToggleButton tgl)
        {
            SaveString(tgl.Name, tgl.IsChecked);
        }

        internal static void Save(ComboBox cmb)
        {
            SaveString(cmb.Name, cmb.Text);
        }

        internal static void Save(CheckBox chk)
        {
            SaveString(chk.Name, chk.IsChecked);
        }

        internal static void Read(TextBox ent)
        {
            ent.Text = ReadString(ent.Name);
        }

        internal static void Read(ComboBox cmb)
        {
            cmb.Text = ReadString(cmb.Name);
        }

        internal static void Read(ToggleButton tgl)
        {
            bool b = false;
            bool.TryParse(ReadString(tgl.Name), out b);
            tgl.IsChecked = b;
        }

        internal static void Read(CheckBox chk)
        {
            bool b = false;
            bool.TryParse(ReadString(chk.Name), out b);
            chk.IsChecked = b;
        }

        internal static string ReadString(string name)
        {
            if (File.Exists(name + ".cfg"))
            {
                return File.ReadAllText(name + ".cfg");
            }
            return "";
        }

        internal static void SaveString(string name, object value)
        {
            File.WriteAllText(name + ".cfg", value.ToString());
        }

    }
}
