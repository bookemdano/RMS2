using System.Windows.Controls;
using System.Linq;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Jiranator
{
    internal class BallGrapher : GraphBase
    {
        public BallGrapher(Panel canvas) : base(canvas)
        {
        }
    }
    internal class SprintBallGrapher : BallGrapher
    {
        internal SprintBallGrapher(Panel canvas, SprintStat stat) : base(canvas)
        {
            if (DataArea == null)
                return;
            try
            {
                var current = stat;   // sprint.Current();
                AddGridAndLabels(current);

                int count = current.ShallowSprint.Issues.Count();
                int iIssue = 0;
                foreach (var issue in current.ShallowSprint.Issues.OrderByDescending(i => i.StoryPoints))
                {
                    var iState = 0;
                    if (issue.CalcedStatus <= JiraIssue.StatusEnum.Open)
                        iState = 0;
                    else if (issue.CalcedStatus <= JiraIssue.StatusEnum.OnHold)
                        iState = 1;
                    else if (issue.CalcedStatus <= JiraIssue.StatusEnum.CodeReview)
                        iState = 2;
                    else if (issue.CalcedStatus == JiraIssue.StatusEnum.TestReady)
                        iState = 3;
                    else if (issue.CalcedStatus == JiraIssue.StatusEnum.InTesting)
                        iState = 4;
                    else if (issue.CalcedStatus == JiraIssue.StatusEnum.Doc)
                        iState = 5;
                    else
                        iState = 6;
                    var pctX = StatePct(iState);
                    var pctY = (double) (iIssue++) / count;  // _rnd.NextDouble();

                    var color = JiraIssueViewModel.GetForeground(issue);
                    if (color == Colors.Black)
                        color = Colors.Gray;
                    if (issue.IssueType == "Spike")
                        AddStar(new Point(pctX, pctY), color, (int)(10 * issue.StoryPoints), issue.Key);
                    else if (issue.IssueType == "Bug")
                        AddDiamond(new Point(pctX, pctY), color, (int)(10 * issue.StoryPoints), issue.Key);
                    else if (issue.IssueType.Contains("Hardware"))
                        AddSquare(new Point(pctX, pctY), color, (int)(10 * issue.StoryPoints), issue.Key);
                    else
                        AddCircle(new Point(pctX, pctY), color, (int)(10 * issue.StoryPoints), issue.Key);
                }
                Border();
            }
            catch (Exception exc)
            {
                FileUtils.ErrorLog("SprintStatus", exc);
            }
        }
        Color RndColor()
        {
            return Color.FromRgb((byte) _rnd.Next(0, 255), (byte) _rnd.Next(0, 255), (byte) _rnd.Next(0, 255));
        }
        Random _rnd = new Random();
        double StatePct(int iState)
        {
            return ((double) iState + .5) / (_states.Count());
        }
        List<string> _states = new List<string>() { "Open", "In Progress", "Code Review", "Test Ready", "Testing", "Doc", "Resolved" };

        internal void AddGridAndLabels(SprintStat stat)
        {
            int bottom = 0;
            AddText(new Point(.1, 1), stat.Timestamp.ToString(), Colors.Black);

            for (int i = 0; i < _states.Count(); i++)
            {
                var pct = StatePct(i);
                AddChartLabel(new Point(pct, bottom), _states[i], Orientation.Horizontal, HorizontalAlignment.Center, VerticalAlignment.Center);
                //AddLine(new Point(pct, 0), new Point(pct, 1), Colors.Gray, 1);
            }
        }
    }
}