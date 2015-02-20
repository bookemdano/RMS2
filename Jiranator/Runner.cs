using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Jiranator
{
    public class Runner
    {
        public Runner(string name, int time, int age)
        {
            Name = name;
            Time = time;
            Age = age;
        }
        public string Name { get; set; }
        public int Time { get; set; }
        public int Age { get; set; }
    }
    public class Race
    {
        static public void QuickTest()
        {
            var race = new Race();
            race.RaceName = "The Big Race";
            race.Runners = new List<Runner>();
            race.Runners.Add(new Runner("Steve", 12, 33));
            race.Runners.Add(new Runner("Tim", 34, 28));
            race.Runners.Add(new Runner("Mark", 22, 37));
            race.Runners.Add(new Runner("Tom", 21, 30));
            race.Runners.Add(new Runner("Cliff", 13, 33));
            race.Runners.Add(new Runner("Vini", 17, 28));
            race.Runners.Add(new Runner("Matt", 10, 28));
            race.Runners.Add(new Runner("Ben", 9, 29));
            race.Runners.Add(new Runner("Brandon", 15, 14));
            var str = JsonConvert.SerializeObject(race);

            using (FileStream fs = File.Open(@"f:\temp\newhire.json", FileMode.OpenOrCreate))
            using (StreamWriter sw = new StreamWriter(fs))
            using (JsonWriter jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Newtonsoft.Json.Formatting.Indented;

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(jw, race);
            }

            str = @"{'?xml': {'@version': '1.0', '@standalone': 'no' },'root':" + str + "}";
            var doc = (XmlDocument)JsonConvert.DeserializeXmlNode(str);
            doc.Save(@"f:\temp\newhire.xml");
        }
        public string RaceName { get; set; }
        public List<Runner> Runners { get; set; }
    }
}
