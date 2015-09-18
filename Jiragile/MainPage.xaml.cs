using JiraOne;
using JiraShare;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

            FileUtils.GetSetting(bartglChart, true);

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
                    str = await JiraHttpAccess.GetSprintLiveAsync(sprintKey.Project, sprintKey.Sprint, false);
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

            var issues = RefreshIssues(_jiraSet, entSearch.Text, false, ShowStatus);
            lstIssues.Items.Clear();
            foreach (var issue in issues.OrderBy(i => i.CalcedStatus))
                lstIssues.Items.Add(new IssueControl(issue));
            staStatus.Text = "Updated " + _jiraSet.RetrieveTime.RelativeTime();
            if (bartglChart.IsChecked == true)
            {
                canvas.Visibility = Visibility.Visible;
                rowChart.Height = new GridLength(.5, GridUnitType.Star);
                var stats = await SprintStats.ReadStats(_jiraSet);
                var grapher = new SprintGrapher(canvas, stats, false, true);
            }
            else
            {
                canvas.Visibility = Visibility.Collapsed;
                rowChart.Height = new GridLength(0);
            }
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.SaveSetting(entSprint);
            Refresh(LoadEnum.LiveAlways);
        }

        private static ObservableCollection<JiraIssueViewModel> RefreshIssues(JiraSet jiraSet, string filter, bool showSubtasks, ShowStatusEnum showStatus)
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

            pnlSprint.Visibility = (tools == Tools.Sprint) ? Visibility.Visible : Visibility.Collapsed;
            pnlFilter.Visibility = (tools == Tools.Filter) ? Visibility.Visible : Visibility.Collapsed;
            pnlSearch.Visibility = (tools == Tools.Search) ? Visibility.Visible : Visibility.Collapsed;
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

            var issues = await FindIssues(source, entSearch.Text);
            if (issues == null)
                return;
            AddIssues(issues);
            Refresh(LoadEnum.Latest);
            staStatus.Text = "Searched for " + entSearch.Text;
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
                url = JiraAccess.SearchIssuesUri(source, text);
            var str = await HttpAccess.HttpGetAsync(url, true);
            if (str == null)
                return null;
            try
            {
                JiraFileAccess.WriteResults(text + ".json", str);
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
    }
}
