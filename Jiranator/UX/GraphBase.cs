using JiraOne;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;


namespace Jiranator
{
   public  class GraphBase
    {
        #region Public

        public GraphBase(Panel canvas)
        {
            try
            {
                _canvas = canvas;
                _canvas.Children.Clear();

                if (canvas.ActualWidth == 0)
                    return;
                _graphSize.X = canvas.ActualWidth;
                _graphSize.Y = Math.Min(canvas.ActualHeight, _graphSize.X * .75);

                DataArea = new Canvas();
                var margin = new Thickness(30, 5, 5, 20);
                DataArea.Margin = margin;
                DataArea.Width = _graphSize.X - (margin.Left + margin.Right);
                DataArea.Height = _graphSize.Y - (margin.Top + margin.Bottom);
                _dataSize.X = DataArea.ActualWidth;
                if (_dataSize.X == 0)
                    _dataSize.X = DataArea.Width;
                _dataSize.Y = DataArea.ActualHeight;
                if (_dataSize.Y == 0)
                    _dataSize.Y = DataArea.Height;
                DataArea.Background = UIUtils.GetBrush(ChartColorBackground);
                var rect = new RectangleGeometry();
                rect.Rect = new Rect(new Point(0, 0), _dataSize);
                DataArea.Clip = rect;
                _canvas.Children.Add(DataArea);
            }
            catch (Exception exc)
            {
                FileUtils.ErrorLog("Grapher", exc);
                throw;
            }
        }
        public void Border()
        {
            var rect = new Rectangle();
            rect.Stroke = UIUtils.GetBrush(ChartColorBorder);
            rect.StrokeThickness = 3;
            rect.Margin = new Thickness(-2);
            rect.Width = _dataSize.X + 4;
            rect.Height = _dataSize.Y + 4;

            Add(DataArea, rect);
        }

        #endregion

        #region Helper Methods

        internal void AddText(Point pt, string str, Color color)
        {
            var conPt = Convert(pt);
            var border = new Border();
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = UIUtils.GetBrush(color);
            border.Background = UIUtils.GetBrush(ChartColorTextBackground);
            border.Margin = new Thickness(conPt.X, conPt.Y, 0, 0);
            var text = new TextBlock();
            text.Text = str;
            text.Foreground = UIUtils.GetBrush(color);
            text.Margin = new Thickness(3, 1, 3, 1);
            border.Child = text;
            Add(DataArea, border);
        }

        internal void Add(Panel panel, FrameworkElement child)
        {
            panel.Children.Add(child);
        }

        public void FillPolygon(List<Point> polyPoints, Color color)
        {
            var poly = new Polygon();
            poly.Fill = UIUtils.GetBrush(color);
            poly.Opacity = .75;
            foreach (var pt in polyPoints.Select(p => Convert(p))) 
                poly.Points.Add(pt);
            Add(DataArea, poly);
        }

        /// <summary>
        /// Takes points in pct
        /// </summary>
        /// <param name="pt1"></param>
        /// <param name="pt2"></param>
        /// <param name="color"></param>
        public void AddLine(Point pt1, Point pt2, Color color, int thickness, bool subtle = false)
        {
            var line = new Line();
            line.Stroke = UIUtils.GetBrush(color);
            if (subtle)
            {
                line.StrokeDashArray = new DoubleCollection() { 2 };
                line.Opacity = .25;
            }
            line.StrokeThickness = thickness;
            line.X1 = Convert(pt1).X;
            line.Y1 = Convert(pt1).Y;
            line.X2 = Convert(pt2).X;
            line.Y2 = Convert(pt2).Y;
            Add(DataArea, line);
        }
        public Point Convert(Point pt)
        {
            var rv = new Point();
            rv.X = pt.X * _dataSize.X;
            if (Downhill)
                rv.Y = pt.Y * _dataSize.Y;
            else
                rv.Y = (1 - pt.Y) * _dataSize.Y;
            return rv;
        }
        public void AddDot(Point pt, Color color, int diameter, string tooltip)
        {
            var dot = new Ellipse();
            dot.ToolTip = tooltip;
            dot.Stroke = UIUtils.GetBrush(color);
            dot.StrokeThickness = diameter;
            var conPt = Convert(pt);
            dot.Margin = new Thickness(conPt.X - diameter / 2.0, conPt.Y - diameter / 2.0, 0, 0);
            dot.Width = diameter;
            dot.Height = diameter;

            Add(DataArea, dot);
        }

        public void AddTriangle(Point pt, Color color, int diameter, string tooltip)
        {
            var dot = new Polygon();
            var pts = new Point[] { new Point(0, 0), new Point(diameter / 2, diameter), new Point(diameter, 0) };
            var myPointCollection = new PointCollection();
            foreach (var ipt in pts)
                myPointCollection.Add(ipt);
            myPointCollection.Add(pts[0]);
            dot.Points = myPointCollection;

            AddShape(dot, pt, diameter, color, tooltip);
        }
        public void AddStar(Point pt, Color color, int diameter, string tooltip)
        {
            var pts = new Point[] { new Point(.5, 0), new Point(.67, .3), new Point(1, .3), new Point(.83, .5), new Point(1, .7), new Point(.67, .7), new Point(.5, 1), new Point(.33, .7), new Point(0, .7), new Point(.17, .5), new Point(0, .3), new Point(.33, .3) };
            AddPolygon(ref pt, ref color, diameter, tooltip, pts);
        }

        public void AddDiamond(Point pt, Color color, int diameter, string tooltip)
        {
            var factor = .125;
            var pts = new Point[] { new Point(.5, 0), new Point(.5 + factor, .5 - factor), new Point(1, .5), new Point(.5 + factor, .5 + factor), new Point(.5, 1), new Point(.5 - factor,  .5 + factor), new Point(0, .5), new Point(.5 - factor, .5 - factor) };
            AddPolygon(ref pt, ref color, diameter, tooltip, pts);
        }

        public void AddSquare(Point pt, Color color, int diameter, string tooltip)
        {
            var pts = new Point[] { new Point(0, 0), new Point(1,0), new Point(1, 1), new Point(0, 1) };
            AddPolygon(ref pt, ref color, diameter, tooltip, pts);
        }

        private void AddPolygon(ref Point pt, ref Color color, int diameter, string tooltip, Point[] pts)
        {
            var dot = new Polygon();

            var myPointCollection = new PointCollection();
            foreach (var ipt in pts)
            {
                var sizedPoint = new Point(ipt.X * diameter, ipt.Y * diameter);
                myPointCollection.Add(sizedPoint);
            }
            dot.Points = myPointCollection;

            AddShape(dot, pt, diameter, color, tooltip);
        }

        private void AddShape(Shape shape, Point pt, int diamter, Color color, string tooltip)
        {

            shape.ToolTip = tooltip;
            shape.Stroke = UIUtils.GetBrush(Colors.Black);
            shape.StrokeThickness = 1;
            shape.Fill = UIUtils.GetBrush(color);
            var conPt = Convert(pt);
            shape.Margin = new Thickness(conPt.X - diamter / 2.0, conPt.Y - diamter / 2.0, 0, 0);

            Add(DataArea, shape);

        }

        public void AddCircle(Point pt, Color color, int diameter, string tooltip)
        {
            var dot = new Ellipse();
            dot.Width = diameter;
            dot.Height = diameter;

            AddShape(dot, pt, diameter, color, tooltip);
        }

        internal void AddChartLabel(Point pt, string str, Orientation dir, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center, VerticalAlignment verticalAlignment = VerticalAlignment.Center, Color? color = null)
        {
            var conPt = Convert(pt);
            var text = new TextBlock();
            text.Text = str;
            
            text.Foreground = UIUtils.GetBrush(color??ChartColorLabel);
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (dir == Orientation.Horizontal)
            {
                conPt.X += DataArea.Margin.Left;
                if (horizontalAlignment == HorizontalAlignment.Center)
                    conPt.X -= (text.DesiredSize.Width / 2.0);
                else if (horizontalAlignment == HorizontalAlignment.Left)
                    conPt.X -= text.DesiredSize.Width;
                else if (horizontalAlignment == HorizontalAlignment.Right)
                    conPt.X += 0;
                conPt.Y += 10;
            }
            else
            {
                conPt = Convert(pt);
                if (verticalAlignment == VerticalAlignment.Top)
                    conPt.Y -= text.DesiredSize.Height / 2.0;
                else if (verticalAlignment == VerticalAlignment.Bottom)
                    conPt.Y += text.DesiredSize.Height / 2.0;
                conPt.X = DataArea.Margin.Left - text.DesiredSize.Width;
            }
            text.Margin = new Thickness(conPt.X, conPt.Y, 0, 0);
            Add(_canvas, text);
        }

        #endregion

        #region Properties

        public Color ChartColorBackground
        {
            get
            {
                return Colors.GhostWhite;
            }
        }

        public Color ChartColorLabel
        {
            get
            {
                return Colors.Black;
            }
        }

        public Color ChartColorBorder
        {
            get
            {
                return Colors.DarkGray;
            }
        }
        public Color ChartColorTextBackground
        {
            get
            {
                return Colors.GhostWhite;
            }
        }
        internal Point _graphSize;
        internal Panel _canvas;
        public Panel DataArea { get; private set; }
        internal Point _dataSize;

        public bool Downhill { get; set; }

        #endregion

    }
}
