using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace Jiranator
{
    // TODONE added sprint age
    public class JiraIssueViewModel : JiraIssue, INotifyPropertyChanged
    {
        #region Ctor

        public JiraIssueViewModel(JiraIssue issue) :
            base(issue)
        {
        }

        #endregion

        #region Display Related

        public string FontWeight
        {
            get
            {
                _cummulative.Start();
                string rv;
                if (IsSubtask)
                    rv = "Normal";
                else
                    rv = "Bold";
                _cummulative.Stop();
                return rv;
            }
        }
        static Stopwatch _cummulative = new Stopwatch();
        public Brush Foreground
        {
            get
            {
                _cummulative.Start();
                var rv = UIUtils.GetBrush(GetForeground(this));
                _cummulative.Stop();
                return rv;
            }
        }

        static public Color GetForeground(JiraIssue issue)
        {
            if (issue.Source == JiraSourceEnum.Omnitracs)
                return Colors.Blue;

            var delta = issue.CalcedStatus - issue.OldCalcedStatus;
            if (delta > 0)
                return Colors.Green;
            else if (delta < 0)
                return Colors.Red;

            return Colors.Black;
        }

        public string SubTaskCount
        {
            get
            {
                _cummulative.Start();
                string rv; 
                if (SubTasks.Count() == 0)
                    rv = "-";
                else
                    rv = SubTasks.Count().ToString();
                _cummulative.Stop();
                return rv;
            }
        }

        public string SprintCount
        {
            get
            {
                _cummulative.Start();
                var rv = Sprints.Count().ToString();
                _cummulative.Stop();
                return rv;
            }
        }

        public string ShortStatus
        {
            get
            {
                if (CalcedStatus == StatusEnum.OnHold)
                    return "H";
                else if (IsTesting)
                    return "T";
                else
                    return CalcedStatus.ToString()[0].ToString();
            }
        }

        public string OmniKey
        {
            get
            {
                if (!IsFromOmni)
                    return null;

                var parts = Summary.Split(" -".ToCharArray());
                var key = parts[0] + "-" + parts[1];
                if (!IsIssueKey(key))
                    return null;

                return key;
            }
        }
        public bool IsFromOmni
        {
            get
            {
                return Summary.StartsWith("RA-") || Summary.StartsWith("RTS-");
            }
        }

        #endregion

        public string Description(bool includeParent, bool includeSummary, bool includeMeta)
        {
            var rv = "";
            if (includeParent && IsSubtask)
                rv += new JiraIssueViewModel(ParentIssue).Description(false, true, false) + " subtask ";
            rv += Key;
            if (includeMeta)
                rv += " [" + ShortStatus + "]";
            if (includeSummary && !string.IsNullOrWhiteSpace(Summary))
                rv += " " + Summary;
            if (includeMeta && !string.IsNullOrWhiteSpace(Assignee))
                rv += " by " + Assignee;
            return rv;
        }
        public string HtmlDescription(bool includeParent, bool includeSummary)
        {
            var rv = "";
            if (IsSubtask)
                rv += new JiraIssueViewModel(ParentIssue)?.HtmlDescription(false, includeSummary) + " subtask  ";
            // todo- found issues don't set parent correctly

            rv += "<A HREF=" + LinkDirect + ">" + Key;
            if (includeSummary)
                rv += "-" + Summary;
            rv += "</A>";
            return rv;
        }


        public string MailToLink()
        {
            var rv = "mailto:?subject=";
            rv += ConvertToUrl(Description(true, true, true));
            return rv;
        }

        #region Interfaces

        public override string ToString()
        {
            return Description(false, true, true);
        }

        #endregion

        #region Interfaces

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}
