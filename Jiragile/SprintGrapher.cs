using JiraOne;
using JiraShare;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JiraOne
{
    public class SprintGrapher : Grapher
    {
        public SprintGrapher(Panel canvas, SprintStats sprint, bool chartStoryPoints, bool chartTasks) : base(canvas)
        {
            if (DataArea == null)
                return;
            try
            {
                AddGridAndLabels(sprint, chartStoryPoints, chartTasks);
                // projection line
                AddLine(new Point(0, 0), new Point(1, 1), Colors.Goldenrod, 2);
                //if (!adventure.IsDone)
                //    DrawEtaLines(adventure);

                bool onlyOne = chartTasks ^ chartStoryPoints;
                if (chartTasks)
                    DrawStuff(ConvertToPtSets(sprint, false), onlyOne, Colors.Green);

                if (chartStoryPoints)
                    DrawStuff(ConvertToPtSets(sprint, true), onlyOne, Colors.Blue);

                    //var ptNow = new Point(sprint.DayPct(DateTimeOffset.Now), ptPrev.Y);
                    //AddDot(ptNow, Colors.DarkOliveGreen);

                Border();
            }
            catch (Exception exc)
            {
                FileUtils.ErrorLog("SprintGrapher", exc);
            }

        }

        private Color ColorPart(Color rgb, double pct)
        {
            var start = Colors.Green;
            var end = Colors.Blue;
            var diffG = start.G - end.G;

            return
                Color.FromArgb(
                    (byte)(.25 * 255),
                    (byte)(start.R + ((end.R - start.R) * pct)),
                    (byte)(start.G + ((end.G - start.G) * pct)),
                    (byte)(start.B + ((end.B - start.B) * pct)));

            //return Color.FromArgb((byte) (pct * 255), (byte)(rgb.R * 1), (byte)(rgb.G * 1), (byte)(rgb.B * 1));
        }
        private Color ColorPart(int i, double pctAlpha = .5)
        {
            var colorSet = new List<Color> { Colors.Cyan, Colors.Violet, Colors.Green, Colors.BurlyWood, Colors.IndianRed, Colors.Orchid, Colors.Sienna };
            return Color.FromArgb((byte) (pctAlpha * 255), colorSet[i].R, colorSet[i].G, colorSet[i].B);
        }
        private void DrawStuff(PointSets ptSets, bool onlyOne, Color rgb)
        {
            if (onlyOne)
            {
                int i = 0;
                FillTo("Total", ptSets.Total, ColorPart(i++), null);
                FillTo("InProgress", ptSets.InProgress, ColorPart(i++), null);
                FillTo("OnHold", ptSets.OnHold, ColorPart(i++), null);
                FillTo("Testing", ptSets.Testing, ColorPart(i++), null);
                FillTo("Resolved", ptSets.Resolved, ColorPart(i), null);
            }
            else
            {
                DrawPointedLine(ptSets.Total, ColorPart(rgb, .1), null);
                DrawPointedLine(ptSets.Resolved, ColorPart(rgb, .75), ColorPart(rgb, 1));
            }
        }

        public class PointSets
        {
            public List<Point> Total { get; set; } = new List<Point>();
            public List<Point> Testing { get; set; } = new List<Point>();
            public List<Point> Resolved { get; set; } = new List<Point>();
            public List<Point> InProgress { get; set; } = new List<Point>();
            public List<Point> OnHold { get; set; } = new List<Point>();
        }

        private PointSets ConvertToPtSets(SprintStats sprint, bool storyPoints )
        {
            var rv = new PointSets();
            var min = storyPoints ? (double)sprint.MinStoryPointCount : (double)sprint.MinTaskCount;
            var max = storyPoints ? (double)sprint.MaxStoryPointCount : (double)sprint.MaxTaskCount;
            var range = max - min;
            foreach (var stat in sprint.Stats.OrderBy(s => s.Timestamp))
            {
                var pctX = sprint.DayPct(stat.Timestamp);
                var counts = storyPoints ? stat.StoryPointCounts : stat.TaskCounts;

                var val = counts.Resolved;
                rv.Resolved.Add(new Point(pctX, (val - min) / range));
                val += counts.Testing;
                rv.Testing.Add(new Point(pctX, (val - min) / range));
                val += counts.OnHold;
                rv.OnHold.Add(new Point(pctX, (val - min) / range));
                val += counts.InProgress;
                rv.InProgress.Add(new Point(pctX, (val - min) / range));

                rv.Total.Add(new Point(pctX, (counts.Total - min) / range));
            }
            return rv;
        }

        private void FillTo(string name, List<Point> pts, Color color, Color? pointColor)
        {
            var polyPts = new List<Point>();
            polyPts.Add(new Point(0, 0));
            polyPts.AddRange(pts);
            polyPts.Add(new Point(pts.Last().X, 0));
            FillPolygon(polyPts, color);
            if (!string.IsNullOrWhiteSpace(name))
                AddText(pts.Last(), name, Colors.Black);

            if (pointColor != null)
                foreach (var pt in pts)
                    AddDot(pt, pointColor.Value, 5, null);
        }

        private Point DrawPointedLine(List<Point> pts, Color lineColor, Color? pointColor)
        {
            var ptPrev = new Point(0, 0);
            foreach (var pt in pts)
            {
                AddLine(ptPrev, pt, lineColor, 2);
                ptPrev = pt;
            }
            if (pointColor != null)
                foreach (var pt in pts)
                    AddDot(pt, pointColor.Value, 5, null);
            return ptPrev;
        }

        internal void AddGridAndLabels(SprintStats sprint, bool chartStoryPoints, bool chartTasks)
        {
            int bottom = 0;
            if (Downhill)
                bottom = 1;
            if (chartTasks)
            {
                AddChartLabel(new Point(0, 0), sprint.MinTaskCount.ToString("N0"), Orientation.Vertical, HorizontalAlignment.Center, VerticalAlignment.Top);
                AddChartLabel(new Point(0, 1), sprint.MaxTaskCount.ToString("N0"), Orientation.Vertical, HorizontalAlignment.Center, VerticalAlignment.Bottom);
            }
            var taskCountRange = sprint.MaxTaskCount - sprint.MinTaskCount;

            for (var d = .25; d < 1; d += .25)
            {
                // rows
                if (chartTasks)
                {
                    var yTasks = sprint.MinTaskCount + (d * taskCountRange);
                    AddChartLabel(new Point(0, d), yTasks.ToString("N0"), Orientation.Vertical, HorizontalAlignment.Center, VerticalAlignment.Top, Colors.DarkBlue);
                }
                if (chartStoryPoints)
                {
                    var yStoryPoints = 0 + (d * sprint.MaxStoryPointCount);
                    AddChartLabel(new Point(0, d), yStoryPoints.ToString("N0"), Orientation.Vertical, HorizontalAlignment.Center, VerticalAlignment.Bottom, Colors.DarkGreen);
                }

                AddLine(new Point(0, d), new Point(1, d), Colors.Gray, 1);

            }
            Rect lastRect = Rect.Empty;
            var rect = GetLabelRect(new Point(0, bottom), sprint.StartTime.RelativeDate(), Orientation.Horizontal, HorizontalAlignment.Center, VerticalAlignment.Center);
            lastRect = rect;
            AddChartLabel(new Point(rect.X, rect.Y), sprint.StartTime.RelativeDate());

            for (var dt = sprint.StartTime.AddDays(1); dt < sprint.TargetTime; dt = dt.AddDays(1))
            {
                var pct = (dt - sprint.StartTime).TotalDays / sprint.DayCount;
                AddLine(new Point(pct, 0), new Point(pct, 1), Colors.Gray, 1);

                rect = GetLabelRect(new Point(pct, bottom), dt.RelativeDate(), Orientation.Horizontal, HorizontalAlignment.Center, VerticalAlignment.Center);
                if (RectIntersect(rect, lastRect))
                    continue;
                lastRect = rect;
                AddChartLabel(new Point(rect.X, rect.Y), dt.RelativeDate());
            }
            rect = GetLabelRect(new Point(1, bottom), sprint.TargetTime.RelativeDate(), Orientation.Horizontal, HorizontalAlignment.Center, VerticalAlignment.Center);
            if (!RectIntersect(rect, lastRect))
                AddChartLabel(new Point(rect.X, rect.Y), sprint.StartTime.RelativeDate());
        }
        bool RectIntersect(Rect rect1, Rect rect2)
        {
            rect1.Intersect(rect2);
            return !rect1.IsEmpty;
        }
    }
}
