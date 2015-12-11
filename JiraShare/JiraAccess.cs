using JiraOne;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JiraShare
{
    public enum JiraSourceEnum
    {
        SDLC,
        Omnitracs,
        Default = Omnitracs
    }

    // TODONE 8/2014 Way faster
    static public class JiraAccess
    {
        public static readonly string Team = "Mobile";
        static bool _getAllFields = false;  // gets all fields but takes forever
        static public string SourceUrl(JiraSourceEnum source)
        {
            var rv = "";
            if (source == JiraSourceEnum.Omnitracs)
                rv = @"https://jira.omnitracs.com";
            else
                rv = @"https://roadnetmobiledev.atlassian.net";
            return rv;
        }
        static string LatestApi(JiraSourceEnum source)
        {
            return SourceUrl(source) + "/rest/api/latest";
        }

        static public string LinkOnBoard(JiraSourceEnum source)
        {
            return SourceUrl(source) + "/secure/RapidBoard.jspa?rapidView=3&view=detail&selectedIssue=";
        }
        static public string LinkDirect(JiraSourceEnum source)
        {
            return SourceUrl(source) + "/browse/";
        }

        static public string GetEncodedCredentials(string username, string password)
        {
            string mergedCredentials = username + ":" + password;
            byte[] byteCredentials = UTF8Encoding.UTF8.GetBytes(mergedCredentials);
            return Convert.ToBase64String(byteCredentials);
        }

        internal static string GetIssueJsonUri(JiraSourceEnum source, string key)
        {
            return LatestApi(source) + @"/issue/" + key + ".json";
        }
        internal static string GetUsersUri(JiraSourceEnum source)
        {
            return LatestApi(source) + @"/role";
        }

        internal static string SearchIssuesUri(JiraSourceEnum source, string text)
        {
            //var fmt = _latestApi + @"/search?jql=text~'{0}' OR issue='{0}'&maxResults=200";
            var fmt = LatestApi(source) + @"/search?jql=text~'{0}'&maxResults=200";

            return string.Format(fmt, text);
        }

        internal static string FindIssueByKey(JiraSourceEnum source, string key)
        {
            var fmt = LatestApi(source) + @"/search?jql=issue='{0}'&maxResults=200&expand=changelog";
            return string.Format(fmt, key);
        }

        internal static string GetSprintUri(string project, string sprint)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            string jql = "";
            if (string.IsNullOrWhiteSpace(sprint))
            {
                jql = string.Format("project={0}", project);
            }
            else
            {
                if (sprint.ToLower() == "backlog")
                    jql = "Sprint=EMPTY AND Status!='Resolved' AND Status!='Closed'";
                else
                    jql = string.Format("Sprint='{0}'", sprint);
            }
            var request = @"/search?jql=" + jql + "&maxResults=200&expand=changelog";
            request += FieldsToGet();
            return LatestApi(JiraSourceEnum.Default) + request;
        }
        internal static string GetEpicsUri(string project)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            var jql = string.Format("project={0}", project);
            //jql += " AND (issuetype='EPIC' OR 'Epic Link' IS NOT EMPTY)"; // can't use JiraIssue.EpicLinkField in query
            jql += " AND issuetype='EPIC'";
            jql += " AND 'Epic Status'=Done";

            var request = @"/search?jql=" + jql + "&maxResults=1000";
            request += FieldsToGet();
            return LatestApi(JiraSourceEnum.Default) + request;
        }

        internal static string GetIssuesForEpicUri(string epicKey)
        {
            var jql = " 'Epic Link'= " + epicKey; // can't use JiraIssue.EpicLinkField in query
            var request = @"/search?jql=" + jql + "&maxResults=1000";
            request += FieldsToGet();
            return LatestApi(JiraSourceEnum.Default) + request;
        }

        static string FieldsToGet()
        {
            if (_getAllFields)
                return null;
            // setting to false gets all fields but takes forever
            var fields = 
                new List<string>()
                {
                    "parent", "summary", "assignee", "components", "versions", "fixVersions", "status",
                    "timetracking", JiraIssue.StoryPointField, "issuetype", JiraIssue.DefaultSprintField,
                    JiraIssue.EpicLinkField, JiraIssue.EpicStatusField, "labels", JiraIssue.CaseFilesField,
                    JiraIssue.CloudTeamField, JiraIssue.OmniTeamField };
            return "&fields=" + string.Join(",", fields);
        }
        internal static string IssueUri(JiraSourceEnum source, string key)
        {
            return IssueUri(source) + @"/" + key;
        }
        internal static string RemoteLinkUri(JiraSourceEnum source, string key)
        {
            return IssueUri(source) + @"/" + key + @"/remotelink";
        }
        internal static string IssueUri(JiraSourceEnum source)
        {
            return LatestApi(source) + @"/issue";
        }
        internal static JObject MakeJobj(string name, JToken token)
        {
            var rv = new JObject();
            rv.Add(name, token);
            return rv;
        }

        internal static string GetBodyComponents(List<string> names)
        {
            return GetBodyArray("components", names, true);
        }
        static string _none = "-"; // sync with EditDetails._none
        private static string GetBodyArray(string tag, string name)
        {
            if (name == _none)
                name = null;
            var jobj1 = MakeJobj("name", name);
            var jarray2 = new JArray();
            jarray2.Add(jobj1);
            var jobj3 = MakeJobj(tag, jarray2);
            var fields = MakeJobj("fields", jobj3);

            var rv = fields.ToString();
            return rv;
            // "components":[{"self":"https://roadnetmobiledev.atlassian.net/rest/api/2/component/10000","id":"10000","name"
        }

        private static string GetBodyArray(string tag, List<string> strs, bool named)
        {
            if (strs == null)
                return GetBodyArray(tag, _none);

            var components = new JArray();
            foreach (var name in strs)
            {
                if (named)
                    components.Add(MakeJobj("name", name));
                else
                    components.Add(name);
            }
            var jobj3 = MakeJobj(tag, components);
            var fields = MakeJobj("fields", jobj3);

            var rv = fields.ToString();
            return rv;
            // "components":[{"self":"https://roadnetmobiledev.atlassian.net/rest/api/2/component/10000","id":"10000","name"
        }

        internal static string GetBodyFixVersion(List<string> names)
        {
            return GetBodyArray("fixVersions", names, true);
        }

        internal static string GetBodyLabels(List<string> names)
        {
            return GetBodyArray("labels", names, false);
        }

        internal static string GetBodyTeam(string team)
        {
            var jobj = MakeJobj(JiraIssue.DefaultTeamField, team);

            // "{\"fields\": {\"assignee\":{\"name\":\"\"}}}";
            return jobj.ToString();
        }

        internal static string GetBodyAssign(string name)
        {
            if (name == _none)
                name = null;
            var jobj1 = MakeJobj("name", name);
            var jobj2 = MakeJobj("assignee", jobj1);
            var fields = MakeJobj("fields", jobj2);

            // "{\"fields\": {\"assignee\":{\"name\":\"\"}}}";
            return fields.ToString();
        }

        internal static string GetBodyNewSubtask(string project, JiraIssue issue, string summary, string originalEstimate, string assignee)
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

        internal static string GetBodyCopiedBug(string project, JiraIssue issue)
        {
            dynamic newTask = new JObject();
            /*
            subtask.summary = "armstrong";
            subtask.description = "described";
            subtask.project = MakeJobj("key", project);
            subtask.issuetype = MakeJobj("name", "Bug");
            */
            newTask.summary = issue.Key + " " + issue.Summary;
            newTask.description = "added by Jiranator";
            newTask.project = MakeJobj("key", project);
            newTask.issuetype = MakeJobj("name", "Bug");
            newTask.customfield_11200 = MakeJobj("value", Team);   //JiraIssue.TeamField
            //newTask.team
            var rv = MakeJobj("fields", newTask);
            return rv.ToString();
        }

        internal static string GetBodySplitStory(JiraIssue issue, string part)
        {
            dynamic newTask = new JObject();
            /*
            subtask.summary = "armstrong";
            subtask.description = "described";
            subtask.project = MakeJobj("key", project);
            subtask.issuetype = MakeJobj("name", "Bug");
            */
            newTask.summary = issue.Summary + "-" + part;
            newTask.description = "added by Jiranator";
            newTask.project = MakeJobj("key", issue.Project);
            newTask.issuetype = MakeJobj("name", issue.IssueType);
            newTask.customfield_11200 = MakeJobj("value", Team);   //JiraIssue.TeamField
            //newTask.team
            var rv = MakeJobj("fields", newTask);
            return rv.ToString();
        }

        internal static string GetBodyNewLink(JiraIssue issue)
        {
            dynamic newTask = new JObject();
            /*
            subtask.summary = "armstrong";
            subtask.description = "described";
            subtask.project = MakeJobj("key", project);
            subtask.issuetype = MakeJobj("name", "Bug");
            */
            newTask.url = issue.LinkDirect;
            newTask.title = "Jira Omni";

            var rv = MakeJobj("object", newTask);
            return rv.ToString();
        }

        internal static JiraSourceEnum DetermineSource(string url)
        {
            if (url.Contains(JiraAccess.SourceUrl(JiraSourceEnum.Omnitracs)))
                return JiraSourceEnum.Omnitracs;
            else
                return JiraSourceEnum.SDLC;
        }
    }
    static public class JiraFileAccess
    {
        static bool _saveUncompressedCopy = false;

        internal static async Task<int> CleanUp()
        {
            var files = await FileUtils.GetFiles("*.jz");
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
            int rv = 0;
            foreach (var datedFile in datedFiles)
            {
                if (!saved.Contains(datedFile))
                {
                    FileUtils.Delete(datedFile);
                    rv++;
                }
            }
            return rv;
        }   

        static public DateTimeOffset? OldCompare { get; set; }

        static private async Task<string> GetLatestFileName(SprintKey key, LoadEnum load)
        {
            var files = await FileUtils.GetFiles(GetFileMask(key));
            IEnumerable<string> subFiles;
            if (load == LoadEnum.Yesterday)
            {
                if (OldCompare == null)
                    subFiles = files.Where(f => DateTimeFromFileName(f) < DateTime.Today);
                else
                {
                    string filename = GetFile(key, OldCompare.Value);
                    subFiles = files.Where(f => f == filename);
                }
            }
            else
                subFiles = files;

            if (subFiles == null || subFiles.Count() == 0)
                return null;
            return subFiles.OrderByDescending(f => DateTimeFromFileName(f)).First();
        }

        public static string GetFileMask(SprintKey key)
        {
            return key.ToFilename() + "-*.jz";
        }

        public static string GetFile(SprintKey key, DateTimeOffset timestamp)
        {
            return GetFile(key.ToFilename(), timestamp);
        }

        public static string GetFile(string filenameStub, DateTimeOffset timestamp)
        {
            return filenameStub + "-" + timestamp.ToString("yyyyMMdd HHmmss") + ".jz";
        }

        public static async Task WriteResults(string filename, string str)
        {
            await FileUtils.WriteAllText(filename, str);
        }

        public static DateTimeOffset DateTimeFromFileName(string filename)
        {
            var parts = System.IO.Path.GetFileNameWithoutExtension(filename).Split("-".ToCharArray());
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

        internal static async Task Write(string filenameStub, string str)
        {
            var name = GetFile(filenameStub, DateTimeOffset.Now);
            if (str.Length == 0)
                return;
            var compressed = ZipStr(str);
            if (compressed.Length == 0)
                return;
            await FileUtils.WriteAllBytes(name, compressed);
            if (_saveUncompressedCopy)   // write an uncompressed copy
            {
                name = System.IO.Path.ChangeExtension(name, ".json");
                await FileUtils.WriteAllText(name, str);
            }
        }

        internal static async Task<Tuple<string, DateTimeOffset>> Read(SprintKey sprintKey, LoadEnum load)
        {
            var filename = await GetLatestFileName(sprintKey, load);
            if (filename == null)
                return null;
            var dt = JiraFileAccess.DateTimeFromFileName(filename);
            var compressed = await FileUtils.ReadAllBytes(filename);
            if (compressed == null || compressed.Length == 0)
                return null;
            var str = UnZipStr(compressed);
            return new Tuple<string, DateTimeOffset>(str, dt);
        }


        public static byte[] ZipStr(String str)
        {
            using (var output = new System.IO.MemoryStream())
            {
                using (DeflateStream gzip =
                  new DeflateStream(output, CompressionMode.Compress))
                {
                    using (var writer =
                      new System.IO.StreamWriter(gzip, System.Text.Encoding.UTF8))
                    {
                        writer.Write(str);
                    }
                }

                return output.ToArray();
            }
        }

        public static string UnZipStr(byte[] input)
        {
            using (var inputStream = new System.IO.MemoryStream(input))
            {
                using (var gzip =
                  new DeflateStream(inputStream, CompressionMode.Decompress))
                {
                    using (var reader =
                      new System.IO.StreamReader(gzip, System.Text.Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
    }
    public static class JiraHttpAccess
    {
        public static async Task<string> GetSprintLiveAsync(string project, string sprint, bool returnError)
        {
            var rv = await HttpAccess.HttpGetAsync(JiraAccess.GetSprintUri(project, sprint), returnError);
            if (!rv.StartsWith("ERROR:"))
                JiraFileAccess.Write(new SprintKey(project, sprint).ToFilename(), rv);
            return rv;
        }
        public static async Task<string> GetSprintLive(string project, string sprint, bool showError)
        {
            var rv = await HttpAccess.HttpGetAsync(JiraAccess.GetSprintUri(project, sprint), showError);
            //var rv = HttpAccess.HttpGet(JiraAccess.GetSprintUri(project, sprint), showError);
            //File.WriteAllLines("search.fancy.json", SplitLinesDeep(str));
            JiraFileAccess.Write(new SprintKey(project, sprint).ToFilename(), rv);
            return rv;
        }
        public static async Task<string> GetEpicsLive(string project, bool showError, bool saveFile = true)
        {
            var rv = await HttpAccess.HttpGetAsync(JiraAccess.GetEpicsUri(project), showError);
            if (saveFile)
                JiraFileAccess.Write(project + "-Epics", rv);
            return rv;
        }

        internal static async Task<string> GetEpicsIssuesLive(string epicKey, bool showError, bool saveFile = true)
        {
            var rv = await HttpAccess.HttpGetAsync(JiraAccess.GetIssuesForEpicUri(epicKey), showError);
            if (saveFile)
                JiraFileAccess.Write("Epic-" + epicKey + "-Issues", rv);
            return rv;
        }
    }
}
