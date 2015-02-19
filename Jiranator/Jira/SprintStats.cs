using System;
using System.Collections.Generic;
using System.Linq;

namespace Jiranator
{
    public enum GroupEnum
    {
        Total,
        InProgress,
        OnHold,
        Testing,
        Resolved
    }
    public class SprintStats
    {
        public SprintStats()
        {

        }
        public SprintStats(SprintKey key)
        {
            Key = key;
            Stats = new List<SprintStat>();
        }

        public SprintKey Key { get; set; }
        DateTimeOffset? _startTime = null;
        public DateTimeOffset StartTime
        {
            get
            {
                if (_startTime == null)
                {
                    var sprintParameters = SprintParameters.GetSprintParameters(Key.Sprint);
                    if (sprintParameters == null)
                    {
                        _startTime = Stats.Min(s => s.Timestamp);
                        if (_startTime.Value.DayOfWeek != DayOfWeek.Monday)
                            _startTime = _startTime.Value.AddDays(0 - (int)_startTime.Value.DayOfWeek + 1);
                    }
                    else
                        _startTime = sprintParameters.StartDate;
                }
                return _startTime.Value;
            }
        }

        internal SprintStat Current
        {
            get
            {
                return Stats.Single(s => s.Special == SprintStat.SpecialEnum.Current);
            }
        }

        public DateTimeOffset TargetTime
        {
            get
            {
                var sprintParameters = SprintParameters.GetSprintParameters(Key.Sprint);
                if (sprintParameters == null)
                    return StartTime.AddDays(11);
                else
                    return sprintParameters.EndDate;
            }
        }
        public List<SprintStat> Stats { get; set; }
        public int MaxStoryPointCount
        {
            get
            {
                return Stats.Max(s => s.StoryPointCounts.Total);
            }
        }
        public int MaxTaskCount
        {
            get
            {
                return Stats.Max(s => s.TaskCounts.Total);
            }
        }
        public double DayCount
        {
            get
            {
                return (TargetTime - StartTime).TotalDays;
            }
        }

        internal void AddStats(JiraSprint jiraSprint, SprintStat.SpecialEnum special)
        {
            Stats.Add(new SprintStat(jiraSprint, special));
        }

        internal double DayPct(DateTimeOffset ts)
        {
            return (ts - StartTime).TotalDays / DayCount;
        }

        internal double TaskPct(int nTasks)
        {
            return (double)nTasks / MaxTaskCount;
        }

        internal double StoryPointPct(int resolvedTasks)
        {
            return (double)resolvedTasks / MaxStoryPointCount;
        }
    }
    public class SprintStat
    {
        public SprintStat(JiraSprint sprint, SpecialEnum special)
        {
            Timestamp = sprint.RetrieveTime;
            ShallowSprint = sprint;
            StoryPointCounts.Total = (int)sprint.Issues.Sum(i => i.StoryPoints);
            var parents = sprint.Issues.Where(i => !i.IsSubtask);
            StoryPointCounts.Resolved = (int)parents.Where(i => i.IsResolved).Sum(i => i.StoryPoints);
            StoryPointCounts.Testing = (int)parents.Where(i => i.IsTesting).Sum(i => i.StoryPoints);
            StoryPointCounts.OnHold = (int)parents.Where(i => i.IsOnHold).Sum(i => i.StoryPoints);
            StoryPointCounts.InProgress = (int)parents.Where(i => i.IsInProgress).Sum(i => i.StoryPoints);

            TaskCounts.Total = sprint.Issues.Sum(i => i.SubTasks.Count()) + sprint.Issues.Count();
            TaskCounts.Resolved = sprint.Issues.Sum(i => i.SubTasks.Count(s => s.IsResolved)) + sprint.Issues.Count(s => s.IsResolved);
            TaskCounts.Testing = sprint.Issues.Sum(i => i.SubTasks.Count(s => s.IsTesting)) + sprint.Issues.Count(s => s.IsTesting);
            TaskCounts.OnHold = sprint.Issues.Sum(i => i.SubTasks.Count(s => s.IsOnHold)) + sprint.Issues.Count(s => s.IsOnHold);
            TaskCounts.InProgress = sprint.Issues.Sum(i => i.SubTasks.Count(s => s.IsInProgress)) + sprint.Issues.Count(s => s.IsInProgress);

            Special = special;
        }
        public JiraSprint ShallowSprint { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public class StatusGroups
        {
            public int Total { get; set; }
            public int InProgress { get; set; }
            public int OnHold { get; set; }
            public int Testing { get; set; }
            public int Resolved { get; set; }
        }

        public StatusGroups StoryPointCounts { get; set; } = new StatusGroups();
        public StatusGroups TaskCounts { get; set; } = new StatusGroups();

        public SpecialEnum Special { get; set; }
        public override string ToString()
        {
            return "Story Points:" + StoryPointCounts.Total + "(" + StoryPointCounts.Resolved + ") SubTasks:" + TaskCounts.Total+ "(" + TaskCounts.Resolved + ")";
        }
        public enum SpecialEnum
        {
            NotSpecial,
            Current,
            Significant
        }

        internal string DateString()
        {
            return Timestamp.ToString(JiraSprint.DateFormatString);
        }
    }
}
