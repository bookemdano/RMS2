using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;

namespace Jiranator
{
    public enum JiraSourceEnum
    {
        SDLC,
        Omnitracs
    }

    // TODONE 8/2014 Way faster
    static public class JiraAccess
    {
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

        internal static string SearchIssuesUri(JiraSourceEnum source, string text)
        {
            //var fmt = _latestApi + @"/search?jql=text~'{0}' OR issue='{0}'&maxResults=200";
            var fmt = LatestApi(source) + @"/search?jql=text~'{0}'&maxResults=200";

            return string.Format(fmt, text);
        }

        internal static string FindIssueByKey(JiraSourceEnum source, string key)
        {
            var fmt = LatestApi(source) + @"/search?jql=issue='{0}'&maxResults=200";
            return string.Format(fmt, key);
        }

        internal static string GetSprintUri(string project, string sprint)
        {
            //str = HttpGet(_latestApi + @"/search?jql=project=MOB AND Sprint='Sprint 16' and issuetype not in (subTaskIssueTypes())&maxResults=200&fields=parent,summary,subtasks,assignee," + JiraIssue.IssueTypeField);
            var jql = string.Format("project={0}", project);
            if (!string.IsNullOrWhiteSpace(sprint))
            {
                if (sprint.ToLower() == "backlog")
                    jql += " AND Sprint=EMPTY AND Status!='Resolved' AND Status!='Closed'";
                else
                    jql += string.Format(" AND Sprint='{0}'", sprint);
            }
            var request = @"/search?jql=" + jql + "&maxResults=200";
            request += FieldsToGet();
            return LatestApi(JiraSourceEnum.SDLC) + request;
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
            return LatestApi(JiraSourceEnum.SDLC) + request;
        }

        internal static string GetIssuesForEpicUri(string epicKey)
        {
            var jql = " 'Epic Link'= " + epicKey; // can't use JiraIssue.EpicLinkField in query
            var request = @"/search?jql=" + jql + "&maxResults=1000";
            request += FieldsToGet();
            return LatestApi(JiraSourceEnum.SDLC) + request;
        }

        static string FieldsToGet()
        {
            if (_getAllFields)
                return null;
            // setting to false gets all fields but takes forever
            var fields = new List<string>() { "parent", "summary", "assignee", "components", "versions", "fixVersions", "status", "timetracking", JiraIssue.StoryPointField, "issuetype", JiraIssue.SprintField, JiraIssue.EpicLinkField, JiraIssue.EpicStatusField };
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

        internal static string GetComponentBody(List<string> names)
        {
            return GetArrayBody("components", names);
        }
        static string _none = "-"; // sync with EditDetails._none
        private static string GetArrayBody(string tag, string name)
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

        private static string GetArrayBody(string tag, List<string> names)
        {
            if (names == null)
                return GetArrayBody(tag, _none);

            var components = new JArray();
            foreach (var name in names)
            {
                components.Add(MakeJobj("name", name));
            }
            var jobj3 = MakeJobj(tag, components);
            var fields = MakeJobj("fields", jobj3);

            var rv = fields.ToString();
            return rv;
            // "components":[{"self":"https://roadnetmobiledev.atlassian.net/rest/api/2/component/10000","id":"10000","name"
        }

        internal static string GetFixVersionBody(string name)
        {
            return GetArrayBody("fixVersions", name);
        }

        internal static string GetAssignBody(string name)
        {
            if (name == _none)
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

        internal static string GetNewTaskBody(string project, JiraIssue issue)
        {
            dynamic newTask = new JObject();
            /*
            subtask.summary = "armstrong";
            subtask.description = "described";
            subtask.project = MakeJobj("key", project);
            subtask.issuetype = MakeJobj("name", "Bug");
            */
            newTask.summary = "TEST-" + issue.Key + " " + issue.Summary;
            newTask.description = "added by Jiranator";
            newTask.project = MakeJobj("key", project);
            newTask.issuetype = MakeJobj("name", "Bug");

            var rv = MakeJobj("fields", newTask);
            return rv.ToString();
        }


        internal static string GetNewLinkBody(JiraIssue issue)
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
    static public class JiraAccessFile
    {
        static bool _saveUncompressedCopy = false;

        internal static int CleanUp()
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
            int rv = 0;
            foreach (var datedFile in datedFiles)
            {
                if (!saved.Contains(datedFile))
                {
                    File.Delete(datedFile);
                    rv++;
                }
            }
            return rv;
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

        public static string GetFileMask(SprintKey key)
        {
            return key.ToFilename() + "-*.jz";
        }

        public static string GetFilename(SprintKey key, DateTimeOffset timestamp)
        {
            return GetFilename(key.ToFilename(), timestamp);
        }

        public static string GetFilename(string filenameStub, DateTimeOffset timestamp)
        {
            return Path.Combine(Dir, filenameStub + "-" + timestamp.ToString("yyyyMMdd HHmmss") + ".jz");
        }

        public static void WriteResults(string filename, string str)
        {
            File.WriteAllText(Path.Combine(Dir, filename), str);
        }

        public static DateTimeOffset DateTimeFromFileName(string filename)
        {
            var parts = Path.GetFileNameWithoutExtension(filename).Split("-".ToCharArray());
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

        internal static void Write(string filenameStub, string str)
        {
            var name = GetFilename(filenameStub, DateTimeOffset.Now);
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
            var filename = GetLatestFileName(sprintKey, load);
            dt = DateTimeOffset.MinValue;
            if (filename == null)
                return null;
            dt = JiraAccessFile.DateTimeFromFileName(filename);
            var compressed = File.ReadAllBytes(filename);
            return UnZipStr(compressed);
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
    public static class JiraHttpAccess
    {
        public static string GetSprintLive(string project, string sprint, bool showError)
        {
            var rv = HttpAccess.HttpGet(JiraAccess.GetSprintUri(project, sprint), showError);
            //var rv = HttpAccess.HttpGet(JiraAccess.GetSprintUri(project, sprint), showError);
            //File.WriteAllLines("search.fancy.json", SplitLinesDeep(str));
            JiraAccessFile.Write(new SprintKey(project, sprint).ToFilename(), rv);
            return rv;
        }
        public static string GetEpicsLive(string project, bool showError, bool saveFile = true)
        {
            var rv = HttpAccess.HttpGet(JiraAccess.GetEpicsUri(project), showError);
            if (saveFile)
                JiraAccessFile.Write(project + "-Epics", rv);
            return rv;
        }

        internal static string GetEpicsIssuesLive(string epicKey, bool showError, bool saveFile = true)
        {
            var rv = HttpAccess.HttpGet(JiraAccess.GetIssuesForEpicUri(epicKey), showError);
            if (saveFile)
                JiraAccessFile.Write("Epic-" + epicKey + "-Issues", rv);
            return rv;
        }
    }

    public static class HttpAccess
    {
        internal static string HttpGet(string url, bool showError)
        {
            var source = JiraAccess.DetermineSource(url);
            string result = null;
            try
            {
                var request = WebRequest.Create(url) as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "GET";
                request.Headers.Add("Authorization", "Basic " + EncodedCredentials(source));
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception exc)
            {
                if (showError)
                    MessageBox.Show(exc.Message);
            }

            return result;
        }

        internal static string HttpPut(string url, string json)
        {
            var source = JiraAccess.DetermineSource(url);
            if (source == JiraSourceEnum.Omnitracs)
                return "Cannot edit Omnitracs Jira";

            try
            {
                var request = WebRequest.Create(url)
                                     as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "PUT";

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                request.Headers.Add("Authorization", "Basic " + EncodedCredentials(source));
                string result = null;
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }

                return result;

            }
            catch (Exception exc)
            {
                return exc.Message;
            }
        }

        internal static string HttpPost(string url, string json)
        {
            var source = JiraAccess.DetermineSource(url);
            if (source == JiraSourceEnum.Omnitracs)
                return "Cannot edit Omnitracs Jira";
            try
            {
                var request = WebRequest.Create(url)
                                     as HttpWebRequest;
                request.ContentType = "application/json";
                request.Method = "POST";

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                request.Headers.Add("Authorization", "Basic " + EncodedCredentials(source));

                string result = null;
                using (var resp = request.GetResponse() as HttpWebResponse)
                {
                    var reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }

                return result;

            }
            catch (Exception exc)
            {
                return exc.Message;
            }
        }

        static string EncodedCredentials(JiraSourceEnum source)
        {
            var rv = "";
            if (source == JiraSourceEnum.SDLC)
                rv = JiraAccess.GetEncodedCredentials("orashkevych", "roadnet");
            else //if (source == JiraSourceEnum.Omnitracs)
                rv = JiraAccess.GetEncodedCredentials("dfrancis", "mibos");
            return rv;
        }


    }
}
