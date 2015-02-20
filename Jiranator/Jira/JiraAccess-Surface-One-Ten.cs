using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Jiranator
{

    // TODONE 8/2014 Way faster
    static public class JiraAccess
    {
        static bool _getAllFields = false;  // gets all fields but takes forever
        static string _latestApi = @"https://roadnetmobiledev.atlassian.net/rest/api/latest";

        static string _linkOnBoard = @"https://roadnetmobiledev.atlassian.net/secure/RapidBoard.jspa?rapidView=3&view=detail&selectedIssue=";
        static string _linkDirect = @"https://roadnetmobiledev.atlassian.net/browse/";
        static public string LinkDirect
        {
            get
            {
                return _linkDirect;
            }
        }
        static public string LinkOnBoard
        {
            get
            {
                return _linkOnBoard;
            }
        }
        static public string GetEncodedCredentials(string username, string password)
        {
            string mergedCredentials = username + ":" + password;
            byte[] byteCredentials = UTF8Encoding.UTF8.GetBytes(mergedCredentials);
            return Convert.ToBase64String(byteCredentials);
        }

        internal static string GetIssueJsonUri(string key)
        {
            return _latestApi + @"/issue/" + key + ".json";
        }

        internal static string SearchIssuesUri(string text)
        {
            //var fmt = _latestApi + @"/search?jql=text~'{0}' OR issue='{0}'&maxResults=200";
            var fmt = _latestApi + @"/search?jql=text~'{0}'&maxResults=200";

            return string.Format(fmt, text);
        }

        internal static string FindIssuesUri(string text)
        {
            var fmt = _latestApi + @"/search?jql=issue='{0}'&maxResults=200";

            return string.Format(fmt, text);
        }

        internal static string GetSprintUri(string project, string sprint)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            var jql = string.Format("project={0}", project);
            if (sprint.ToLower() == "backlog")
                jql += " AND Sprint=EMPTY AND Status!='Resolved' AND Status!='Closed'";
            else
                jql += string.Format(" AND Sprint='{0}'", sprint);
            var request = @"/search?jql=" + jql + "&maxResults=200";
            if (!_getAllFields)   // setting to false gets all fields but takes forever
            {
                var fields = new List<string>() { "parent", "summary", "assignee", "components", "status", "timetracking", JiraIssue.StoryPointField, JiraIssue.IssueTypeField, JiraIssue.SprintField };
                request += "&fields=" + string.Join(",", fields);
            }
            return _latestApi + request;
        }

        internal static string GetIssueUri(string key)
        {
            return _latestApi + @"/issue/" + key;
        }
        internal static string GetIssuesUri()
        {
            return _latestApi + @"/issue";
        }
        internal static JObject MakeJobj(string name, JToken token)
        {
            var rv = new JObject();
            rv.Add(name, token);
            return rv;
        }

        internal static string GetComponentBody(string name)
        {
            if (name == EditDetails._none)
                name = null;
            var jobj1 = MakeJobj("name", name);
            var jarray2 = new JArray();
            jarray2.Add(jobj1);
            var jobj3 = MakeJobj("components", jarray2);
            var fields = MakeJobj("fields", jobj3);

            var rv = fields.ToString();
            return rv;
            // "components":[{"self":"https://roadnetmobiledev.atlassian.net/rest/api/2/component/10000","id":"10000","name"
        }

        internal static string GetAssignBody(string name)
        {
            if (name == EditDetails._none)
                name = null;
            var jobj1 = MakeJobj("name", name);
            var jobj2 = MakeJobj("assignee", jobj1);
            var fields = MakeJobj("fields", jobj2);

            // "{\"fields\": {\"assignee\":{\"name\":\"\"}}}";
            return fields.ToString();
        }

        internal static string GetNewSubtaskBody(string project, JiraIssue issue, string summary, string originalEstimate, string assignee)
        {
            dynamic subtask = new JObject();
            /*
            subtask.summary = "armstrong";
            subtask.description = "described";
            subtask.project = MakeJobj("key", project);
            subtask.issuetype = MakeJobj("name", "Bug");
            */
            subtask.summary = summary;
            subtask.description = "added by Jiranator";
            subtask.project = MakeJobj("key", project);
            if (summary.Contains("DOC"))
                subtask.issuetype = MakeJobj("name", "Doc Sub-task");
            else
                subtask.issuetype = MakeJobj("name", "Sub-task");
            subtask.parent = MakeJobj("id", issue.Key);
            subtask.assignee = MakeJobj("name", assignee);
            subtask.timetracking = MakeJobj("originalEstimate", originalEstimate);

            var rv = MakeJobj("fields", subtask);
            return rv.ToString();
        }
    }
    static public class JiraAccessFile
    {
        static bool _saveUncompressedCopy = false;

        internal static void CleanUp()
        {
            var files = Directory.GetFiles(Dir, "*.jz");
            var datedFiles = files.Where(f => DateTimeFromFileName(f) != DateTime.MinValue).ToArray();
            var groupedDatedFiles = datedFiles.GroupBy(f => DateTimeFromFileName(f).Date);

            var saved = new List<string>();
            foreach (var kvp in groupedDatedFiles)
            {
                if (kvp.Key == DateTimeOffset.MinValue.Date)
                    continue;
                saved.Add(kvp.First());
                if (!saved.Contains(kvp.Last()))
                    saved.Add(kvp.Last());
            }
            foreach (var datedFile in datedFiles)
            {
                if (!saved.Contains(datedFile))
                    File.Delete(datedFile);
            }
        }

        static string _dir;
        internal static string Dir
        {
            get
            {
                if (_dir != null)
                    return _dir;
                try
                {
                    _dir = FileUtils.GetDataPath();
                    _dir = Path.Combine(_dir, "Jiranator");
                    Directory.CreateDirectory(_dir);
                }
                catch (Exception)
                {
                    _dir = ".";
                }
                return _dir;
            }
        }

        static public DateTimeOffset? OldCompare { get; set; }

        static private string GetLatestFileName(SprintKey key, LoadEnum load)
        {
            var files = Directory.GetFiles(Dir, GetFileMask(key));
            IEnumerable<string> subFiles;
            if (load == LoadEnum.Yesterday)
            {
                if (OldCompare == null)
                    subFiles = files.Where(f => DateTimeFromFileName(f) < DateTime.Today);
                else
                {
                    string filename = GetFilename(key, OldCompare.Value);
                    subFiles = files.Where(f => f == filename);

                }
            }
            else
                subFiles = files;

            if (subFiles == null || subFiles.Count() == 0)
                return null;
            return subFiles.OrderByDescending(f => DateTimeFromFileName(f)).First();
        }

        private static string GetFileMask(SprintKey key)
        {
            return key.ToFilename() + "-*.jz";
        }

        private static string GetFilename(SprintKey key, DateTimeOffset timestamp)
        {
            return Path.Combine(Dir, key.ToFilename()+ "-" + timestamp.ToString("yyyyMMdd HHmmss") + ".jz");
        }

        static private Dictionary<string, DateTimeOffset> GetDatedFiles(SprintKey sprintKey)
        {
            var files = Directory.GetFiles(Dir, GetFileMask(sprintKey));

            if (files == null || files.Count() == 0)
                return null;

            var fileDates = new Dictionary<string, DateTimeOffset>();
            foreach (var file in files)
                fileDates.Add(file, DateTimeFromFileName(file));
            return fileDates;
        }

        private static DateTimeOffset DateTimeFromFileName(string filename)
        {
            var parts = Path.GetFileName(filename).Split("-.".ToCharArray());
            var rv = DateTimeOffset.MinValue;
            try
            {
                rv = DateTime.ParseExact(parts[2], "yyyyMMdd HHmmss", null);
            }
            catch (Exception)
            {
            }
            return rv;
        }

        internal static void Write(SprintKey sprintKey, string str)
        {
            var name = GetFilename(sprintKey, DateTimeOffset.Now);
            var compressed = ZipStr(str);
            File.WriteAllBytes(name, compressed);
            if (_saveUncompressedCopy)   // write an uncompressed copy
            {
                name = Path.ChangeExtension(name, ".json");
                File.WriteAllText(name, str);
            }
        }

        internal static string Read(SprintKey sprintKey, LoadEnum load, out DateTimeOffset dt)
        {
            var filename =GetLatestFileName(sprintKey, load);
            dt = DateTimeOffset.MinValue;
            if (filename == null)
                return null;
            dt = JiraAccessFile.DateTimeFromFileName(filename);
            var compressed = File.ReadAllBytes(filename);
            return UnZipStr(compressed);
        }
        // TODONE 2104/07/28 zipped all files
        // TODO Save a copy as json, let clean clear them all out.
        static dynamic _oldStat;
        internal static SprintStats ReadStats(JiraSprint currentSprint)
        {
            bool logSpeed = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var key = currentSprint.Key;

            var fileTimes = GetDatedFiles(key);
            if (logSpeed)
                FileUtils.Log("ReadStats GetDatedFiles", sw);

            if (_oldStat != null && _oldStat.Key.ToString() == key.ToString() && _oldStat.Timestamp > DateTimeOffset.Now.AddHours(-4) && _oldStat.DatedFilesCount == fileTimes.Count())
                return _oldStat.Stats;
            if (logSpeed)
                FileUtils.Log("ReadStats CheckOld", sw);

            var dts = fileTimes.Select(f => f.Value);
            var rv  = new SprintStats(key);
            rv.AddStats(currentSprint, SprintStat.SpecialEnum.Current);
            var importants = ImportantOnly(dts);
            if (logSpeed)
                FileUtils.Log("ReadStats importants " + dts.Count(), sw);
            bool logEachSpeed = false;
            foreach (var dt in dts)
            {
                try
                {
                    var name = GetFilename(key, dt);
                    var compressed = File.ReadAllBytes(name);
                    if (logEachSpeed)
                        FileUtils.Log("ReadAllBytes " + dt, sw);
                    var str = UnZipStr(compressed);

                    if (logEachSpeed)
                        FileUtils.Log("UnZipStr " + dt, sw);
                    if (string.IsNullOrWhiteSpace(str))
                        continue;
                    var jiraSprint = JiraSprint.Parse(JObject.Parse(str));
                    if (logEachSpeed)
                        FileUtils.Log("Parse " + dt, sw);
                    jiraSprint.RetrieveTime = dt;
                    var important = importants.Any(d => d == dt);
                    rv.AddStats(jiraSprint, important?SprintStat.SpecialEnum.Significant: SprintStat.SpecialEnum.NotSpecial);
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

        public static byte[] ZipStr(String str)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream gzip =
                  new DeflateStream(output, CompressionMode.Compress))
                {
                    using (StreamWriter writer =
                      new StreamWriter(gzip, System.Text.Encoding.UTF8))
                    {
                        writer.Write(str);
                    }
                }

                return output.ToArray();
            }
        }

        public static string UnZipStr(byte[] input)
        {
            using (MemoryStream inputStream = new MemoryStream(input))
            {
                using (DeflateStream gzip =
                  new DeflateStream(inputStream, CompressionMode.Decompress))
                {
                    using (StreamReader reader =
                      new StreamReader(gzip, System.Text.Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
    }
}
