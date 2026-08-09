using System.Windows;

namespace QuickResponseBao.App.Views;

public partial class DesignSystemPreviewWindow : Window
{
    public DesignSystemPreviewWindow() => InitializeComponent();

    public void FocusDemoControl() => FocusDemoButton.Focus();
}
