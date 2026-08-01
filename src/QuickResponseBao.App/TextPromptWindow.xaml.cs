using System.Windows;
using QuickResponseBao.App.Services;

namespace QuickResponseBao.App;
public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string value = "") { InitializeComponent(); PromptText.Text = LocalizationService.Get("CategoryNamePrompt"); ValueText.Text = value; ValueText.SelectAll(); }
    public string Value => ValueText.Text.Trim();
    private void Save_Click(object sender, RoutedEventArgs e) { if (Value.Length == 0) return; DialogResult = true; }
}
