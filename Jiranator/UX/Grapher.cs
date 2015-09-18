using JiraShare;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Jiranator
{
    public class Grapher : GraphBase
    {
        public Grapher(Panel canvas) : base(canvas)
        {

        }

        internal void DrawEtaLine(Point ptPrev, double pct, Color color)
        {
            AddLine(ptPrev, new Point(pct, 1), color, 2, true);
        }

        internal void DrawEtaText(double pct, DateTimeOffset eta, Color color)
        {
            var offset = .025;
            if (Downhill)
                offset = .1;    // we need more room at the bottom

            if (pct <= 0.0)
                return;
            if (pct < .9)
                AddText(new Point(pct, 1 - offset), eta.RelativeDate(), color);
            else if (pct < 1.1)
                AddText(new Point(.9, 1 - offset), eta.RelativeDate(), color);
            else
                AddText(new Point(.9, (1.0 / pct)), eta.RelativeDate(), color);
        }
    }
}
