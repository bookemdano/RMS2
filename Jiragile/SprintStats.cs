﻿using JiraOne;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JiraShare
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
        // TODONE 2104/07/28 zipped all files
        // TODO Save a copy as json, let clean clear them all out.
        static dynamic _oldStat;
        internal static SprintStats ReadStats(JiraSet currentSprint)
        {
            var key = currentSprint.Key;
            var rv = new SprintStats(key);
            var start = rv.StartTime;
            var end = rv.TargetTime;
            if (end > DateTimeOffset.Now)
                end = DateTimeOffset.Now;

            for (var dt = start; dt <= end; dt = dt.AddHours(1))
            {
                var stat = new SprintStat();
                stat.Timestamp = dt;
                foreach (var issue in currentSprint.Issues)
                {
                    var changes = issue.Changes.FieldChanges("status").Where(c => c.Timestamp < dt);
                    UpdateStat(stat, issue, changes);
                }
                rv.Add(stat);
            }
            return rv;
        }

        private static void UpdateStat(SprintStat stat, JiraIssue issue, IEnumerable<Change> changes)
        {
            if (changes == null || changes.Count() == 0)
                return;
            var lastChange = changes.Last();
            var status = JiraIssue.GetStatusEnum(lastChange.NewValue);
            stat.StoryPointCounts.Total += issue.StoryPoints;
            stat.TaskCounts.Total++;
            if (JiraIssue.IsResolvedEnum(status))
            {
                stat.StoryPointCounts.Resolved += issue.StoryPoints;
                stat.TaskCounts.Resolved++;
            }
            if (JiraIssue.IsTestingEnum(status))
            {
                stat.StoryPointCounts.Testing += issue.StoryPoints;
                stat.TaskCounts.Testing++;
            }
            if (JiraIssue.IsOnHoldEnum(status))
            {
                stat.StoryPointCounts.OnHold += issue.StoryPoints;
                stat.TaskCounts.OnHold++;
            }
            if (JiraIssue.IsInProgressEnum(status))
            {
                stat.StoryPointCounts.InProgress += issue.StoryPoints;
                stat.TaskCounts.InProgress++;
            }
        }

        internal static async Task<SprintStats> ReadStatsOld(JiraSet currentSprint)
        {
            bool logSpeed = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var key = currentSprint.Key;

            var fileTimes = await GetDatedFiles(key);
            if (logSpeed)
                FileUtils.Log("ReadStats GetDatedFiles", sw);

            if (_oldStat != null &&
                _oldStat.Key.ToString() == key.ToString() &&
                _oldStat.Timestamp > DateTimeOffset.Now.AddHours(-4) &&
                _oldStat.DatedFilesCount == fileTimes.Count())
                return _oldStat.Stats;
            if (logSpeed)
                FileUtils.Log("ReadStats CheckOld", sw);
            //Get age in status
            var dts = fileTimes?.Select(f => f.Value);

            var rv = new SprintStats(key);
            if (dts == null)
                return rv;
            rv.AddStats(currentSprint, SprintStat.SpecialEnum.Current);
            var importants = ImportantOnly(dts);
            if (logSpeed)
                FileUtils.Log("ReadStats importants " + dts.Count(), sw);
            bool logEachSpeed = false;
            foreach (var dt in dts)
            {
                try
                {
                    var compressed = await FileUtils.ReadAllBytes(JiraFileAccess.GetFile(key, dt));
                    if (logEachSpeed)
                        FileUtils.Log("ReadAllBytes " + dt, sw);
                    var str = FileUtils.UnZipStr(compressed);

                    if (logEachSpeed)
                        FileUtils.Log("UnZipStr " + dt, sw);
                    if (string.IsNullOrWhiteSpace(str))
                        continue;
                    var jiraSet = JiraSet.Parse(str);
                    if (logEachSpeed)
                        FileUtils.Log("Parse " + dt, sw);
                    jiraSet.RetrieveTime = dt;
                    var important = importants.Any(d => d == dt);
                    rv.AddStats(jiraSet, important ? SprintStat.SpecialEnum.Significant : SprintStat.SpecialEnum.NotSpecial);
                    if (logEachSpeed)
                        FileUtils.Log("Done " + dt, sw);
                }
                catch (Exception exc)
                {
                    FileUtils.ErrorLog("ReadOlds", exc);
                }
            }

            _oldStat = new { Key = key, Stats = rv, Timestamp = DateTimeOffset.Now, DatedFilesCount = fileTimes.Count };
            return rv;
        }

        static private async Task<Dictionary<string, DateTimeOffset>> GetDatedFiles(SprintKey sprintKey)
        {
            var files = await FileUtils.GetFiles(JiraFileAccess.GetFileMask(sprintKey));

            if (files == null || files.Count() == 0)
                return null;

            var fileDates = new Dictionary<string, DateTimeOffset>();
            foreach (var file in files)
                fileDates.Add(file, JiraFileAccess.DateTimeFromFileName(file));
            return fileDates;
        }

        static private List<DateTimeOffset> ImportantOnly(IEnumerable<DateTimeOffset> dts)
        {
            var rv = new List<DateTimeOffset>();
            var ordered = dts.OrderBy(d => d);
            var notToday = ordered.Where(kvp => kvp.Date != DateTime.Today);
            if (notToday.Count() == 0)
                return rv;

            var lastBeforeToday = notToday.Last();
            foreach (var d in ordered)
            {
                if (d == DateTimeOffset.MinValue)
                    continue;
                var date = d.Date;
                if (!rv.Any(kvp2 => kvp2.Date == date))
                    rv.Add(d);
                else if (d == lastBeforeToday)
                    rv.Add(d);
            }
            return rv;
        }


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
        public int MinStoryPointCount
        {
            get
            {
                return Stats.Min(s => s.StoryPointCounts.Total);
            }
        }
        public int MaxStoryPointCount
        {
            get
            {
                return Stats.Max(s => s.StoryPointCounts.Total);
            }
        }
        public int MinTaskCount
        {
            get
            {
                return Stats.Min(s => s.TaskCounts.Resolved);
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

        internal void AddStats(JiraSet jiraSet, SprintStat.SpecialEnum special)
        {
            Stats.Add(new SprintStat(jiraSet, special));
        }
        internal void Add(SprintStat stat)
        {
            Stats.Add(stat);
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
        public SprintStat()
        {

        }
        public SprintStat(JiraSet jiraSet, SpecialEnum special)
        {
            Timestamp = jiraSet.RetrieveTime;
            ShallowSprint = jiraSet;
            StoryPointCounts.Total = (int)jiraSet.Issues.Sum(i => i.StoryPoints);
            var parents = jiraSet.Issues.Where(i => !i.IsSubtask);
            StoryPointCounts.Resolved = (int)parents.Where(i => i.IsResolved).Sum(i => i.StoryPoints);
            StoryPointCounts.Testing = (int)parents.Where(i => i.IsTesting).Sum(i => i.StoryPoints);
            StoryPointCounts.OnHold = (int)parents.Where(i => i.IsOnHold).Sum(i => i.StoryPoints);
            StoryPointCounts.InProgress = (int)parents.Where(i => i.IsInProgress).Sum(i => i.StoryPoints);

            TaskCounts.Total = jiraSet.Issues.Sum(i => i.SubTasks.Count()) + jiraSet.Issues.Count();
            TaskCounts.Resolved = jiraSet.Issues.Sum(i => i.SubTasks.Count(s => s.IsResolved)) + jiraSet.Issues.Count(s => s.IsResolved);
            TaskCounts.Testing = jiraSet.Issues.Sum(i => i.SubTasks.Count(s => s.IsTesting)) + jiraSet.Issues.Count(s => s.IsTesting);
            TaskCounts.OnHold = jiraSet.Issues.Sum(i => i.SubTasks.Count(s => s.IsOnHold)) + jiraSet.Issues.Count(s => s.IsOnHold);
            TaskCounts.InProgress = jiraSet.Issues.Sum(i => i.SubTasks.Count(s => s.IsInProgress)) + jiraSet.Issues.Count(s => s.IsInProgress);

            Special = special;
        }
        public JiraSet ShallowSprint { get; set; }
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
            return Timestamp.ToString(JiraSet.DateFormatString);
        }
    }
}
