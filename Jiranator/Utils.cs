using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace JiraOne
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

        internal static void WriteAllBytes(string name, byte[] compressed)
        {
            var filename = Path.Combine(Dir, name);
            File.WriteAllBytes(filename, compressed);
        }

        internal static void WriteAllText(string name, string str)
        {
            var filename = Path.Combine(Dir, name);
            File.WriteAllText(filename, str);
        }

        internal static async Task<string[]> GetFiles(string mask)
        {
            return Directory.GetFiles(Dir, mask);
        }

        internal static async Task<byte[]> ReadAllBytes(string filename)
        {
            return File.ReadAllBytes(filename);
        }

        internal static void Delete(string filename)
        {
            File.Delete(filename);
        }
    }
}
