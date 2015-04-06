using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Jirzooky.Resources;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Windows.Media;
using Microsoft.Phone.Tasks;
using Taskino;

namespace Jirzooky
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            BuildLocalizedApplicationBar();
        }

        string _project = "MOB";
        string _sprint = null;
        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            _sprint = new SettingsStore().GetValueOrDefault("Current Sprint", "Sprint 22");
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            // TODO calc sprint based on date
            var str = IsolatedStore.ReadAllText(GetLastFileName(_project, _sprint, LoadEnum.Latest));
            if (!string.IsNullOrWhiteSpace(str))
            {
                _jiraSprint = JiraSprint.Parse(JObject.Parse(str));
                UpdateOldStatus(_jiraSprint);
            }
            else
                _jiraSprint = new JiraSprint();
            RefreshIssues();
            BeginHttpGet(JiraAccess.GetSprintUri(_project, _sprint));
        }

        private void UpdateOldStatus(JiraSprint jiraSprint)
        {
            var oldStr = IsolatedStore.ReadAllText(GetLastFileName(_project, _sprint, LoadEnum.Yesterday));
            var oldJira = JiraSprint.Parse(JObject.Parse(oldStr));
            jiraSprint.UpdateOldStatus(oldJira);
        }

        static string EncodedCredentials
        {
            get
            {
                return JiraAccess.GetEncodedCredentials("orashkevych", "roadnet");
            }
        }

        void BeginHttpGet(string url)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            //request.ContentType = "application/json";
            request.Method = "GET";
            request.Headers["Authorization"] = "Basic " + EncodedCredentials;
           
            request.BeginGetResponse(GetResponseCallback, request);
        }

        private void GetResponseCallback(IAsyncResult ar)
        {
            HttpWebRequest request = (HttpWebRequest)ar.AsyncState;
            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(ar);
            Stream streamResponse = response.GetResponseStream();
            StreamReader streamRead = new StreamReader(streamResponse);
            string responseString = streamRead.ReadToEnd();
            streamResponse.Close();
            streamRead.Close();
            response.Close();

            var jiraSprint = JiraSprint.Parse(JObject.Parse(responseString));
            if (jiraSprint.GetHashCode() == _jiraSprint.GetHashCode())
                return;
            IsolatedStore.WriteAllText(GetNewFileName(_project, _sprint), responseString);

            _jiraSprint = jiraSprint;
            _jiraSprint.RetrieveTime = DateTime.Now;
            UpdateOldStatus(_jiraSprint);

            RefreshIssues();
        }

        private static string GetNewFileName(string project, string sprint)
        {
            var filename = project + "-" + sprint + "-" + DateTime.Now.ToString("yyyyMMdd") + ".json";
            return Path.Combine("jirzooky", filename);
        }

        private static string GetLastFileName(string project, string sprint, LoadEnum load)
        {
            string currentFile = Path.GetFileName(GetNewFileName(project, sprint));
            var files = IsolatedStore.GetFiles(Path.Combine("jirzooky", project + "-" + sprint + "-*.json"));
            if (files == null)
                return null;
            var rv = currentFile;
            foreach (var file in files.OrderByDescending(f => f))
            {
                if (load == LoadEnum.Latest || file != currentFile)
                {
                    rv = file;
                    break;
                }
            }
            return Path.Combine("jirzooky", rv);
        }

        private void RefreshIssues()
        {
            lst.Dispatcher.BeginInvoke(() =>
            {
                if (_jiraSprint == null)
                    return;
                staTitle.Text = "Jirzooky " + _colorBy + " " + _sprint;
                lst.Items.Clear();
                var bold = _showSubTasks;
                foreach (var issue in _jiraSprint.Issues)   //.Where(i => i.IsSubtask == false))
                {
                    AddIssue(issue, bold);
                    if (_showSubTasks)
                        foreach (var subtask in issue.SubTasks)
                            AddIssue(subtask, false);
                }
            });
        }

        private void AddIssue(JiraIssue issue, bool bold)
        {
            if (!_showResolved && issue.IsResolved)
                return;
            var lbi = new ListBoxItem();
            var vm = new JiraIssueViewModel(issue);
            lbi.Content = vm.ToString();
            lbi.Tag = issue;
            lbi.Foreground = GetBrush(issue);
            lbi.DoubleTap += lbi_DoubleTap;
            lbi.FontSize = 24;
            if (bold)
            {
                lbi.FontWeight = FontWeights.Bold;
                lbi.FontSize = 32;
            }
            lst.Items.Add(lbi);
        }

        void lbi_DoubleTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var selected = lst.SelectedItem as ListBoxItem;
            var issue = selected.Tag as JiraIssue;
            var task = new WebBrowserTask();
            task.Uri = new Uri(issue.LinkDirect);
            task.Show();
        }

        private System.Windows.Media.Brush GetBrush(JiraIssue issue)
        {
            Brush rv = null;
            if (_colorBy == ColorByEnum.Status)
            {
                if (issue.IsResolved)
                    rv = new SolidColorBrush(Colors.DarkGray);
                else if (issue.IsInProgress)
                    rv = new SolidColorBrush(Colors.Green);
                else if (issue.IsOnHold)
                    rv = new SolidColorBrush(Colors.Red);
                else if (issue.IsTesting)
                    rv = new SolidColorBrush(Colors.Orange);
                else if (issue.IsOpen)
                    rv = new SolidColorBrush(Colors.Yellow);
                else
                    rv = new SolidColorBrush(Colors.White);
            }
            else if (_colorBy == ColorByEnum.StatusChange)
            {
                var delta = issue.CalcedStatus - issue.OldCalcedStatus;
                if (delta > 0)
                    rv = new SolidColorBrush(Colors.Green);
                else if (delta < 0)
                    rv = new SolidColorBrush(Colors.Red);
                else 
                    rv = new SolidColorBrush(Colors.White);
            }
            else if (_colorBy == ColorByEnum.Dude)
            {
                var dude = issue.Assignee;
                if (dude != null)
                    dude = dude.ToLower();
                if (dude == "dfrancis")
                    rv = new SolidColorBrush(Colors.Cyan);
                else if (dude == "dloyd")
                    rv = new SolidColorBrush(Colors.Green);
                else if (dude == "asta")
                    rv = new SolidColorBrush(Colors.Red);
                else if (dude == "jshukla")
                    rv = new SolidColorBrush(Color.FromArgb(255, 128, 196, 196));
                else if (dude == "bpayne")
                    rv = new SolidColorBrush(Colors.Brown);
                else if (dude == "gharrison")
                    rv = new SolidColorBrush(Colors.Purple);
                else if (dude == "lstevens")
                    rv = new SolidColorBrush(Colors.Magenta);
                else if (dude == "jyoung" || dude == "dowen")
                    rv = new SolidColorBrush(Colors.Orange);
                else
                    rv = new SolidColorBrush(Colors.White);
            }
            else if (_colorBy == ColorByEnum.Type)
            {
                var type = issue.IssueType;
                if (type != null)
                    type = type.ToLower();
                if (type == "story")
                    rv = new SolidColorBrush(Colors.Cyan);
                else if (type == "task" || type == "sub-task")
                    rv = new SolidColorBrush(Colors.Green);
                else if (type.Contains("bug"))
                    rv = new SolidColorBrush(Colors.Red);
                else if (type == "improvement")
                    rv = new SolidColorBrush(Color.FromArgb(255, 128, 196, 196));
                else if (type == "spike")
                    rv = new SolidColorBrush(Colors.Brown);
                else if (type == "bug")
                    rv = new SolidColorBrush(Colors.Purple);
                else if (type == "doc sub-task")
                    rv = new SolidColorBrush(Colors.Magenta);
                else if (type == "hardware test request")
                    rv = new SolidColorBrush(Colors.Orange);
                else
                    rv = new SolidColorBrush(Colors.White);
            }
            else
                rv = new SolidColorBrush(Colors.White);

            return rv;
        }
        JiraSprint _jiraSprint = null;
        // Sample code for building a localized ApplicationBar
        private void BuildLocalizedApplicationBar()
        {
            // Set the page's ApplicationBar to a new instance of ApplicationBar.
            ApplicationBar = new ApplicationBar();

            // Create a new button and set the text value to the localized string from AppResources.
            var btnColorBy = new ApplicationBarIconButton(new Uri("/Assets/refresh.png", UriKind.Relative));
            btnColorBy.Text = "By Status";
            btnColorBy.Click += btnColorBy_Click;
            ApplicationBar.Buttons.Add(btnColorBy);

            // Create a new menu item with the localized string from AppResources.
            var mi = new ApplicationBarMenuItem("Show Resolved");
            mi.Click += miResolved_Click;
            ApplicationBar.MenuItems.Add(mi);

            var miSubs = new ApplicationBarMenuItem("Show SubTasks");
            miSubs.Click += miSubTasks_Click;
            ApplicationBar.MenuItems.Add(miSubs);

            var miInc = new ApplicationBarMenuItem("Increment Sprint");
            miInc.Click += miIncSprint_Click;
            ApplicationBar.MenuItems.Add(miInc);

            var miReset = new ApplicationBarMenuItem("Reset Sprint");
            miReset.Click += miResetSprint_Click;
            ApplicationBar.MenuItems.Add(miReset);
        }

        private void miResetSprint_Click(object sender, EventArgs e)
        {
            _sprint = "Sprint 20";
            new SettingsStore().AddOrUpdateValue("Current Sprint", _sprint);
            BeginHttpGet(JiraAccess.GetSprintUri(_project, _sprint));
        }

        private void miIncSprint_Click(object sender, EventArgs e)
        {
            var parts = _sprint.Split(" ".ToCharArray());
            var sprintNumber = int.Parse(parts.Last());
            var newSprint = parts[0] + " " + (sprintNumber + 1);
            _sprint = newSprint;
            new SettingsStore().AddOrUpdateValue("Current Sprint", _sprint);
            BeginHttpGet(JiraAccess.GetSprintUri(_project, _sprint));
        }

        private void btnColorBy_Click(object sender, EventArgs e)
        {
            if (_colorBy == ColorByEnum.Type)
                _colorBy = ColorByEnum.NoColor;
            else
                _colorBy = _colorBy + 1;
            var btn = sender as ApplicationBarIconButton;
            btn.Text = "By " + _colorBy;
            RefreshIssues();
        }

        bool _showSubTasks = false;
        private void miSubTasks_Click(object sender, EventArgs e)
        {
            var mi = sender as ApplicationBarMenuItem;
            _showSubTasks = !_showSubTasks;
            mi.Text = (_showSubTasks ? "Hide" : "Show") + " SubTasks";
            RefreshIssues();
        }

        bool _showResolved = false;
        void miResolved_Click(object sender, EventArgs e)
        {
            var mi = sender as ApplicationBarMenuItem;
            _showResolved = !_showResolved;
            mi.Text = (_showResolved ? "Hide" : "Show") + " Resolved";
            RefreshIssues();
        }

        enum ColorByEnum
        {
            Nah,
            NoColor,
            Status,
            StatusChange,
            Dude,
            Type,
            Change,
        }
        ColorByEnum _colorBy = ColorByEnum.Status;
    }
}