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
            cmbComponent.Items.Add("AMX");
            cmbComponent.Items.Add("IMX");
            cmbComponent.Items.Add("WMX");
            cmbComponent.Items.Add(_none);
            cmbComponent.Items.Add("MCP");
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
            SetComponent(issue);
            SetAssignee(issue);

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
            if (issue.Components.Count() > 1)
                Component = _legion;
            else if (issue.Components.Count() == 1)
                Component = issue.Components[0];
            else
                Component = _none;
            cmbComponent.Text = Component;
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
        }

        static string GetCmb(ComboBox cmb, string original)
        {
            if (cmb.Text == original || cmb.Text == _legion)
                return null;

            return cmb.Text;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Summary = entSummary.Text;
            Estimate = entEstimate.Text;

            Component = GetCmb(cmbComponent, Component);
            Assignee = GetCmb(cmbAssignee, Assignee);

            Close();
        }

        public string Summary { get; private set; }
        public string Estimate { get; private set; }
        public string Assignee { get; private set; }
        public string Component { get; private set; }
    }
}
