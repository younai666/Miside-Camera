using static Miside.Camera.Desktop.Localization;
using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Miside.Camera.Desktop;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewModel model;
    private bool mayClose;
    private Button? pressedButton;
    public MainWindow(WorkspaceViewModel model)
    {
        this.model = model; DataContext = model; InitializeComponent();
        Loaded += async (_, _) => {
            if (!File.Exists(model.Settings.GamePath) || !HostLocator.IsSupported(model.Settings.GamePath)) model.Page = "settings";
            else if (model.Settings.AutoStart) await model.RunAsync(model.Manager.StartAsync);
        };
        Closing += ClosingAsync;
        AddHandler(Button.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(PressFeedback), true);
        AddHandler(Button.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(ReleaseFeedback), true);
        AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, _) => ReleasePressedButton()), true);
    }

    private static void AnimateButton(Button button, double scale)
    {
        if (!SystemParameters.ClientAreaAnimation || !button.IsEnabled || VisualTreeHelper.GetChildrenCount(button) == 0) return;
        if (VisualTreeHelper.GetChild(button, 0) is Border { RenderTransform: ScaleTransform transform } edge) {
            // WPF freezes Freezables in templates; animate an instance owned by this button.
            if (transform.IsFrozen) { transform = transform.Clone(); edge.RenderTransform = transform; }
            double from = transform.ScaleX;
            transform.ScaleX = transform.ScaleY = scale;
            var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(scale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)), new KeySpline(0.23, 1, 0.32, 1)));
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation); transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }
    private void PressFeedback(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? child = e.OriginalSource as DependencyObject;
        while (child != null && child is not Button) child = child is Visual ? VisualTreeHelper.GetParent(child) : (child as FrameworkContentElement)?.Parent;
        if (child is Button button) { pressedButton = button; AnimateButton(button, 0.97); }
    }
    private void ReleasePressedButton() { if (pressedButton == null) return; AnimateButton(pressedButton, 1); pressedButton = null; }
    private void ReleaseFeedback(object sender, MouseButtonEventArgs e) => ReleasePressedButton();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = T("FileFilter"), CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        switch ((sender as Button)?.Tag) {
            case "GamePath": model.Settings.GamePath = dialog.FileName; break;
        }
    }
    private async void ClosingAsync(object? sender, CancelEventArgs e)
    {
        if (mayClose) return;
        e.Cancel = true;
        if (model.Busy) return;
        await model.RunAsync(async () => { await model.Manager.DisposeAsync(); mayClose = true; });
        if (mayClose) Close();
    }
}
