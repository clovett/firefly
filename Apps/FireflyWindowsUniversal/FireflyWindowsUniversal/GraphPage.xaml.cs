using FireflyWindows.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FireflyWindows
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GraphPage : Page
    {
        public GraphPage()
        {
            this.InitializeComponent();
            InkDrawingAttributes inkDrawingAttributes = new InkDrawingAttributes();
            inkDrawingAttributes.Color = Windows.UI.Colors.Blue;
            GraphCanvas.InkPresenter.UpdateDefaultDrawingAttributes(inkDrawingAttributes);
            GraphCanvas.InkPresenter.InputDeviceTypes = Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen |
                Windows.UI.Core.CoreInputDeviceTypes.Touch;

            GraphCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
        }

        class Cube
        {
            public Rectangle Shape { get; set; }
            public double Score { get; set; }
            public int Column { get; set; }
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (args.Strokes.Count > 0)
            {
                HubManager hubs = ((App)App.Current).Hubs;
                double goal = 100; // shots

                Backdrop.Children.Clear();
                InkStroke newStroke = args.Strokes[0];
                
                List<Point> pts = new List<Point>();
                foreach (var point in newStroke.GetInkPoints())
                {
                    Point pt = point.Position;
                    pts.Add(pt);
                }
                Rect box = GetBoundingBox(pts);
                Debug.WriteLine(box.ToString());
                double area = 0;
                double x = box.Left;
                double bottom = box.Bottom;
                foreach (var pt in pts)
                {
                    double dx = pt.X - x;
                    area += (bottom - pt.Y) * dx;
                    x = pt.X;
                }
                Debug.WriteLine("area=" + area);

                // No approximate the area under the curve using the 100 shots.
                double cubeSize = Math.Sqrt(area / goal);
                int count = 0;
                SolidColorBrush cubeColor = new SolidColorBrush(Colors.DarkSeaGreen);
                SolidColorBrush cubeOutline = new SolidColorBrush(Colors.Transparent);
                List<Cube> cubes = new List<Cube>();
                double left = box.Left;
                double w = Math.Ceiling(box.Width / cubeSize);
                if (double.IsInfinity(w) || double.IsNaN(w))
                {
                    return;
                }
                var program = new List<int>();

                for (x = left; x < box.Right; x += cubeSize)
                {
                    // get minY in this slice
                    double minY = double.MaxValue;
                    double x2 = x;
                    int start = -1;
                    int end = 0;
                    for (end = 0; end < pts.Count; end++)
                    {
                        var pt = pts[end];
                        if (pt.X > x + cubeSize)
                        {
                            break;
                        }
                        if (pt.X >= x)
                        {
                            if (start == -1) start = end;
                            minY = Math.Min(minY, pt.Y);
                        }
                    }

                    // ok, place the cubes
                    int v = (int)Math.Ceiling((bottom - minY) / cubeSize);
                    program.Add(v);
                    for (int i = 0; i < v; i++)
                    {
                        var r = new Rectangle()
                        {
                            Width = cubeSize,
                            Height = cubeSize,
                            Fill = cubeColor,
                            StrokeThickness = 1,
                            Stroke = cubeOutline
                        };
                        double top = bottom - (i * cubeSize);
                        Cube c = new Cube()
                        {
                            Shape = r,
                            Column = program.Count - 1
                        };
                        cubes.Add(c);
                        Canvas.SetLeft(r, x);
                        Canvas.SetTop(r, top);
                        Backdrop.Children.Add(r);
                        count++;
                        if (v > 0)
                        {
                            // add it to our sorted list of cubes (sorted by distance above the line.
                            double sliceArea = 0;
                            for (int k = start; k < end; k++)
                            {
                                var pt = pts[k];
                                double dx = (pt.X - x2);
                                double distance = pt.Y - top;
                                sliceArea += distance * dx;
                            }
                            c.Score = sliceArea;
                        }
                    }
                }

                Debug.WriteLine("Placed {0} cubes", count);

                cubes.Sort(new Comparison<Cube>((a, b) =>
                {
                    return (int)(b.Score - a.Score);
                }));
                while (count > goal && cubes.Count > 0)
                {
                    // remove the cubes that are sitting the furthest above the line, but never take the bottom row.
                    Cube toRemove = cubes[0];
                    cubes.RemoveAt(0);
                    Backdrop.Children.Remove(toRemove.Shape);
                    program[toRemove.Column] = program[toRemove.Column] - 1;
                    count--;
                }

                hubs.SetProgram(program);
                Debug.WriteLine("Reach target {0} cubes", count);
            }
        }

        private Rect GetBoundingBox(List<Point> pts)
        {
            double minx = double.MaxValue;
            double miny = double.MaxValue;
            double maxx = double.MinValue;
            double maxy = double.MinValue;
            foreach (var pt in pts)
            {
                minx = Math.Min(minx, pt.X);
                maxx = Math.Max(maxx, pt.X);
                miny = Math.Min(miny, pt.Y);
                maxy = Math.Max(maxy, pt.Y);
            }
            return new Rect(minx, miny, maxx - minx, maxy - miny);
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }
    }
}
