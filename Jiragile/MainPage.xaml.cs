using JiraOne;
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
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

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
            
            FileUtils.GetSetting(tglShowOnHold, true);
            FileUtils.GetSetting(tglShowOther, true);
            FileUtils.GetSetting(tglShowResolved, true);
            FileUtils.GetSetting(tglShowTesting, true);

            FileUtils.GetSetting(bartglSprint, false);
            FileUtils.GetSetting(bartglFilter, true);
            FileUtils.GetSetting(bartglSearch, false);
            bartglTools_Click(null, null);

            FileUtils.GetSetting(tglChart, false);
            FileUtils.GetSetting(tglDetail, true);

            FileUtils.GetSetting(entSprint, "AP 2015.R4.S5.Mobile");

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async void Timer_Tick(object sender, object e)
        {
            var timer = sender as DispatcherTimer;
            Refresh(LoadEnum.LiveOnlyIfOld);
            if (timer.Interval > TimeSpan.FromMinutes(1))
                await JiraFileAccess.CleanUp();
            timer.Interval = TimeSpan.FromHours(1);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh(LoadEnum.Latest);
        }

        JiraSet _jiraSet;
        async void Refresh(LoadEnum loadEnum)
        {
            if (loadEnum != LoadEnum.Latest || _jiraSet == null)
            {
                staStatus.Text = "Refreshing";

                var dt = DateTimeOffset.MinValue;
                var sprintKey = new SprintKey(entSprint.Text);
                var str = "";
                if (loadEnum == LoadEnum.LiveAlways || loadEnum == LoadEnum.LiveOnlyIfOld)
                {
                    if (loadEnum == LoadEnum.LiveOnlyIfOld)
                    {
                        if (_jiraSet != null && _jiraSet.RetrieveTime.Age().TotalMinutes < 5)
                        {
                            staStatus.Text = "Not Updated " + _jiraSet.RetrieveTime.RelativeTime();
                            return;
                        }
                    }
                    str = await JiraHttpAccess.GetSprintLiveAsync(sprintKey.Project, sprintKey.Sprint, true);
                    if (str.StartsWith("ERROR"))
                    {
                        staStatus.Text = str;
                        return;
                    }
                    dt = DateTimeOffset.Now;
                }
                else
                {
                    var tpl = await JiraFileAccess.Read(sprintKey, loadEnum);
                    str = tpl.Item1;
                    dt = tpl.Item2;
                }
                if (string.IsNullOrWhiteSpace(str))
                    return;
                _jiraSet = JiraSet.Parse(str);
                _jiraSet.RetrieveTime = dt;
                _jiraSet.SetSprintName(sprintKey);
            }

            var issues = CreateIssueVms(_jiraSet, entSearch.Text, false, ShowStatus);
            lstIssues.Items.Clear();
            foreach (var issue in issues.OrderBy(i => i.CalcedStatus))
                lstIssues.Items.Add(new IssueControl(issue));
            staStatus.Text = "Updated " + _jiraSet.RetrieveTime.RelativeTime();
            if (tglChart.IsChecked == true)
            {
                canvas.Visibility = UIUtils.IsVisible(true);
                rowChart.Height = new GridLength(.5, GridUnitType.Star);
                var stats = await SprintStats.ReadStats(_jiraSet);
                var grapher = new SprintGrapher(canvas, stats, false, true);
            }
            else
            {
                canvas.Visibility = UIUtils.IsVisible(false);
                rowChart.Height = new GridLength(0);
            }
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(entSprint);
            Refresh(LoadEnum.LiveAlways);
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

        private void chkShow_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(sender as ToggleButton);

            Refresh(LoadEnum.Latest);
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Refresh(LoadEnum.Latest);
        }
        private enum Tools
        {
            None,
            Sprint,
            Filter,
            Search
        }
        private void bartglTools_Click(object sender, RoutedEventArgs e)
        {
            var bartgl = sender as AppBarToggleButton;
            if (bartgl == null) // to handle initial case
            {
                if (bartglSprint.IsChecked == true)
                    bartgl = bartglSprint;
                else if (bartglSearch.IsChecked == true)
                    bartgl = bartglSearch;
                else 
                    bartgl = bartglFilter;
            }
            var tools = Tools.None;
            if (bartgl.IsChecked == true)
            {
                if (bartgl == bartglSprint)
                    tools = Tools.Sprint;
                else if (bartgl == bartglSearch)
                    tools = Tools.Search;
                else
                    tools = Tools.Filter;
            }

            pnlSprint.Visibility = UIUtils.IsVisible(tools == Tools.Sprint);
            pnlFilter.Visibility = UIUtils.IsVisible(tools == Tools.Filter);
            pnlSearch.Visibility = UIUtils.IsVisible(tools == Tools.Search);
            bartglSprint.IsChecked = (tools == Tools.Sprint);
            bartglFilter.IsChecked = (tools == Tools.Filter);
            bartglSearch.IsChecked = (tools == Tools.Search);
            FileUtils.SaveSetting(bartglSprint);
            FileUtils.SaveSetting(bartglFilter);
            FileUtils.SaveSetting(bartglSearch);
        }

        private void bartglChart_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(sender as AppBarToggleButton);
            Refresh(LoadEnum.Latest);
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
                Refresh(LoadEnum.Latest);
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

        private void entSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            Refresh(LoadEnum.Latest);
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

        private async void bartglCopyFromOmni_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            if (issue == null)
                return;
            var newIssues = new List<JiraIssue>();
            var issues = new List<JiraIssueViewModel>() { issue };
            foreach (var omniIssue in issues.Where(i => !i.IsSubtask))
            {
                var str = await HttpAccess.HttpPostAsync(JiraAccess.IssueUri(JiraSourceEnum.SDLC), JiraAccess.GetNewTaskBody(Project, omniIssue));
                await JiraFileAccess.WriteResults("new.json", str);
                var json = JObject.Parse(str);
                var self = (string)json["self"];
                await HttpAccess.HttpPostAsync(self + @"/remotelink", JiraAccess.GetNewLinkBody(omniIssue));
                newIssues.AddRange(await FindIssues(JiraSourceEnum.SDLC, (string)json["key"]));
            }
            AddIssues(newIssues);

            Refresh(LoadEnum.Latest);
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
        private void bartglCopy_Click(object sender, RoutedEventArgs e)
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

        private async void bartglMail_Click(object sender, RoutedEventArgs e)
        {
            var issue = SelectedIssue;
            CopyToClipboard(issue, true);
            await Launcher.LaunchUriAsync(new Uri(issue.MailToLink()));
        }
        private void tglShow_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(sender as AppBarToggleButton);
            var tgl = (sender as AppBarToggleButton);
            if (tgl == tglDetail)
                ShowSelectedDetails();
            else
                Refresh(LoadEnum.Latest);
        }
    }
}
