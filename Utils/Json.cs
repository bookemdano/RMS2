using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils
{
    public class Json
    {
        FieldValuePairs _fvps = new FieldValuePairs();
        public void SetField(string field, Json jsonValue)
        {
            _fvps.SetValue(field, jsonValue);
        }

        public void SetField(string field, object value)
        {
            _fvps.SetValue(field, value.ToString());
        }

        public bool Contains(string field)
        {
            return _fvps.ContainsField(field);
        }

        public string GetString(string field)
        {
            return _fvps.GetString(field);
        }

        public DateTime GetDateTime(string field)
        {
            return _fvps.GetDateTime(field);
        }

        public int GetInt(string field)
        {
            return _fvps.GetInt(field);
        }

        public string ToJsonString()
        {
            var rv = "{" + Environment.NewLine;
            var chunks = new List<string>();
            chunks.Add(_fvps.ToString());

            foreach (var arraySet in _arraySets)
            {
                var lines = arraySet.ToLines();
                chunks.Add(string.Join(Environment.NewLine, AddTab(lines).ToArray()));
            }

            rv += string.Join("," + Environment.NewLine, chunks);
            rv += Environment.NewLine;
            rv += "}" + Environment.NewLine;
            return rv;
        }

        public override string ToString()
        {
            var rv = "{" + Environment.NewLine;
            var chunks = new List<string>();
            chunks.Add(_fvps.ToString());
            
            foreach (var arraySet in _arraySets)
            {
                var lines = arraySet.ToLines();
                chunks.Add(string.Join(Environment.NewLine, AddTab(lines).ToArray()));
            }

            rv += string.Join("," + Environment.NewLine, chunks);
            rv += Environment.NewLine;
            rv += "}" + Environment.NewLine;
            return rv;
        }

        static public IEnumerable<string> AddTab(IEnumerable<string> lines)
        {
            var rv = new List<string>();
            foreach (var line in lines)
                rv.Add("  " + line);
            return rv;
        }
        public static Json Parse(string str)
        {
            if (str == null)
                return null;
            str = str.Replace(Environment.NewLine, "");

            // subparse
            System.Diagnostics.Debug.Assert(str.StartsWith("{"));
            System.Diagnostics.Debug.Assert(str.EndsWith("}"));
            str = str.Substring(1, str.Length - 2); // remove {}

            var lines = SplitLinesShallow(str);
            var rv = new Json();
            for (int i = 0; i < lines.Count(); i++)
            {
                var line = lines[i];

                rv._fvps.Add(FieldValuePair.Parse(line));
            }
            return rv;
        }

        public static string[] SplitLinesDeep(string str)
        {
            var rv = "";
            int indent = 0;
            bool inQuotes = false;
            foreach (var c in str)
            {
                if (inQuotes)
                {
                    if (c == '\"')
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\"')
                    {
                        inQuotes = true;
                    }
                }
                rv += c;
                if (inQuotes)
                    continue;
                if (c == '{' || c == '[')
                    indent++;
                else if (c == '}' || c == ']')
                    indent--;
                if (c == ',' || c == '{' || c == '[')
                {
                    rv += Environment.NewLine;
                    for (int i = 0; i < indent; i++)
                        rv += "\t";
                }
            }
            return rv.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }
        static char _quote = '\"';
        public static string[] SplitLinesShallow(string str, bool isArray = false)
        {
            var rv = "";
            int sub = 0;
            if (str.StartsWith("{") && !isArray)
                sub = -1;
            bool inQuotes = false;
            int nLines = 0;
            int quoteStart = int.MinValue;
            int quoteEnd = int.MinValue;
            int i = 0;
            string quoted;
            //var result = str.Replace(@"\\\"", @"_");
            //if (result.Length != str.Length)
            //    str = result;
            foreach (var c in str)
            {
                if (inQuotes)
                {
                    if (c == _quote)
                    {
                        inQuotes = false;
                        quoteEnd = i;
                        quoted = str.Substring(quoteStart, i - quoteStart);
                        if (rv.Last() == '\\')
                        {
                            rv = rv.Trim('\\');
                            quoted = quoted.Trim('\\');
                        }
                    }
                }
                else
                {
                    if (c == _quote)
                    {
                        if (quoteEnd == i - 1)
                            continue;
                        inQuotes = true;
                        quoteStart = i;
                    }
                }
                rv += c;
                i++;
                if (inQuotes)
                    continue;
                if (c == '{' || c == '[')
                    sub++;
                else if (c == '}' || c == ']')
                    sub--;
                if (sub == 0 && c == ',')
                {
                    rv += Environment.NewLine;
                    nLines++;
                }
            }
            return rv.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        static public string[] LinesBetween(string[] lines, int startLine, string strEnd)
        {
            var subLines = new List<string>();
            int i = startLine;
            while (true)
            {
                var line = lines[i++];
                subLines.Add(line);
                if (line.Contains(strEnd))
                    break;
            }
            return subLines.ToArray();   
        }

        static public string GetStringBetween(string line, string strStart)
        {
            return GetStringBetween(line, strStart, strStart);
        }
        static private string GetStringBetween(string line, string strStart, string strEnd)
        {
            var start = line.IndexOf(strStart);
            if (start == -1)
                return null;
            start += strStart.Length;
            int end;
            if (strEnd == null)
                end = line.Length;
            else
                end = line.IndexOf(strEnd, start);
            if (end == -1)
                return null;
            var str = line.Substring(start, end - start).Trim();
            return str;
        }

        List<ArraySet> _arraySets = new List<ArraySet>();
        public void SetArray(ArraySet arraySet)
        {
            if (_arraySets.Any(a => a.Key == arraySet.Key))
                _arraySets.Remove(_arraySets.SingleOrDefault(a => a.Key == arraySet.Key));
            _arraySets.Add(arraySet);
        }

        public ArraySet GetArraySet(string key)
        {
            if (!_arraySets.Any(a => a.Key == key))
                return null;
            var arraySet = _arraySets.SingleOrDefault(a => a.Key == key);
            return arraySet;
        }

        public string GetStringFromArray(string key, int index, string field)
        {
            var arraySet = GetArraySet(key);
            if (index >= arraySet.Count())
                return null;
            var fvps = arraySet[index];

            return fvps.GetString(field);
        }

        public void SetArray(string key, List<string> fields, List<List<string>> values)
        {
            var arraySet = new ArraySet(key);
            foreach (var v in values)
            {
                var fvps = new FieldValuePairs();
                int i = 0;
                foreach (var v1 in v)
                {
                    fvps.Add(new FieldValuePair(fields[i], v1));
                    i++;
                }
                arraySet.Add(fvps);
            }
            System.Diagnostics.Debug.Assert(arraySet.Count() == values.Count());
            SetArray(arraySet);
          }

        public void Add(FieldValuePairs fvps)
        {
            _fvps.AddRange(fvps);
        }

        public FieldValuePairs GetValues()
        {
            return _fvps;
        }

    }

    public class FieldValuePairs : List<FieldValuePair>
    {
        public bool ContainsField(string field)
        {
            return this.Any(f => f.Field == field);
        }

        public void SetValue(string field, Json jsonValue)
        {
            if (!ContainsField(field))
            {
                this.Add(new FieldValuePair(field, jsonValue));
            }
            else
            {
                GetFvp(field).JsonValue = jsonValue;
            }
        }

        public void SetValue(string field, string value)
        {
            if (!ContainsField(field))
            {
                this.Add(new FieldValuePair(field, value));
            }
            else
            {
                GetFvp(field).Value = value;
            }
        }

        public string GetString(string field)
        {
            return GetFvp(field).Value;
        }
        public DateTime GetDateTime(string field)
        {
            return DateTime.Parse(GetString(field));
        }
        public int GetInt(string field)
        {
            return int.Parse(GetString(field));
        }
        public override string ToString()
        {
            return string.Join("," + Environment.NewLine, ToLines());
        }
        public string ToJsonString()
        {
            return string.Join("," + Environment.NewLine, ToLines());
        }
        public string[] ToLines()
        {
            var parts = new List<string>();
            foreach (var fvp in this)
            {
                parts.Add(fvp.ToJsonString());
            }
            return Json.AddTab(parts).ToArray();
        }

        public static FieldValuePairs Parse(string[] lines)
        {
            var fvps = new FieldValuePairs();
            foreach (var line in lines)
            {
                if (line.Contains(":"))
                    fvps.Add(FieldValuePair.Parse(line));
            }
            return fvps;
        }

        private FieldValuePair GetFvp(string field)
        {
            System.Diagnostics.Debug.Assert(this.Any(f => f.Field == field), "Field " + field + " Not Found");
            var fvp = this.Single(f => f.Field == field);
            return fvp;
        }
        public Json GetJson(string field)
        {
            return GetFvp(field).JsonValue;
        }

        public List<Json> GetJsonArray(string field)
        {
            return GetFvp(field).JsonArray;
        }
    }
    
    public class FieldValuePair
    {
        public string Field { get; set; }
        public string Value { get; set; }
        public Json JsonValue { get; set; }
        public List<Json> JsonArray { get; set; }

        public FieldValuePair(string field, Json jsonValue)
        {
            Field = field;
            JsonValue = jsonValue;
        }

        public FieldValuePair(string field, List<Json> jsonArray)
        {
            Field = field;
            JsonArray = jsonArray;
        }

        public FieldValuePair(string field, object value)
        {
            Field = field;
            Value = value.ToString();
        }

        public FieldValuePair(string field)
        {
            Field = field;
            Value = null;
        }

        public override string ToString()
        {
            if (JsonValue != null)
                return Field + ": Json";
            return Field + ":" + Value;
        }
        public string ToJsonString()
        {
            if (JsonValue != null)
                return "\"" + Field + "\": \"" + JsonValue.ToJsonString() + "\"";
            return "\"" + Field + "\": \"" + Value + "\"";
        }
        static char[] _trims = "\" ".ToCharArray();
        private string key;
        public static FieldValuePair Parse(string line)
        {
            var mid = line.IndexOf(':');
            var key = line.Substring(0, mid).Trim(_trims);
            var value = line.Substring(mid + 1).Trim(_trims);
            if (value.EndsWith(","))
                value = value.Substring(0, value.Length - 1).Trim(_trims);
            FieldValuePair rv;
            if (value.StartsWith("{"))
                rv = new FieldValuePair(key, Json.Parse(value));
            else if (value.StartsWith("["))
            {
                if (value == "[]")
                    rv = new FieldValuePair(key);
                else
                {
                    var trimmed = value.Trim("[]".ToCharArray());
                    if (trimmed[0] != '{')
                    {
                        // I don't know what this is so just shove it in a string
                        rv = new FieldValuePair(key, trimmed);
                    }
                    else
                    {
                        var sublines = Json.SplitLinesShallow(trimmed, true);
                        var jsonArray = new List<Json>();
                        int isubline = 0;
                        foreach (var subline in sublines)
                        {
                            isubline++;
                            if (isubline == sublines.Count())
                                isubline = 0;
                            jsonArray.Add(Json.Parse(subline.Trim(",".ToCharArray())));
                        }
                        rv = new FieldValuePair(key, jsonArray);
                    }
                    //rv = new FieldValuePair(key, Json.Parse(value));
                }
            }
            else
                rv = new FieldValuePair(key, value);
            return rv;
        }
    }
    public class ArraySet : List<FieldValuePairs>
    {
        public string Key { get; set; }
        public ArraySet(string key, List<FieldValuePairs> fvps) 
            : this (key)
        {
            foreach (var fvp in fvps)
                Add(fvp);
        }

        public ArraySet(string key)
        {
            Key = key;
        }
        public List<string> ToLines()
        {
            var lines = new List<string>();
            lines.Add("\"" + Key + "\": [");
            lines.AddRange(Json.AddTab(ToJustLines()));
            lines.Add("]");
            return lines;
        }
        private List<string> ToJustLines()
        {
            var lines = new List<string>();
            if (Count == 0)
                return lines;
            foreach (var fvp in this)
            {
                lines.Add("{");
                lines.AddRange(Json.AddTab(fvp.ToLines()));
                lines.Add("},");
            }
            lines.RemoveAt(lines.Count() - 1);
            lines.Add("}");
            return lines;
        }


        public static ArraySet Parse(string[] lines)
        {
            var line = lines[0];
            var key = Json.GetStringBetween(line, "\"");
            var rv = new ArraySet(key);
            for (int i = 1; i < lines.Count(); i++)
            {
                if (lines[i].Contains("]"))
                    break;
                var subLines = Json.LinesBetween(lines, i, "}");
                i += subLines.Count() - 1;
                rv.Add(FieldValuePairs.Parse(subLines));
            }
            return rv;
        }
    }

    public interface IJsonifable
    {
        FieldValuePairs ToFvps();
    }

    public class Route : IJsonifable
    {
        public RouteKey RouteKey { get; set; }
        public DateTime RouteStart { get; set; }
        public DateTime RouteComplete { get; set; }

        public FieldValuePairs ToFvps()
        {
            var rv = new FieldValuePairs();
            rv.AddRange(RouteKey.ToFvps());
            rv.Add(new FieldValuePair("RouteStart", RouteStart.ToString()));
            rv.Add(new FieldValuePair("RouteComplete", RouteComplete.ToString()));
            return rv;
        }

        public static Route Fake()
        {
            var rv = new Route();
            rv.RouteKey = RouteKey.Fake();
            rv.RouteStart = rv.RouteKey.RouteDate.AddHours(9);
            rv.RouteComplete = rv.RouteKey.RouteDate.AddHours(17);
            return rv;
        }

        public static Route Parse(FieldValuePairs fvps)
        {
            var rv = new Route();
            rv.RouteKey = RouteKey.Parse(fvps);
            rv.RouteStart = fvps.GetDateTime("RouteStart");
            rv.RouteComplete = fvps.GetDateTime("RouteComplete");
            return rv;
        }
    }

    public class RouteKey : IJsonifable
    {
        public RouteKey(int publicRouteId, string routeId, DateTime routeDate)
        {
            PublicRouteId = publicRouteId;
            RouteId = routeId;
            RouteDate = routeDate;
        }
        public int PublicRouteId { get; set; }
        public string RouteId { get; set; }
        public DateTime RouteDate{ get; set; }

        public FieldValuePairs ToFvps()
        {
            var rv = new FieldValuePairs();
            rv.Add(new FieldValuePair("publicRouteId", PublicRouteId.ToString()));
            rv.Add(new FieldValuePair("RouteId", RouteId));
            rv.Add(new FieldValuePair("RouteDate", RouteDate.ToShortDateString()));
            return rv;
        }

        public static RouteKey Parse(FieldValuePairs fvps)
        {
            var publicRouteId = fvps.GetInt("publicRouteId");
            var routeId = fvps.GetString("RouteId");
            var routeDate = fvps.GetDateTime("RouteDate");
            return new RouteKey(publicRouteId, routeId, routeDate);
        }

        public static RouteKey Fake()
        {
            return new RouteKey(10001, "1001", DateTime.Today);
        }
    }
}
