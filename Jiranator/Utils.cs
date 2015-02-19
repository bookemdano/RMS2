using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Jiranator
{
    public static class UIUtils
    {
        static Dictionary<Color, Brush> _brushes = new Dictionary<Color, Brush>();
        static public Brush GetBrush(Color color)
        {
            if (!_brushes.ContainsKey(color))
                _brushes.Add(color, new SolidColorBrush(color));

            return _brushes[color];
        }
    }
    public static class FileUtils
    {
        static string _dataPath;
        internal static string GetDataPath()
        {
            if (_dataPath == null)
            {
                var key = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SkyDrive", "UserFolder", null);
                if (key == null)
                    _dataPath = Path.Combine(@"c:\onedrive", "data");
                else
                    _dataPath = Path.Combine(key.ToString(), "data");
            }
            return _dataPath;
        }

        internal static void ErrorLog(string str, Exception exc)
        {
            Log(str + " " + exc.ToString());
        }
        internal static void Log(string str)
        {
            File.AppendAllText("endless.log", DateTimeOffset.Now + str + Environment.NewLine);
        }
        internal static void Log(string str, Stopwatch sw)
        {
            str += " " + sw?.Elapsed.TotalMilliseconds.ToString("0") + "ms";
            sw?.Restart();

            FileUtils.Log(str);
        }
    }
    public static class StringUtils
    {
        internal static string GetStringBetween(string line, string strStart, string strEnd)
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
        public static string ArrayToString(List<string> array)
        {
            if (array == null)
                return "-";
            return string.Join("|", array.OrderBy(a => a).ToArray());
        }
    }
    static public class TimeUtils
    {
        public static string RelativeDate(this DateTimeOffset dt)
        {
            var now = DateTimeOffset.UtcNow;
            var days = (dt - now).TotalDays;
            string rv;
            if (Math.Abs(days) < 30)
                rv = dt.ToString(@"ddd M/d");
            else if (now.Year == dt.Year)
                rv = dt.ToString(@"M/d");
            else
                rv = dt.ToString(@"M/d/yy");

            return rv;
        }
    }
}
