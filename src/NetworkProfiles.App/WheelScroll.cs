using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NetworkProfiles.App;

/// <summary>
/// Scrolls in proportion to the wheel delta. WPF's ScrollViewer moves a fixed
/// three lines per wheel event whatever its size, so touchpads and smooth-scroll
/// mice, which send many small events, scroll far too fast.
/// </summary>
public static class WheelScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(WheelScroll), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;
        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((bool)e.NewValue)
            element.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = sender as ScrollViewer ?? FindScrollViewer((DependencyObject)sender);
        if (viewer is null || viewer.ScrollableHeight <= 0)
            return;

        // One standard notch (delta 120) moves the user's "lines per notch" setting, 16 px per line.
        var lines = SystemParameters.WheelScrollLines > 0 ? SystemParameters.WheelScrollLines : 3;
        var pixels = e.Delta / 120.0 * lines * 16;
        viewer.ScrollToVerticalOffset(Math.Clamp(viewer.VerticalOffset - pixels, 0, viewer.ScrollableHeight));
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                return viewer;
            if (FindScrollViewer(child) is { } nested)
                return nested;
        }
        return null;
    }
}
