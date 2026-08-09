using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace QuickResponseBao.App.Controls;

public partial class ColumnFilterPanel : UserControl
{
    private ResponseLibraryFilterState? _state;
    private ResponseFilterField _field;
    private readonly ObservableCollection<FilterValueOption> _values = [];
    private ICollectionView? _valuesView;
    private int _unfilteredMatchCount;
    public event EventHandler? Applied;
    public event EventHandler? CloseRequested;
    public event EventHandler<ColumnSortRequestedEventArgs>? SortRequested;

    public ColumnFilterPanel() { InitializeComponent(); ValuesList.ItemsSource = _values; NumberOperatorBox.SelectionChanged += NumberOperatorChanged; PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape) { CloseRequested?.Invoke(this, EventArgs.Empty); args.Handled = true; } }; }

    public void BeginConfigure(ResponseFilterField field)
    {
        _field = field;
        TitleText.Text = $"{LocalizationService.Get("FilterBy")} {LocalizationService.Get(FieldKey(field))}";
        ContentPanel.Visibility = Visibility.Hidden;
        LoadingPanel.Visibility = Visibility.Visible;
    }

    public void Configure(ResponseFilterField field, ResponseLibraryFilterState state, IReadOnlyList<FacetValue>? availableValues = null, int matchCount = 0)
    {
        _field = field; _state = state; _unfilteredMatchCount = matchCount; TitleText.Text = $"{LocalizationService.Get("FilterBy")} {LocalizationService.Get(FieldKey(field))}";
        LoadingPanel.Visibility = Visibility.Collapsed; ContentPanel.Visibility = Visibility.Visible;
        TextPanel.Visibility = MultiPanel.Visibility = NumberPanel.Visibility = DatePanel.Visibility = Visibility.Collapsed;
        if (field is ResponseFilterField.Summary or ResponseFilterField.Keywords) ConfigureText();
        else if (field is ResponseFilterField.Category or ResponseFilterField.Language or ResponseFilterField.Status) ConfigureValues(availableValues ?? []);
        else if (field == ResponseFilterField.UsageCount) ConfigureNumber(); else ConfigureDate();
        UpdateSelectionSummary();
    }

    private void ConfigureText()
    {
        TextPanel.Visibility = Visibility.Visible;
        TextOperatorBox.ItemsSource = Enum.GetValues<TextFilterOperator>().Select(x => new OperatorOption<TextFilterOperator>(LocalizationService.Get(x.ToString()), x));
        var filter = _field == ResponseFilterField.Summary ? _state!.Summary : _state!.Keywords;
        TextOperatorBox.SelectedItem = TextOperatorBox.Items.Cast<OperatorOption<TextFilterOperator>>().First(x => x.Value == (filter?.Operator ?? TextFilterOperator.Contains)); TextValueBox.Text = filter?.Value ?? string.Empty; TextValueBox.Focus();
    }
    private void ConfigureValues(IEnumerable<FacetValue> available)
    {
        MultiPanel.Visibility = Visibility.Visible; _values.Clear();
        var selected = _field switch { ResponseFilterField.Category => _state!.Categories, ResponseFilterField.Language => _state!.Languages, _ => new HashSet<string>(_state!.Statuses.Select(x => x ? "enabled" : "disabled"), StringComparer.OrdinalIgnoreCase) };
        foreach (var value in available) _values.Add(new FilterValueOption(DisplayValue(value.Value), value.Value, value.Count, selected.Count == 0 || selected.Contains(value.Value)));
        _valuesView = CollectionViewSource.GetDefaultView(_values); ValueSearchBox.Text = string.Empty; ApplyValueSearch();
    }
    private void ConfigureNumber()
    {
        NumberPanel.Visibility = Visibility.Visible; NumberOperatorBox.ItemsSource = Enum.GetValues<NumberFilterOperator>().Select(x => new OperatorOption<NumberFilterOperator>(LocalizationService.Get(x.ToString()), x));
        var filter = _state!.UsageCount; NumberOperatorBox.SelectedItem = NumberOperatorBox.Items.Cast<OperatorOption<NumberFilterOperator>>().First(x => x.Value == (filter?.Operator ?? NumberFilterOperator.GreaterThan)); NumberValueBox.Text = filter?.Value.ToString() ?? string.Empty; NumberSecondValueBox.Text = filter?.SecondValue?.ToString() ?? string.Empty; RenderSecondNumber();
    }
    private void ConfigureDate()
    {
        DatePanel.Visibility = Visibility.Visible; DatePeriodBox.ItemsSource = Enum.GetValues<LastUsedFilterPeriod>().Select(x => new OperatorOption<LastUsedFilterPeriod>(LocalizationService.Get(x.ToString()), x));
        var filter = _state!.LastUsed; DatePeriodBox.SelectedItem = DatePeriodBox.Items.Cast<OperatorOption<LastUsedFilterPeriod>>().First(x => x.Value == (filter?.Period ?? LastUsedFilterPeriod.All)); FromDatePicker.SelectedDate = filter?.From?.LocalDateTime; ToDatePicker.SelectedDate = filter?.To?.LocalDateTime; RenderDateRange();
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        if (_field is ResponseFilterField.Summary or ResponseFilterField.Keywords) { var value = TextValueBox.Text.Trim(); var op = (TextOperatorBox.SelectedItem as OperatorOption<TextFilterOperator>)?.Value ?? TextFilterOperator.Contains; var filter = string.IsNullOrEmpty(value) ? null : new TextColumnFilter(value, op); if (_field == ResponseFilterField.Summary) _state.Summary = filter; else _state.Keywords = filter; }
        else if (_field is ResponseFilterField.Category or ResponseFilterField.Language or ResponseFilterField.Status) ApplyValues();
        else if (_field == ResponseFilterField.UsageCount) { _state.UsageCount = int.TryParse(NumberValueBox.Text, out var first) ? new NumberColumnFilter((NumberOperatorBox.SelectedItem as OperatorOption<NumberFilterOperator>)?.Value ?? NumberFilterOperator.Equals, first, int.TryParse(NumberSecondValueBox.Text, out var second) ? second : null) : null; }
        else { var period = (DatePeriodBox.SelectedItem as OperatorOption<LastUsedFilterPeriod>)?.Value ?? LastUsedFilterPeriod.All; _state.LastUsed = period == LastUsedFilterPeriod.All ? null : new LastUsedColumnFilter(period, ToOffset(FromDatePicker.SelectedDate), ToOffset(ToDatePicker.SelectedDate)); }
        Applied?.Invoke(this, EventArgs.Empty); CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    private void ApplyValues()
    {
        var selected = _values.Where(x => x.IsSelected).Select(x => x.Value).ToList();
        if (selected.Count == _values.Count) selected.Clear();
        if (_field == ResponseFilterField.Category) Replace(_state!.Categories, selected); else if (_field == ResponseFilterField.Language) Replace(_state!.Languages, selected); else { _state!.Statuses.Clear(); foreach (var value in selected) _state.Statuses.Add(value.Equals("enabled", StringComparison.OrdinalIgnoreCase)); }
    }
    private void Clear_Click(object sender, RoutedEventArgs e) { _state?.Clear(_field); Applied?.Invoke(this, EventArgs.Empty); CloseRequested?.Invoke(this, EventArgs.Empty); }
    private void ValueSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyValueSearch();
    private void ApplyValueSearch() { if (_valuesView is not null) _valuesView.Filter = value => value is FilterValueOption option && option.Label.Contains(ValueSearchBox.Text, StringComparison.OrdinalIgnoreCase); }
    private void ValueCheck_Click(object sender, RoutedEventArgs e) => UpdateSelectionSummary();
    private void SelectAllCheck_Click(object sender, RoutedEventArgs e)
    {
        var selected = !_values.All(x => x.IsSelected);
        foreach (var item in _valuesView?.Cast<FilterValueOption>() ?? []) item.IsSelected = selected;
        UpdateSelectionSummary();
    }
    private void UpdateSelectionSummary()
    {
        var selected = _values.Count(x => x.IsSelected);
        SelectAllCheck.IsChecked = _values.Count == 0 || selected == 0 ? false : selected == _values.Count ? true : null;
        SelectionSummaryText.Text = string.Format(LocalizationService.Get("FilterSelectedCount"), selected, _values.Count);
        var preview = MultiPanel.Visibility == Visibility.Visible ? _values.Where(x => x.IsSelected).Sum(x => x.Count) : _unfilteredMatchCount;
        PreviewCountText.Text = string.Format(LocalizationService.Get("FilterPreviewCount"), preview);
    }
    private void SortAscending_Click(object sender, RoutedEventArgs e) { SortRequested?.Invoke(this, new(_field, ListSortDirection.Ascending)); CloseRequested?.Invoke(this, EventArgs.Empty); }
    private void SortDescending_Click(object sender, RoutedEventArgs e) { SortRequested?.Invoke(this, new(_field, ListSortDirection.Descending)); CloseRequested?.Invoke(this, EventArgs.Empty); }
    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void NumberOperatorChanged(object sender, SelectionChangedEventArgs e) => RenderSecondNumber();
    private void DatePeriodBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RenderDateRange();
    private void RenderSecondNumber() => NumberSecondValueBox.Visibility = (NumberOperatorBox.SelectedItem as OperatorOption<NumberFilterOperator>)?.Value == NumberFilterOperator.Between ? Visibility.Visible : Visibility.Collapsed;
    private void RenderDateRange() => DateRangePanel.Visibility = (DatePeriodBox.SelectedItem as OperatorOption<LastUsedFilterPeriod>)?.Value == LastUsedFilterPeriod.CustomRange ? Visibility.Visible : Visibility.Collapsed;
    private string DisplayValue(string value) => _field == ResponseFilterField.Status ? LocalizationService.Get(value.Equals("enabled", StringComparison.OrdinalIgnoreCase) ? "Enabled" : "Disabled") : value;
    private static string FieldKey(ResponseFilterField field) => field switch { ResponseFilterField.UsageCount => "UsageCount", ResponseFilterField.LastUsed => "LastUsed", _ => field.ToString() };
    private static DateTimeOffset? ToOffset(DateTime? value) => value is null ? null : new DateTimeOffset(value.Value);
    private static void Replace(HashSet<string> target, IEnumerable<string> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
public sealed record OperatorOption<T>(string Label, T Value);
public sealed record ColumnSortRequestedEventArgs(ResponseFilterField Field, ListSortDirection Direction);
public sealed class FilterValueOption(string label, string value, int count, bool selected) : INotifyPropertyChanged
{
    private bool _selected = selected; public string Label { get; } = label; public string Value { get; } = value; public int Count { get; } = count;
    public bool IsSelected { get => _selected; set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
