using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Collections.Generic;
using Utils;

namespace Burr
{
    [TestClass]
    public class AttackJson
    {
        #region Json
        [TestMethod]
        public void ShouldWorkWithJiraResult()
        {
            var str = File.ReadAllText("MOB.json");
            File.WriteAllLines("MOBSplit.json", Json.SplitLinesDeep(str));
            var json = Json.Parse(str);
            Assert.IsNotNull(json);
        }

        [TestMethod]
        public void ShouldBeAbleToRoundTripSimpleJson()
        {
            var json = new Json();
            json.SetField("Name", "Demonic");
            json.SetField("Type", "DRV");
            Assert.AreEqual("Demonic", json.GetString("Name"));
            Assert.AreEqual("DRV", json.GetString("Type"));
            var str = json.ToJsonString();
            File.WriteAllText("test.json", str);

            var json2 = Json.Parse(str);
            Assert.AreEqual("Demonic", json2.GetString("Name"));
            Assert.AreEqual("DRV", json2.GetString("Type"));
        }
        [TestMethod]
        public void ShouldBeAbleToRoundTripJsonWithCommas()
        {
            var json = new Json();
            var name = "Demonic,Joe";
            var type = "DRV,SYL";
            json.SetField("Name", name);
            json.SetField("Type", type);
            Assert.AreEqual(name, json.GetString("Name"));
            Assert.AreEqual(type, json.GetString("Type"));
            var str = json.ToJsonString();
            File.WriteAllText("test.json", str);

            var json2 = Json.Parse(str);
            Assert.AreEqual(name, json2.GetString("Name"));
            Assert.AreEqual(type, json2.GetString("Type"));
        }

        [TestMethod]
        public void ShouldBeAbleToRoundTripSingleLineJson()
        {
            var json = new Json();
            var name = "Demonic,Joe";
            var type = "DRV,SYL";
            json.SetField("Name", name);
            json.SetField("Type", type);
            Assert.AreEqual(name, json.GetString("Name"));
            Assert.AreEqual(type, json.GetString("Type"));
            var str = json.ToJsonString().Replace(Environment.NewLine, "");
            File.WriteAllText("test.json", str);

            var json2 = Json.Parse(str);
            Assert.AreEqual(name, json2.GetString("Name"));
            Assert.AreEqual(type, json2.GetString("Type"));
        }

        [TestMethod]
        public void ShouldBeAbleToRoundTripTimesJson()
        {
            var json = new Json();
            var time = new DateTime(2008, 12, 10, 12, 24, 25);

            json.SetField("Time", time);
            Assert.AreEqual(time, json.GetDateTime("Time"));
            var str = json.ToJsonString();
            File.WriteAllText("test.json", str);

            var json2 = Json.Parse(str);
            Assert.AreEqual(time, json2.GetDateTime("Time"));
        }

        [TestMethod]
        public void ShouldBeAbleToRoundTripArrays()
        {
            var jsonEmptyArray = Json.Parse( "{\"allOfThese\":[]}");
            Assert.IsNull(jsonEmptyArray.GetArraySet("allOfThese"));

            var json = new Json();
            json.SetField("Name", "Demonic");
            json.SetField("Type", "DRV");
            var fields = new List<string>();
            fields.Add("RouteId");
            fields.Add("RouteDate");
            var values = new List<List<string>>();
            values.Add(new List<string>() { "1001", "2/17/2006" });
            values.Add(new List<string>() { "1002", "3/19/1979" });
            values.Add(new List<string>() { "1003", "12/10/2008" });
            json.SetArray("Routes", fields, values);
            Assert.IsNull(json.GetStringFromArray("Routes", 3, "RouteId"));
            Assert.AreEqual("1001", json.GetStringFromArray("Routes", 0, "RouteId"), "preparsed");
            Assert.AreEqual("2/17/2006", json.GetStringFromArray("Routes", 0, "RouteDate"), "preparsed");
            Assert.AreEqual("1002", json.GetStringFromArray("Routes", 1, "RouteId"), "preparsed");

            var str = json.ToJsonString();
            File.WriteAllText("test.json", str);

            var json2 = Json.Parse(str);
            Assert.IsNull(json2.GetStringFromArray("Routes", 3, "RouteId"));
            Assert.AreEqual("1001", json2.GetStringFromArray("Routes", 0, "RouteId"), "postparsed");
            Assert.AreEqual("2/17/2006", json2.GetStringFromArray("Routes", 0, "RouteDate"), "postparsed");
            Assert.AreEqual("1002", json2.GetStringFromArray("Routes", 1, "RouteId"), "postparsed");
        }

        #endregion
    }
}
