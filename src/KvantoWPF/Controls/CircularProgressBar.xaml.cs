using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace KvantoWPF.Controls;

public partial class CircularProgressBar : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(CircularProgressBar),
        new PropertyMetadata(0d, OnVisualPropertyChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush),
        typeof(Brush),
        typeof(CircularProgressBar),
        new PropertyMetadata(Brushes.Red));

    public static readonly DependencyProperty CenterTextProperty = DependencyProperty.Register(
        nameof(CenterText),
        typeof(string),
        typeof(CircularProgressBar),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption),
        typeof(string),
        typeof(CircularProgressBar),
        new PropertyMetadata(string.Empty));

    public CircularProgressBar()
    {
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public Geometry ProgressGeometry => CreateGeometry(Value);

    private static void OnVisualPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CircularProgressBar progressBar)
        {
            progressBar.PropertyChanged?.Invoke(progressBar, new PropertyChangedEventArgs(nameof(ProgressGeometry)));
        }
    }

    private static Geometry CreateGeometry(double value)
    {
        if (value <= 0)
        {
            return Geometry.Empty;
        }

        const double size = 170d;
        const double stroke = 12d;
        var radius = (size - stroke) / 2d;
        var center = new Point(size / 2d, size / 2d);
        var startPoint = new Point(center.X, center.Y - radius);
        var clampedValue = Math.Min(value, 99.999d);
        var angle = clampedValue / 100d * 360d;
        var radians = (Math.PI / 180d) * (angle - 90d);
        var endPoint = new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        var isLargeArc = angle >= 180d;

        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = isLargeArc
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
