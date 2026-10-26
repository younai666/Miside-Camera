using System.Windows;
using System.Windows.Controls;

namespace Miside.Camera.Desktop;

/// <summary>Aligned read-only properties shared by the workspace and inspectors.</summary>
public sealed class InfoRow : Control
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(InfoRow), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(InfoRow), new PropertyMetadata("—"));
    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(nameof(LabelWidth), typeof(GridLength), typeof(InfoRow), new PropertyMetadata(new GridLength(136)));
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public GridLength LabelWidth { get => (GridLength)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
}
