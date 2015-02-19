using System;
using System.Collections.Generic;
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

namespace Jiranator
{
    /// <summary>
    /// Interaction logic for EditDetails.xaml
    /// </summary>
    public partial class EditDetails : Window
    {
        public EditDetails()
        {
            InitializeComponent();
            cmbComponent.Items.Add(_legion);
            cmbComponent.Items.Add(new CheckBox() { Content = "AMX" });
            cmbComponent.Items.Add(new CheckBox() { Content = "IMX" });
            cmbComponent.Items.Add(new CheckBox() { Content = "WMX" });
            cmbComponent.Items.Add(_none);
            cmbComponent.Items.Add(new CheckBox() { Content = "RMV" });
            cmbComponent.Items.Add(new CheckBox() { Content = "Apex" });
            cmbComponent.Items.Add(new CheckBox() { Content = "MCP" });
            cmbComponent.Items.Add(new CheckBox() { Content = "ALK" });

            cmbVersion.Items.Add(_none);
            cmbVersion.Items.Add("RTS-3.7.1");
            cmbVersion.Items.Add("RTS-3.6.9");
            cmbVersion.Items.Add("RTS 3.6.8 SR1");
            cmbVersion.Items.Add("RTS 3.6.8");
            cmbVersion.Items.Add("Apex 1.4");
            cmbVersion.Items.Add("RA-3.5");
            cmbVersion.Items.Add(_legion);

            cmbAssignee.Items.Add("dfrancis");
            cmbAssignee.Items.Add("bpayne");
            cmbAssignee.Items.Add("dloyd");
            cmbAssignee.Items.Add("ehartig");
            cmbAssignee.Items.Add(_none);
            cmbAssignee.Items.Add("alkra");
            cmbAssignee.Items.Add("dshmilo");
            cmbAssignee.Items.Add(_none);
            cmbAssignee.Items.Add("dowen");
            cmbAssignee.Items.Add("jyoung");
            cmbAssignee.Items.Add(_none);
            cmbAssignee.Items.Add("lstevens");
        }
        static string _legion = "-legion-";
        public static string _none = "-";
        public EditDetails(JiraIssue issue) :
            this()
        {
            SetAssignee(issue);
            SetComponent(issue);
            SetVersion(issue);

            entSummary.Text = issue.Summary;
            entEstimate.Text = issue.Remaining;
        }

        private void SetAssignee(JiraIssue issue)
        {
            if (string.IsNullOrWhiteSpace(issue.Assignee))
                Assignee = _none;
            else
                Assignee = issue.Assignee;
            cmbAssignee.Text = Assignee;
        }

        private void SetComponent(JiraIssue issue)
        {
            foreach (var item in cmbComponent.Items)
            {
                var chk = item as CheckBox;
                if (chk == null)
                    continue;
                chk.IsChecked = issue.Components.Contains(chk.Content);
            }

            Components = issue.Components;
            cmbComponent.Text = issue.ComponentsString;
        }

        private void SetVersion(JiraIssue issue)
        {
            if (issue.FixVersions.Count() > 1)
                Version = _legion;
            else if (issue.FixVersions.Count() == 1)
                Version = issue.FixVersions[0];
            else
                Version = _none;
            cmbVersion.Text = Version;
        }

        public EditDetails(IList<JiraIssue> issues) :           
            this()
        {
            if (issues.Count() == 0)
                return;
            var issue = issues[0];
            if (issues.Count == 1)
            {
                entSummary.Text = issue.Summary;
                entEstimate.Text = issue.Remaining;
            }
            else
            {
                entSummary.Text = _legion;
                entEstimate.Text = _legion;
            }
            entSummary.IsEnabled = false;
            entEstimate.IsEnabled = false;
            SetAssignee(issue);
            SetComponent(issue);
            SetVersion(issue);
        }

        static string GetCmb(ComboBox cmb, string original)
        {
            if (cmb.Text == original || cmb.Text == _legion)
                return null;

            return cmb.Text;
        }

        static List<string> GetCmbChks(ComboBox cmb)
        {
            var rv = new List<string>();

            foreach (var item in cmb.Items)
            {
                var chk = item as CheckBox;
                if (chk == null)
                    continue;
                if (chk.IsChecked == true)
                    rv.Add(chk.Content as string);
            }

            return rv;
        }

        static List<string> GetCmb(ComboBox cmb, List<string> originals)
        {
            if (cmb.Text == StringUtils.ArrayToString(originals) || cmb.Text == _legion)
                return null;

            return GetCmbChks(cmb);
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Summary = entSummary.Text;
            Estimate = entEstimate.Text;

            Assignee = GetCmb(cmbAssignee, Assignee);
            Components = GetCmb(cmbComponent, Components);
            Version = GetCmb(cmbVersion, Version);

            Close();
        }

        public string Summary { get; private set; }
        public string Estimate { get; private set; }
        public string Assignee { get; private set; }
        public List<string> Components { get; private set; }
        public string Version { get; private set; }

        private void cmbComponent_DropDownClosed(object sender, EventArgs e)
        {
            var chks = GetCmbChks(cmbComponent);
            cmbComponent.Text = StringUtils.ArrayToString(chks);
        }
    }
}
