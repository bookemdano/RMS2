using JiraShare;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Epicnator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void Log(string str, object tag)
        {
            var lbi = new ListBoxItem();
            lbi.Content = str;
            lbi.Tag = tag;
            lbi.MouseDoubleClick += Lbi_MouseDoubleClick;
            lst.Items.Add(lbi);
        }

        private void Lbi_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tag = (sender as ListBoxItem)?.Tag as JiraIssue;
            if (tag != null)
                System.Diagnostics.Process.Start(tag.LinkDirect);
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            btnGo.IsEnabled = false;
            var bw = new BackgroundWorker();
            bw.DoWork += Bw_DoWork;
            bw.WorkerReportsProgress = true;
            bw.ProgressChanged += Bw_ProgressChanged;
            bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
            bw.RunWorkerAsync(entProject.Text);
        }

        private void Bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var dyn = e.UserState as dynamic;
            if (dyn.text == null)
                lst.Items.Clear();
            else
                Log(dyn.text, dyn.issue);
        }

        void ReportProgress(BackgroundWorker bw, string str, JiraIssue issue)
        {
            bw.ReportProgress(0, new { text = str, issue = issue });
        }

        private void Bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnGo.IsEnabled = true;
            btnCopy.IsEnabled = true;
        }


        private void Bw_DoWork(object sender, DoWorkEventArgs e)
        {
            var ts = Stopwatch.StartNew();
            var bw = sender as BackgroundWorker;
            var project = e.Argument as string;
            ReportProgress(bw, null, null);
            //var str = File.ReadAllText(@"C:\OneDrive\Data\Jiranator\MOB-Epics-20150306 112515.json");
            var str = JiraHttpAccess.GetEpicsLive(project, false, false);
            var jiraSet = JiraSet.Parse(str);
            foreach (var epic in jiraSet.Issues)
            {
                var strIssues = JiraHttpAccess.GetEpicsIssuesLive(epic.Key, false, false);
                var issueSprint = JiraSet.Parse(strIssues);
                var notDone = issueSprint.Issues.Where(i => i.Status != JiraIssue.StatusEnum.Resolved && i.Status != JiraIssue.StatusEnum.Closed);
                if (notDone.Count() == 0)
                    continue;

                ReportProgress(bw, "Epic:" + epic.Key + " " + epic.Summary, epic);
                ReportProgress(bw, " Total:" + issueSprint.Issues.Count() + " Not Done:" + notDone.Count(), epic);
                foreach (var issue in notDone)
                {
                    ReportProgress(bw, "-" + issue.Status + " " + issue.Key + " " + issue.Summary, issue);
                }
            }
            ReportProgress(bw, "Done in:" + ts.Elapsed.TotalSeconds.ToString("0") + " seconds", null);
            ReportProgress(bw, "Epics Checked:" + jiraSet.Issues.Count(), null);
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            var lines = new List<string>();
            foreach(ListBoxItem item in lst.Items)
            {
                lines.Add(item.Content as string);
            }
            Clipboard.SetText(string.Join(Environment.NewLine, lines.ToArray()));
            MessageBox.Show("Copied to clipboard.");
        }
    }
}
