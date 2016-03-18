using JiraOne;
using JiraShare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jiragile
{
    public enum ProductEnum
    {
        RTS,
        RA
    }

    public class BugReport
    {
        static private async Task<JiraSet> GetBugSet(string team)
        {
            var useFile = false;
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
                useFile = true;
#endif
            string str = string.Empty;
            if (useFile)
            {
                str = FileUtils.UnZipStr(await FileUtils.ReadAllBytes("Bugs" + team + ".jz"));
                if (string.IsNullOrEmpty(str))
                    useFile = false;
            }
            if (useFile == false)
                str = await JiraHttpAccess.GetBugsLiveAsync(team);
            var rv = JiraSet.Parse(str);
            rv.FromFile = useFile;
            rv.Team = team;
            return rv;
        }
        static private DatedStats GetDatedBugs(List<BugWithChanges> allBugs, DateTime start)
        {
            var rv = new DatedStats();
            var lastTime = start;
            start = start.AddDays(1);
            var end = DateTimeOffset.Now.Date.AddDays(1);
            var bugSnapshots = new List<BugSnapshot>();
            for (var dt = start; dt <= end; dt = dt.AddDays(1))
            {
                rv.ProdCounts.Add(dt);

                foreach (var bug in allBugs)
                {
                    var dailyChanges = bug.SubChanges.Where(s => s.Timestamp > lastTime && s.Timestamp < dt);
                    if (dailyChanges.Count() == 0)
                        continue;
                    rv.ProdCounts.Update(dt, bug, dailyChanges);

                    var lastChangeToday = dailyChanges.Last();
                    var found = bugSnapshots.SingleOrDefault(b => b.Bug.Key == bug.Key);
                    if (found == null)
                        bugSnapshots.Add(new BugSnapshot(bug as BugInfo, lastChangeToday));
                    else
                        found.SubChange = lastChangeToday; // already in list so update for today

                    if (lastChangeToday.IsDone)
                        rv.ProdCounts.UpdateClosed(dt, bug, dailyChanges);
                }
                rv.AddBugs(dt, bugSnapshots);
                lastTime = dt;
            }
            return rv;
        }
        static private async Task<List<BugWithChanges>> ConvertToBugs(JiraSet set)
        {
            var allBugs = new List<BugWithChanges>();
            var outs = new List<string>();
            outs.Add("Type,Date,key,status,sev,change");
            string line;
            foreach (var issue in set.Issues)
            {
                if (BugInfo.IsSR(issue))
                    continue;
                var bug = new BugWithChanges(issue.Key, issue.Summary, issue.FixVersionsString, issue.Resolution);
                allBugs.Add(bug);
                var prevStatus = "Open";
                var prevSeverity = issue.Changes.FirstChange(SubChange._severity)?.OldValue;
                if (prevSeverity == null)
                    prevSeverity = issue.Severity;

                bug.SubChanges.Add(new SubChange(issue.CreatedDate, "Create", prevStatus, prevSeverity));
                line = "ACREATE," + issue.CreatedDate.LocalDateTime + "," + issue.Key + "," + prevStatus + "," + prevSeverity;
                outs.Add(line);
                var changes = issue.Changes.AllChanges.Where(c => c.Field == SubChange._status || c.Field == SubChange._severity);
                foreach (var change in changes)
                {
                    if (change.Field == SubChange._status)
                    {
                        if (prevStatus == change.NewValue)
                            continue;
                        prevStatus = change.NewValue;
                    }
                    else
                    {
                        if (prevSeverity == change.NewValue)
                            continue;
                        prevSeverity = change.NewValue;
                    }
                    line = "CHANGE," + change.Timestamp.LocalDateTime + "," + issue.Key + "," + prevStatus + "," + prevSeverity + "," + change.Field;
                    bug.SubChanges.Add(new SubChange(change.Timestamp, change.Field, prevStatus, prevSeverity));
                    outs.Add(line);
                }
                line = "CURRENT," + DateTimeOffset.Now.LocalDateTime + "," + issue.Key + "," + issue.Status + "," + issue.Severity;
                outs.Add(line);
            }

            await FileUtils.WriteAllText("bugs" + set.Team + " " + DateTimeOffset.Now.ToString("yyyyMMdd HHmm") + ".csv", string.Join(Environment.NewLine, outs.ToArray()));
            return allBugs;
        }
        static private async Task LogBugChanges(List<BugWithChanges> allBugs, string team)
        {
            var bugStats = new List<string>();
            bugStats.Add("bug,first,last,sev,status,resolution,version,blocks,zeros,sevchanges,closedAs0,summary");
            foreach (var bug in allBugs)
            {
                var blocks = bug.SubChanges.Count(s => s.IsBlocked && s.StatusChanged);
                var zeros = bug.SubChanges.Count(s => (s.HasSeverity(0)) && s.SeverityChanged);
                var sevchanges = bug.SubChanges.Count(s => s.SeverityChanged);
                var last = bug.SubChanges.Last();
                var closedAs0 = last.IsDone && last.HasSeverity(0);
                var started = bug.SubChanges.First().Timestamp;
                var ended = last.Timestamp;

                bugStats.Add(bug.Key + "," + started.ToString("g") + "," + ended.ToString("g") + "," + last.Severity + "," + last.Status + "," + bug.Resolution + "," + bug.Version + "," + blocks + "," + zeros + "," + sevchanges + "," + closedAs0 + "," + StringUtils.ReadyForCsv(bug.Summary));
            }
            await FileUtils.WriteAllText("BugStats" + team + " " + DateTimeOffset.Now.ToString("yyyyMMdd HHmm") + ".csv", string.Join(Environment.NewLine, bugStats.ToArray()));
        }
        static private async Task CreateSeverityFile(DatedStats datedStats, string team, bool fromFile)
        {
            var datedCounts = new List<CountsAll>();
            foreach (var kvp in datedStats.Bugs)
            {
                var dt = kvp.Key;
                var bugs = kvp.Value;
                var dayCounts = new CountsAll(dt.Date);
                foreach (ProductEnum product in Enum.GetValues(typeof(ProductEnum)))
                {
                    var byProd = dayCounts.ByProd[product];
                    for (int iSev = 0; iSev <= 3; iSev++)
                        byProd.BySev[iSev].UpdateCounts(datedStats, bugs, dt.Date, product, iSev);
                }
                datedCounts.Add(dayCounts);
            }
            await WriteFile(datedCounts, team, false, fromFile);
            await WriteFile(datedCounts, team, true, fromFile);
        }

        static async Task WriteFile(List<CountsAll> datedCounts, string team, bool classic, bool fromFile)
        {
            var lines = new List<string>();
            lines.Add(CountsAll.CsvHeader(classic));
            foreach (var dc in datedCounts)
                lines.Add(dc.ToCsv(classic));

            var rootName = "sevs";
            if (classic)
                rootName += "Classic";
            var fileName = rootName + team + " " + DateTimeOffset.Now.ToString("yyyyMMdd HHmm");
            if (fromFile)
                fileName = fileName.Replace(rootName, rootName + "FromFile");
            await FileUtils.WriteAllText(fileName + ".csv", string.Join(Environment.NewLine, lines));

            if (classic) // save a copy to upload
                await FileUtils.WriteAllTextWithPicker(fileName, string.Join(Environment.NewLine, lines), "csv");
        }

        internal static async Task Generate(string team)
        {
            var set = await GetBugSet(team);
            List<BugWithChanges> allBugs = await ConvertToBugs(set);

            await LogBugChanges(allBugs, team);

            var start = set.Issues.Min(i => i.CreatedDate).Date;
            var datedStats = GetDatedBugs(allBugs, start);

            await CreateSeverityFile(datedStats, team, set.FromFile);
        }
    }

    public class ProductCounts
    {
        public ProductCounts()
        {
            _addeds = new Dictionary<DateTimeOffset, Dictionary<ProductEnum, int[]>>();
            _closeds = new Dictionary<DateTimeOffset, Dictionary<ProductEnum, int[]>>();
        }

        public void Add(DateTimeOffset dt)
        {
            _addeds.Add(dt, new Dictionary<ProductEnum, int[]>());
            _addeds[dt].Add(ProductEnum.RA, new int[4]);
            _addeds[dt].Add(ProductEnum.RTS, new int[4]);
            _closeds.Add(dt, new Dictionary<ProductEnum, int[]>());
            _closeds[dt].Add(ProductEnum.RA, new int[4]);
            _closeds[dt].Add(ProductEnum.RTS, new int[4]);
        }

        internal void Increment(DirectionEnum direction, DateTime dt, ProductEnum product, int iSev)
        {
            if (direction == DirectionEnum.Added)
                _addeds[dt][product][iSev]++;
            else
                _closeds[dt][product][iSev]++;
        }
        public enum DirectionEnum
        {
            Added,
            Closed
        }
        internal int GetCount(DirectionEnum direction, DateTimeOffset dt, ProductEnum product, int iSev)
        {
            if (direction == DirectionEnum.Added)
                return _addeds[dt][product][iSev];
            else
                return _closeds[dt][product][iSev];
        }

        internal void Update(DateTime dt, BugInfo bug, IEnumerable<SubChange> dailyChanges)
        {
            for (int iSev = 0; iSev <= 3; iSev++)
            {
                if (dailyChanges.Any(d => d.SeverityChanged && d.HasSeverity(iSev)))
                {
                    Increment(ProductCounts.DirectionEnum.Added, dt, bug.Product, iSev);
                    break;
                }
            }
        }

        internal void UpdateClosed(DateTime dt, BugInfo bug, IEnumerable<SubChange> dailyChanges)
        {
            // got closed or resolved
            for (int iSev = 0; iSev <= 3; iSev++)
            {
                if (dailyChanges.Any(d => d.HasSeverity(iSev)))
                {
                    Increment(ProductCounts.DirectionEnum.Closed, dt, bug.Product, iSev);
                    break;
                }
            }
        }

        Dictionary<DateTimeOffset, Dictionary<ProductEnum, int[]>> _addeds;
        Dictionary<DateTimeOffset, Dictionary<ProductEnum, int[]>> _closeds;
    }
    public class DatedStats
    {
        public Dictionary<DateTimeOffset, BugSnapshot[]> Bugs { get; set; } = new Dictionary<DateTimeOffset, BugSnapshot[]>();
        public ProductCounts ProdCounts { get; set; } = new ProductCounts();

        internal void AddBugs(DateTime dt, List<BugSnapshot> bugSnapshots)
        {
            var copiedSnapshots = new BugSnapshot[bugSnapshots.Count()];
            int i = 0;
            foreach (var bss in bugSnapshots)
            {
                copiedSnapshots[i++] = new BugSnapshot(bss);
            }
            Bugs.Add(dt, copiedSnapshots);
        }
    }

    public class BugSnapshot
    {
        public BugSnapshot(BugInfo bug, SubChange subChange)
        {
            Bug = bug;
            SubChange = new SubChange(subChange);
        }

        public BugSnapshot(BugSnapshot other) :
            this(other.Bug, other.SubChange)
        {
        }

        public override string ToString()
        {
            return Bug.Key + "," + SubChange.ToString();
        }

        internal bool HasProduct(ProductEnum product)
        {
            return Bug.Key.StartsWith(product.ToString());
        }

        public BugInfo Bug { get; set; }
        public SubChange SubChange { get; set; }
    }
    public class BugWithChanges : BugInfo
    {
        public BugWithChanges(string key, string summary, string version, string resolution)
            : base(key, summary, version, resolution)
        {
        }

        public List<SubChange> SubChanges { get; set; } = new List<SubChange>();
        public override string ToString()
        {
            return Key + " " + SubChanges.Count() + " changes";
        }

    }
    public class BugInfo
    {
        public BugInfo()
        {
        }

        public BugInfo(string key, string summary, string version, string resolution)
        {
            Key = key;
            Summary = summary;
            Version = version;
            Resolution = resolution;
        }

        public override string ToString()
        {
            return Key;
        }
        public ProductEnum Product
        {
            get
            {
                if (Key.StartsWith("RA"))
                    return ProductEnum.RA;
                else if (Key.StartsWith("RTS"))
                    return ProductEnum.RTS;
                else
                    return ProductEnum.RTS;
            }
        }
        public string Key { get; set; }
        public string Summary { get; set; }
        public string Version { get; set; }
        static public bool IsSR(JiraIssue issue)
        {
            var capSummary = issue.Summary.ToUpper();
            if (capSummary.StartsWith("[") || issue.FixVersionsString.Contains("SR"))
                return true;
            return false;
        }
        public bool IsFixed
        {
            get
            {
                return Resolution == "Fixed";
            }
        }
        public string Resolution { get; private set; }
    }
    public class SubChange
    {
        public SubChange(DateTimeOffset timestamp, string field, string status, string severity)
        {
            Field = field;
            Timestamp = timestamp;
            Status = status;
            Severity = severity;
        }

        public SubChange(SubChange subChange) :
            this(subChange.Timestamp, subChange.Field, subChange.Status, subChange.Severity)
        {
        }

        public override string ToString()
        {
            return Field + " " + Timestamp.Date + " " + Status + " " + Severity;
        }

        internal bool HasSeverity(int i)
        {
            if (string.IsNullOrWhiteSpace(Severity))
                return false;
            return Severity.StartsWith(i.ToString());
        }

        public string Field { get; set; }
        public string Status { get; set; }
        public string Severity { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public bool IsDone
        {
            get
            {
                return (IsResolved || IsClosed); // || Status == "Blocked")
            }
        }
        public bool IsResolved
        {
            get
            {
                return (Status == "Resolved");
            }
        }
        public bool IsClosed
        {
            get
            {
                return (Status == "Closed");
            }
        }
        public bool IsBlocked
        {
            get
            {
                return (Status == "Blocked");
            }
        }
        public bool IsOpen
        {
            get
            {
                return (Status == "Open");
            }
        }
        public static string _status = "status";
        public static string _severity = "CS Severity";

        public bool SeverityChanged
        {
            get
            {
                return (Field == _severity || Field == "Create");
            }
        }
        public bool StatusChanged
        {
            get
            {
                return (Field == _status || Field == "Create");
            }
        }
    }
    public class CountsAll
    {
        public CountsAll(DateTimeOffset dt)
        {
            Timestamp = dt;
            ByProd.Add(ProductEnum.RTS, new CountsForProduct());
            ByProd.Add(ProductEnum.RA, new CountsForProduct());
        }
        public DateTimeOffset Timestamp { get; set; }
        public Dictionary<ProductEnum, CountsForProduct> ByProd { get; set; } = new Dictionary<ProductEnum, CountsForProduct>();
        public CountArray Total
        {
            get
            {
                var rv = new CountArray();
                foreach(var prod in ByProd)
                    rv.Combine(prod.Value.Total, false);
                return rv;
            }
        }

        internal void CombineDays(CountsAll other)
        {
            foreach (var prod in ByProd)
                prod.Value.Combine(other.ByProd[prod.Key]);
        }

        public string ToCsv(bool classic)
        {
            return Timestamp.Date.ToString(@"M/d/yyyy") + "," + string.Join(",", StringParts(classic));
        }
        private string[] StringParts(bool classic)
        {
            var rv = new List<string>();
            foreach (var prod in ByProd)
                rv.AddRange(prod.Value.StringParts(classic));
            rv.AddRange(Total.StringParts(false, classic));
            return rv.ToArray();
        }

        public static string CsvHeader(bool classic)
        {
            var rv = "Date";
            foreach (var product in Enum.GetValues(typeof(ProductEnum)))
            {
                for (int iSev = 0; iSev <= 3; iSev++)
                {
                    rv += CountArray.CsvHeader(product.ToString(), iSev.ToString(), classic);
                }
                rv += CountArray.CsvHeader(product.ToString(), null, classic);
            }
            rv += CountArray.CsvHeader(null, null, classic);
            return rv;
        }
    }
    public class CountsForProduct
    {
        public CountsForProduct()
        {
            for (int i = 0; i <= 3; i++)
                BySev.Add(i, new CountArray());
        }
        public Dictionary<int, CountArray> BySev { get; set; } = new Dictionary<int, CountArray>();

        public CountArray Total
        {
            get
            {
                var rv = new CountArray();
                foreach (var sev in BySev)
                    rv.Combine(sev.Value, false);
                return rv;
            }
        }
        internal void Combine(CountsForProduct other)
        {
            foreach (var sev in BySev)
                sev.Value.Combine(other.BySev[sev.Key], true);
        }
        internal string[] StringParts(bool classic)
        {
            var rv = new List<string>();
            foreach (var sev in BySev)
                rv.AddRange(sev.Value.StringParts(true, classic));
            rv.AddRange(Total.StringParts(false, classic));
            return rv.ToArray();
        }
    }

    public class CountArray
    {
        static public string CsvHeader(string product, string sev, bool classic)
        {
            var rv = string.Empty;
            if ((classic && sev != null) || !classic)
                rv += ",New" + product + sev + ",JustClosed" + product + sev;
            rv += ",Open" + product + sev + ",WIP" + product + sev + ",Blocked" + product + sev + ",Resolved" + product + sev;
            if (!classic)
                rv += ",ClosedFixed" + product + sev + ",ClosedNot" + product + sev;
            rv +=  ",Closed" + product + sev;
            return rv;
        }
        public string[] StringParts(bool sev, bool classic)
        {
            var rv = new List<string>();
            if (!classic || sev)
            {
                rv.Add(OpenedToday.ToString());
                rv.Add(ClosedToday.ToString());
            }
            rv.Add(Open.ToString());
            rv.Add(InProgress.ToString());
            rv.Add(Blocked.ToString());
            rv.Add(Resolved.ToString());
            if (!classic)
            {
                rv.Add(ClosedFixed.ToString());
                rv.Add(ClosedNot.ToString());
            }
            rv.Add(Closed.ToString());

            return rv.ToArray();
        }

        internal void Combine(CountArray other, bool differentDays)
        {
            OpenedToday += other.OpenedToday;
            ClosedToday += other.ClosedToday;
            if (differentDays)
            {
                Open = other.Open;
                InProgress = other.InProgress;
                Blocked = other.Blocked;
                Resolved = other.Resolved;
                ClosedFixed = other.ClosedFixed;
                ClosedNot = other.ClosedNot;
            }
            else
            {
                Open += other.Open;
                InProgress += other.InProgress;
                Blocked += other.Blocked;
                Resolved += other.Resolved;
                ClosedFixed += other.ClosedFixed;
                ClosedNot += other.ClosedNot;
            }
        }

        internal void UpdateCounts(DatedStats datedStats, IEnumerable<BugSnapshot> bugs, DateTime dt, ProductEnum product, int iSev)
        {
            var prodBugs = bugs.Where(b => b.HasProduct(product));

            OpenedToday = datedStats.ProdCounts.GetCount(ProductCounts.DirectionEnum.Added, dt, product, iSev);
            ClosedToday = datedStats.ProdCounts.GetCount(ProductCounts.DirectionEnum.Closed, dt, product, iSev);
            var sevBugs = prodBugs.Where(c => c.SubChange.HasSeverity(iSev));
            Open = sevBugs.Count(c => c.SubChange.IsOpen);
            Blocked = sevBugs.Count(c => c.SubChange.IsBlocked);
            Resolved = sevBugs.Count(c => c.SubChange.IsResolved);
            var closedBugs = sevBugs.Where(c => c.SubChange.IsClosed);
            ClosedFixed = closedBugs.Count(c => c.Bug.IsFixed);
            ClosedNot = closedBugs.Count() - ClosedFixed;
            InProgress = sevBugs.Count() - Open - Blocked - Closed - Resolved;
        }
  
        public int OpenedToday { get; set; }
        public int ClosedToday { get; set; }
        public int Open { get; set; }
        public int Blocked { get; set; }
        public int Resolved { get; set; }
        public int ClosedFixed { get; set; }
        public int ClosedNot { get; set; }
        public int Closed
        {
            get
            {
                return ClosedFixed + ClosedNot;
            }
        }
        public int InProgress { get; set; }
    }
}
