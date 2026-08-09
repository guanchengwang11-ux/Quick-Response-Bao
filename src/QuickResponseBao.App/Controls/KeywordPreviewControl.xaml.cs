using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace QuickResponseBao.App.Controls;

public partial class KeywordPreviewControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty KeywordsProperty = DependencyProperty.Register(
        nameof(Keywords), typeof(IEnumerable<string>), typeof(KeywordPreviewControl),
        new PropertyMetadata(null, (_, _) => { }));

    public IEnumerable<string>? Keywords
    {
        get => (IEnumerable<string>?)GetValue(KeywordsProperty);
        set => SetValue(KeywordsProperty, value);
    }

    public ObservableCollection<string> VisibleKeywords { get; } = [];

    public KeywordPreviewControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Refresh();
        Loaded += (_, _) => Refresh();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == KeywordsProperty) Refresh();
    }

    private void Refresh()
    {
        if (MoreBadge is null) return;
        var values = Keywords?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        VisibleKeywords.Clear();
        foreach (var keyword in values.Take(3)) VisibleKeywords.Add(keyword);
        var remaining = values.Count - VisibleKeywords.Count;
        MoreText.Text = $"+{remaining}";
        MoreBadge.Visibility = remaining > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
