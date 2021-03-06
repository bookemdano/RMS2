﻿using JiraOne;
using JiraShare;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Jiragile
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                _toolTgls = new List<ToggleButton>() { tglSprint, tglSearch, tglSort, tglFilter, tglBugs };
                _sortTgls = new List<ToggleButton>() { tglSortByStatus, tglSortByVersion, tglSortByAssignee };
                FileUtils.GetSetting(tglShowOnHold);
                FileUtils.GetSetting(tglShowOther);
                FileUtils.GetSetting(tglShowResolved);
                FileUtils.GetSetting(tglShowTesting);
                FileUtils.GetSetting(tglShowSubtasks);

                foreach (var tgl in _sortTgls)
                    FileUtils.GetSetting(tgl);

                foreach (var tgl in _toolTgls)
                {
                    FileUtils.GetSetting(tgl);
                    if (tgl.IsChecked == true)
                    {
                        SelectTool(tgl);
                        break;
                    }
                }
                FileUtils.GetSetting(tglList);
                FileUtils.GetSetting(tglChart);
                FileUtils.GetSetting(tglDetail);

                FileUtils.GetSetting(entSprint, "AP 2015.R4.S5.Mobile");

                cmbTeam.Items.Add("");
                cmbTeam.Items.Add("Mobile");
                cmbTeam.Items.Add("RA - SWAT");
                cmbTeam.Items.Add("RTS - Feature");
                cmbTeam.Items.Add("RA - Feature");
                cmbTeam.Items.Add("RA - Insight");
                cmbTeam.Items.Add("RA - Telematics");
                cmbTeam.Items.Add("Algo");
                cmbTeam.Items.Add("GIS");
                cmbTeam.Items.Add("Localization");
                cmbTeam.Items.Add("Mobile - EP");
                cmbTeam.Items.Add("Navigation");
                cmbTeam.Items.Add("Architecture");
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += Timer_Tick;
                timer.Start();
            }
            catch (Exception exc)
            {
                staStatus.Text = exc.Message;
            }
            base.OnNavigatedTo(e);
        }

        private async void Timer_Tick(object sender, object e)
        {
            var timer = sender as DispatcherTimer;
            await Refresh(LoadEnum.LiveOnlyIfOld);
            if (timer.Interval > TimeSpan.FromMinutes(1))
                await JiraFileAccess.CleanUp();
            timer.Interval = TimeSpan.FromHours(1);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await Refresh(LoadEnum.Latest);
            _loaded = true; // checked by resize
        }

        JiraSet _jiraSet;
        bool _loaded = false;
        async Task Refresh(LoadEnum loadEnum)
        {
            try
            {
                if (loadEnum != LoadEnum.Latest || _jiraSet == null)
                {
                    staStatus.Text = "Refreshing";
                    if (false == await GetJiraSet(loadEnum))
                        return;
                }
                UpdateCounts(_jiraSet);
                var issues = CreateIssueVms(_jiraSet, entSearch.Text, ShowSubtasks, ShowStatus);
                lstIssues.Items.Clear();
                IOrderedEnumerable<JiraIssueViewModel> ordered;
                var sort = GetSortOrder();
                if (sort == SortEnum.Status)
                    ordered = issues.OrderBy(i => i.CalcedStatus);
                else if (sort == SortEnum.Version)
                    ordered = issues.OrderBy(i => i.FixVersionsString);
                else
                    ordered = issues.OrderBy(i => i.Assignee);

                var totalStoryPoints = 0;
                foreach (var issue in ordered)
                {
                    totalStoryPoints += issue.StoryPoints;
                    lstIssues.Items.Add(new IssueControl(issue));
                }
                staStatus.Text = "Updated " + _jiraSet.RetrieveTime.RelativeTime();
                staCounts.Text = ordered.Count() + " stories " + totalStoryPoints + " story points";
                if (tglList.IsChecked == true)
                {
                    grdList.Visibility = UIUtils.IsVisible(true);
                    rowList.Height = new GridLength(1, GridUnitType.Star);
                }
                else
                {
                    grdList.Visibility = UIUtils.IsVisible(false);
                    rowList.Height = new GridLength(0);
                }
                if (tglChart.IsChecked == true)
                {
                    canvas.Visibility = UIUtils.IsVisible(true);
                    rowChart.Height = new GridLength(1, GridUnitType.Star);
                    var stats = SprintStats.ReadStats(_jiraSet);
                    var grapher = new SprintGrapher(canvas, stats, false, true);
                }
                else
                {
                    canvas.Visibility = UIUtils.IsVisible(false);
                    rowChart.Height = new GridLength(0);
                }
            }
            catch (Exception exc)
            {
                staStatus.Text = exc.Message;
            }

        }

        private async Task<bool> GetJiraSet(LoadEnum loadEnum)
        {
            try
            {
                var dt = DateTimeOffset.MinValue;
                var sprintKey = new SprintKey(entSprint.Text);
                if (string.IsNullOrWhiteSpace(sprintKey.Project))
                {
                    await new MessageDialog("Bad Sprint Text Format- should be like 'RA 2016.R2.S3.Mobile' not " + entSprint.Text).ShowAsync();
                    entSprint.Text = "RA 2016.R2.S3.Mobile";
                    return false;
                }

                var str = "";
                if (loadEnum == LoadEnum.LiveAlways || loadEnum == LoadEnum.LiveOnlyIfOld)
                {
                    if (loadEnum == LoadEnum.LiveOnlyIfOld)
                    {
                        if (_jiraSet != null && _jiraSet.RetrieveTime.Age().TotalMinutes < 60)  // 5 minutes
                        {
                            staStatus.Text = "Not Updated " + _jiraSet.RetrieveTime.RelativeTime();
                            return false;
                        }
                    }
                    str = await JiraHttpAccess.GetSprintLiveAsync(sprintKey.Project, sprintKey.Sprint, true);
                    if (str.StartsWith("ERROR"))
                    {
                        staStatus.Text = str;
                        return false;
                    }
                    dt = DateTimeOffset.Now;
                }
                else
                {
                    var tpl = await JiraFileAccess.Read(sprintKey, loadEnum);
                    if (tpl != null)
                    {
                        str = tpl.Item1;
                        dt = tpl.Item2;
                    }
                }
                if (string.IsNullOrWhiteSpace(str))
                    return false;
                _jiraSet = JiraSet.Parse(str);
                _jiraSet.RetrieveTime = dt;
                _jiraSet.SetSprintName(sprintKey);
                return true;
            }
            catch (Exception exc)
            {
                staStatus.Text = exc.Message;
                return false;
            }
        }

        private void UpdateCounts(JiraSet _jiraSet)
        {
            int resolved = 0;
            int testing = 0;
            int blocked = 0;
            int other = 0;
            foreach (var issue in _jiraSet.Issues)
            {
                if (issue.IsResolved)
                    resolved++;
                else if(issue.IsTesting)
                    testing++;
                else if(issue.IsOnHold)
                    blocked++;
                else
                    other++;
            }
            tglShowResolved.Content = "Resolved-" + resolved;
            tglShowTesting.Content = "Testing-" + testing;
            tglShowOnHold.Content = "Blocked-" + blocked;
            tglShowOther.Content = "Other-" + other;
        }

        private async void btnGo_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(entSprint);
            entSearch.Text = "";
            await Refresh(LoadEnum.LiveAlways);
        }

        private static ObservableCollection<JiraIssueViewModel> CreateIssueVms(JiraSet jiraSet, string filter, bool showSubtasks, ShowStatusEnum showStatus)
        {
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

            return rv;
        }
        ShowStatusEnum ShowStatus
        {
            get
            {
                var rv = ShowStatusEnum.None;
                //if (chkShowLabels.IsChecked == false)
                    rv |= ShowStatusEnum.Labels;
                if (tglShowOther.IsChecked == true)
                    rv |= ShowStatusEnum.Other;
                if (tglShowOnHold.IsChecked == true)
                    rv |= ShowStatusEnum.OnHold;
                if (tglShowTesting.IsChecked == true)
                    rv |= ShowStatusEnum.Testing;
                if (tglShowResolved.IsChecked == true)
                    rv |= ShowStatusEnum.Resolved;
                return rv;
            }
        }

        private static bool ShowIssue(JiraIssue issue, string filter, ShowStatusEnum showStatus)
        {
            var rv = true;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                rv = issue.Contains(filter);
                if (rv)
                    return true; // if something was searched show it regardless of filter
            }
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

        private async void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_loaded)   // this is a hack, forgive me
                await Refresh(LoadEnum.Latest);
        }
        private enum ToolEnum
        {
            None,
            Sprint,
            Filter,
            Sort,
            Search,
            Bugs
        }
        private enum SortEnum
        {
            None,
            Status,
            Version,
            Assignee
        }
        void ExclusiveCheck(List<ToggleButton> buttons, ToggleButton selected)
        {
            foreach (var button in buttons)
            {
                if (button == selected)
                    button.IsChecked = true;
                else
                    button.IsChecked = false;
                FileUtils.SaveSetting(button);
            }
            
        }
        List<ToggleButton> _toolTgls;
        List<ToggleButton> _sortTgls;
        private void tglTools_Click(object sender, RoutedEventArgs e)
        {
            SelectTool(sender as ToggleButton);
        }

        private void SelectTool(ToggleButton tgl)
        {
            var tools = ToolEnum.None;
            if (tgl == tglSprint)
                tools = ToolEnum.Sprint;
            else if (tgl == tglSearch)
                tools = ToolEnum.Search;
            else if (tgl == tglSort)
                tools = ToolEnum.Sort;
            else if (tgl == tglBugs)
                tools = ToolEnum.Bugs;
            else
                tools = ToolEnum.Filter;

            ExclusiveCheck(_toolTgls, tgl);
            pnlSprint.Visibility = UIUtils.IsVisible(tools == ToolEnum.Sprint);
            pnlFilter.Visibility = UIUtils.IsVisible(tools == ToolEnum.Filter);
            pnlSort.Visibility = UIUtils.IsVisible(tools == ToolEnum.Sort);
            pnlSearch.Visibility = UIUtils.IsVisible(tools == ToolEnum.Search);
            pnlBugs.Visibility = UIUtils.IsVisible(tools == ToolEnum.Bugs);
        }

        private async void tglChart_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(sender as AppBarToggleButton);
            await Refresh(LoadEnum.Latest);
        }

        private void btnSearchOld_Click(object sender, RoutedEventArgs e)
        {
            FindFromFilter(JiraSourceEnum.SDLC);
        }

        private void btnSearchNew_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(entSearch);
            FindFromFilter(JiraSourceEnum.Omnitracs);
        }

        private async void FindFromFilter(JiraSourceEnum source)
        {
            if (string.IsNullOrWhiteSpace(entSearch.Text))
                return;
            try
            {
                var issues = await FindIssues(source, entSearch.Text);
                if (issues == null)
                    return;
                AddIssues(issues);
                await Refresh(LoadEnum.Latest);
                staStatus.Text = "Searched for " + entSearch.Text;
            }
            catch (Exception exc)
            {
                staStatus.Text = exc.Message;
            }
        }

        private void AddIssues(IEnumerable<JiraIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (!_jiraSet.Issues.Any(i => i.Key == issue.Key))
                    _jiraSet.Issues.Add(issue);
            }
        }
        
        async Task<List<JiraIssue>> FindIssues(JiraSourceEnum source, string text)
        {

            string url;
            if (JiraIssue.IsIssueKey(text))
                url = JiraAccess.FindIssueByKey(source, text);
            else
                url = JiraAccess.SearchIssuesUri(source, text + "*");
            var str = await HttpAccess.HttpGetAsync(url, true);
            if (str == null)
                return null;
            try
            {
                var jobject = JObject.Parse(str);
                await JiraFileAccess.WriteResults(text + ".json", JsonConvert.SerializeObject(jobject, Formatting.Indented));
            }
            catch (Exception)
            {
                // just try it
            }
            var jiraSet = JiraSet.Parse(str);

            return jiraSet.Issues;
        }

        private void btnSearchClear_Click(object sender, RoutedEventArgs e)
        {
            entSearch.Text = "";
        }

        private async void entSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            await Refresh(LoadEnum.Latest);
        }

        JiraIssueViewModel SelectedIssue
        {
            get
            {
                var issueControl = lstIssues.SelectedItem as IssueControl;
                return issueControl?.Tag as JiraIssueViewModel;
            }
        }

        private string Project
        {
            get
            {
                var parts = entSprint.Text.Split(" -".ToCharArray());
                return parts[0];
            }
        }

        public bool ShowSubtasks
        {
            get
            {
                return tglShowSubtasks.IsChecked == true;
            }
        }

        private async void btnCopyFromOmni_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            if (issue == null)
                return;
            var newIssues = new List<JiraIssue>();
            var issues = new List<JiraIssueViewModel>() { issue };
            foreach (var omniIssue in issues.Where(i => !i.IsSubtask))
            {
                var str = await HttpAccess.HttpPostAsync(JiraAccess.IssueUri(JiraSourceEnum.Omnitracs), JiraAccess.GetBodyCopiedBug(Project, omniIssue));
                await JiraFileAccess.WriteResults("new.json", str);
                var json = JObject.Parse(str);
                var self = (string)json["self"];
                await HttpAccess.HttpPostAsync(self + @"/remotelink", JiraAccess.GetBodyNewLink(omniIssue));
                newIssues.AddRange(await FindIssues(JiraSourceEnum.SDLC, (string)json["key"]));
            }
            AddIssues(newIssues);

            await Refresh(LoadEnum.Latest);
        }

        private void lstIssues_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cmdActions.Visibility = UIUtils.IsVisible(e.AddedItems.Count() > 0);

            ShowSelectedDetails();
        }

        private void ShowSelectedDetails()
        {
            var item = SelectedIssue;
            if (item != null && tglDetail.IsChecked == true)
            {
                colDetail.Width = new GridLength(.25, GridUnitType.Star);
                pnlDetails.Children.Clear();
                var props = typeof(JiraIssue).GetProperties();
                foreach (var prop in props)
                {
                    var str = prop.GetValue(item, null)?.ToString();
                    if (string.IsNullOrWhiteSpace(str) || str.StartsWith("System.") || str.StartsWith("Windows."))
                        continue;
                    AddDetail(prop.Name, str);
                }
            }
            else
                colDetail.Width = new GridLength(0);
        }

        enum DetailDisplayEnum
        {
            None,
            Label
        }

        private void AddDetail(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            AddDetail(label + ":", DetailDisplayEnum.Label);
            AddDetail(value, DetailDisplayEnum.None);
        }
        private void AddDetail(string str, DetailDisplayEnum detailDisplay)
        {
            if (string.IsNullOrWhiteSpace(str))
                return;
            var sta = new TextBlock();
            sta.Text = str;
            if (detailDisplay == DetailDisplayEnum.Label)
                sta.FontWeight = FontWeights.Bold;
            sta.TextWrapping = TextWrapping.Wrap;
            pnlDetails.Children.Add(sta);
        }
        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            var item = SelectedIssue;
            CopyToClipboard(item, true);
        }

        private static void CopyToClipboard(JiraIssueViewModel item, bool includeMeta)
        {
            var dataPackage = new DataPackage();
            var html = item.HtmlDescription(true, true);

            dataPackage.SetText(item.Description(true, true, includeMeta));
            dataPackage.SetHtmlFormat(HtmlWrap(html));

            Clipboard.SetContent(dataPackage);
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

        private async void btnMail_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            CopyToClipboard(issue, true);
            await Launcher.LaunchUriAsync(new Uri(issue.MailToLink()));
        }
        private async void tglShow_Click(object sender, RoutedEventArgs e)
        {
            var tgl = (sender as ToggleButton);
            FileUtils.SaveSetting(tgl);
            if (tgl == tglDetail)
                ShowSelectedDetails();
            else
                await Refresh(LoadEnum.Latest);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            SelectedIssue?.BrowseTo();
        }

        private async void tglSort_Click(object sender, RoutedEventArgs e)
        {
            ExclusiveCheck(_sortTgls, sender as ToggleButton);
            await Refresh(LoadEnum.Latest);
        }
        private SortEnum GetSortOrder()
        {
            var sort = SortEnum.None;
            if (tglSortByStatus.IsChecked == true)
                sort = SortEnum.Status;
            else if (tglSortByVersion.IsChecked == true)
                sort = SortEnum.Version;
            else if (tglSortByAssignee.IsChecked == true)
                sort = SortEnum.Assignee;
            return sort;
        }

        private async void btnPreviousSprint_Click(object sender, RoutedEventArgs e)
        {
            var sprint = new SprintClass(entSprint.Text);
            sprint.Sprint--;
            if (sprint.Sprint < 1)
            {
                sprint.Sprint = 6;
                sprint.Release--;
            }
            if (sprint.Release < 1)
            {
                sprint.Release = 4;
                sprint.Year--;
            }
            entSprint.Text = sprint.ToString();
            FileUtils.SaveSetting(entSprint);
            entSearch.Text = "";
            await Refresh(LoadEnum.LiveAlways);
        }

        private async void btnNextSprint_Click(object sender, RoutedEventArgs e)
        {
            var sprint = new SprintClass(entSprint.Text);
            sprint.Sprint++;
            if(sprint.Sprint > 6)
            {
                sprint.Sprint = 1;
                sprint.Release++;
            }
            if (sprint.Release > 4)
            {
                sprint.Release = 1;
                sprint.Year++;
            }
            entSprint.Text = sprint.ToString();
            FileUtils.SaveSetting(entSprint);
            entSearch.Text = "";
            await Refresh(LoadEnum.LiveAlways);
        }

        private async void btnSplit_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            if (issue == null)
                return;

            var newIssues = new List<JiraIssue>();
            var parts = new List<string>() { "IMX", "AMX", "WMX", "Server" };
            foreach(var part in parts)
            {
                var dlg = new MessageDialog("Create part for " + part + "?");
                dlg.Commands.Add(new UICommand { Label = "Ok", Id = 0 });
                dlg.Commands.Add(new UICommand { Label = "Cancel", Id = 1 });
                var cmd = await dlg.ShowAsync();
                if (cmd.Label != "Ok")
                    continue;
                var body = JiraAccess.GetBodySplitStory(issue, part);
                var str = await HttpAccess.HttpPostAsync(JiraAccess.IssueUri(JiraSourceEnum.SDLC), body);

                await JiraFileAccess.WriteResults("new.json", str);
                var json = JObject.Parse(str);
                var self = (string)json["self"];
                await HttpAccess.HttpPostAsync(self + @"/remotelink", JiraAccess.GetBodyNewLink(issue));
                newIssues.AddRange(await FindIssues(JiraSourceEnum.SDLC, (string)json["key"]));
            }

            AddIssues(newIssues);

            await Refresh(LoadEnum.Latest);

        }

        private async void btnSetTeam_Click(object sender, RoutedEventArgs e)
        {
            var issues = _jiraSet.Issues;
            foreach(var issue in issues)
            {
                try
                {
                    if (issue.Team != JiraAccess.Team)
                        await HttpAccess.HttpPutAsync(
                            JiraAccess.IssueUri(issue.Source, issue.Key),
                            JiraAccess.GetBodyTeam(JiraAccess.Team));
                }
                catch (Exception exc)
                {
                    FileUtils.ErrorLog("Problem setting team on " + issue, exc);
                }
            }
            await Refresh(LoadEnum.Latest);
        }

        private async void btnBugs_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button).IsEnabled = false;
            //UploadToSharepoint();
            staStatus.Text = "Generating Bug Reports";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await BugReport.Generate(cmbTeam.SelectedValue as string);
            staStatus.Text = "Done " + sw.Elapsed.TotalSeconds.ToString("0") + "s";
            (sender as Button).IsEnabled = true;
        }

        private async void btnLocalCache_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(Windows.Storage.ApplicationData.Current.LocalFolder);
        }
    }
    public class SprintClass
    {
        public SprintClass(string text)
        {
            var parts = text.Split(" .".ToCharArray());
            var sprint = 0;
            int.TryParse(parts[3][1].ToString(), out sprint);
            var release = 0;
            int.TryParse(parts[2][1].ToString(), out release);
            var year = 0;
            int.TryParse(parts[1], out year);
            Project = parts[0];
            Sprint = sprint;
            Release = release;
            Year = year;
            Team = parts[4];
        }
        public string Project { get; set; }
        public int Year { get; set; }
        public int Release { get; set; }
        public int Sprint { get; set; }
        public string Team { get; set; }
        public override string ToString()
        {
            return Project + " " + Year + ".R" + Release + ".S" + Sprint + "." + Team;
        }
    }
}
