using System.Windows.Controls;

namespace QuickResponseBao.App.Controls;

public sealed class ScrollablePageLayout : ContentControl
{
    public ScrollViewer? ScrollViewer { get; private set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ScrollViewer = GetTemplateChild("PART_PageScrollViewer") as ScrollViewer;
    }
}
