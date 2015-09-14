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
            AddString(cmbComponent, _legion);
            AddCheckbox(cmbComponent, "AMX");
            AddCheckbox(cmbComponent, "IMX");
            AddCheckbox(cmbComponent, "WMX");
            AddString(cmbComponent, _none);
            AddCheckbox(cmbComponent, "RMV");
            AddCheckbox(cmbComponent, "Apex");
            AddCheckbox(cmbComponent, "MCP");
            AddCheckbox(cmbComponent, "ALK");

            AddString(cmbVersion, _none);
            AddCheckbox(cmbVersion, "RTS-3.7.3");
            AddCheckbox(cmbVersion, "RTS-3.7.2");
            AddCheckbox(cmbVersion, "RTS-3.7.1");
            AddCheckbox(cmbVersion, "RA-3.8");
            AddCheckbox(cmbVersion, "RA-3.7");
            AddString(cmbVersion, _legion);

            AddString(cmbAssignee, "dfrancis");
            AddString(cmbAssignee, "bpayne");
            AddString(cmbAssignee, "dloyd");
            AddString(cmbAssignee, "ehartig");
            AddString(cmbAssignee, _none);
            AddString(cmbAssignee, "alkra");
            AddString(cmbAssignee, "dshmilo");
            AddString(cmbAssignee, _none);
            AddString(cmbAssignee, "dowen");
            AddString(cmbAssignee, "jyoung");
            AddString(cmbAssignee, _none);
            AddString(cmbAssignee, "lstevens");
        }

        private void AddString(ComboBox cmb, string text)
        {
            cmb.Items.Add(text);
        }

        private void AddCheckbox(ComboBox cmb, string text)
        {
            cmb.Items.Add(new CheckBox() { Content = text });
        }

        public static string _legion = "-legion-";
        public static string _none = "-";
        public EditDetails(JiraIssue issue) :
            this()
        {
            SetAssignee(issue);
            SetComponents(issue);
            SetVersions(issue);

            entSummary.Text = issue.Summary;
            entEstimate.Text = issue.Remaining;
            entLabel.Text = issue.LabelsString;
        }

        private void SetAssignee(JiraIssue issue)
        {
            if (string.IsNullOrWhiteSpace(issue.Assignee))
                Assignee = _none;
            else
                Assignee = issue.Assignee;
            cmbAssignee.Text = Assignee;
        }

        private void SetComponents(JiraIssue issue)
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

        private void SetVersions(JiraIssue issue)
        {
            foreach (var item in cmbVersion.Items)
            {
                var chk = item as CheckBox;
                if (chk == null)
                    continue;
                chk.IsChecked = issue.FixVersions.Contains(chk.Content);
            }

            Versions = issue.FixVersions;
            cmbVersion.Text = issue.FixVersionsString;
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
                entLabel.Text = issue.LabelsString;
            }
            else
            {
                entSummary.Text = _legion;
                entEstimate.Text = _legion;
                entLabel.Text = _legion;
            }
            entSummary.IsEnabled = false;
            entEstimate.IsEnabled = false;
            SetAssignee(issue);
            SetComponents(issue);
            SetVersions(issue);
        }

        static string GetCmb(ComboBox cmb, string original)
        {
            if (cmb.Text == original || cmb.Text == _legion)
                return null;

            return cmb.Text;
        }

        static List<string> GetEnt(TextBox ent, List<string> original)
        {
            if (ent.Text == original?.FirstOrDefault() || ent.Text == _legion)
                return null;

            return new List<string>() { ent.Text };
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
            Labels = GetEnt(entLabel, Labels);
            Assignee = GetCmb(cmbAssignee, Assignee);
            Components = GetCmb(cmbComponent, Components);
            Versions = GetCmb(cmbVersion, Versions);

            Close();
        }

        public string Summary { get; private set; }
        public string Estimate { get; private set; }
        public string Assignee { get; private set; }
        public List<string> Components { get; private set; }
        public List<string> Labels { get; private set; }
        public List<string> Versions { get; private set; }

        private void cmbComponent_DropDownClosed(object sender, EventArgs e)
        {
            var chks = GetCmbChks(cmbComponent);
            cmbComponent.Text = StringUtils.ArrayToString(chks);
        }

        private void cmbVersion_DropDownClosed(object sender, EventArgs e)
        {
            var chks = GetCmbChks(cmbVersion);
            cmbVersion.Text = StringUtils.ArrayToString(chks);
        }
    }
}
