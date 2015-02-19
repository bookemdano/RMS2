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

        #endregion

        public string Description(bool shortForm)
        {
            var rv = "";
            if (IsSubtask)
                rv += "-";
            rv += Key;
            if (shortForm)
                return rv;
            rv += " [" + ShortStatus + "]";
            if (!string.IsNullOrWhiteSpace(Summary))
                rv += " " + Summary;
            if (!string.IsNullOrWhiteSpace(Assignee))
                rv += " by " + Assignee;
            return rv;
        }
        public string HtmlDescription(bool shortForm)
        {
            string link = "<A HREF=" + LinkDirect + ">" + Key;
            if (!shortForm)
                link += "-" + Summary;
            link += "</A>";
            return link;
        }


        public string MailToLink()
        {
            var rv = "mailto:?subject=";
            if (ParentIssue != null)
                rv += new JiraIssueViewModel(ParentIssue).Description(true) + "sub ";

            rv += ConvertToUrl(Description(false));
            return rv;
        }

        #region Interfaces

        public override string ToString()
        {
            return Description(false);
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

        static readonly bool _sublog = true;

    }
}
