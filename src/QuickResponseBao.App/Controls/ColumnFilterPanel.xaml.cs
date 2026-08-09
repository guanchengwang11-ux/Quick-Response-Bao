using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace QuickResponseBao.App.Controls;

public partial class ColumnFilterPanel : UserControl
{
    private ResponseLibraryFilterState? _committedState;
    private ResponseLibraryFilterState? _draftState;
    private ResponseFilterField _field;
    private readonly ObservableCollection<FilterValueOption> _values = [];
    private ICollectionView? _valuesView;
    private int _unfilteredMatchCount;
    private int _previewVersion;
    private bool _configuring;
    private CancellationTokenSource? _previewCancellation;

    public event EventHandler<FilterAppliedEventArgs>? Applied;
    public event EventHandler? CloseRequested;
    public event EventHandler<ColumnSortRequestedEventArgs>? SortRequested;
    public Func<ResponseLibraryFilterState, CancellationToken, Task<int>>? PreviewCountProvider { get; set; }
    public int LastPreviewCount { get; private set; }
    public ResponseFilterField CurrentField => _field;

    public ColumnFilterPanel()
    {
        InitializeComponent();
        ValuesList.ItemsSource = _values;
        TextOperatorBox.SelectionChanged += DraftInputChanged;
        TextValueBox.TextChanged += DraftInputChanged;
        NumberOperatorBox.SelectionChanged += NumberOperatorChanged;
        NumberValueBox.TextChanged += DraftInputChanged;
        NumberSecondValueBox.TextChanged += DraftInputChanged;
        FromDatePicker.SelectedDateChanged += DraftInputChanged;
        ToDatePicker.SelectedDateChanged += DraftInputChanged;
        PreviewKeyDown += HandlePreviewKeyDown;
        Unloaded += (_, _) => CancelPreview();
    }

    public void BeginConfigure(ResponseFilterField field)
    {
        CancelPreview();
        _field = field;
        TitleText.Text = $"{LocalizationService.Get("FilterBy")} {LocalizationService.Get(FieldKey(field))}";
        ContentPanel.Visibility = Visibility.Hidden;
        LoadingPanel.Visibility = Visibility.Visible;
    }

    public void Configure(ResponseFilterField field, ResponseLibraryFilterState state, IReadOnlyList<FacetValue>? availableValues = null, int matchCount = 0)
    {
        _configuring = true;
        _field = field;
        _committedState = state;
        _draftState = state.Clone();
        _unfilteredMatchCount = matchCount;
        TitleText.Text = $"{LocalizationService.Get("FilterBy")} {LocalizationService.Get(FieldKey(field))}";
        LoadingPanel.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;
        TextPanel.Visibility = MultiPanel.Visibility = NumberPanel.Visibility = DatePanel.Visibility = Visibility.Collapsed;
        if (field is ResponseFilterField.Summary or ResponseFilterField.Keywords) ConfigureText();
        else if (field is ResponseFilterField.Category or ResponseFilterField.Language or ResponseFilterField.Status) ConfigureValues(availableValues ?? []);
        else if (field == ResponseFilterField.UsageCount) ConfigureNumber();
        else ConfigureDate();
        _configuring = false;
        UpdateSelectionSummary();
        QueuePreview(false);
    }

    private void ConfigureText()
    {
        TextPanel.Visibility = Visibility.Visible;
        var operators = _field == ResponseFilterField.Keywords
            ? new[] { TextFilterOperator.Contains, TextFilterOperator.Equals }
            : Enum.GetValues<TextFilterOperator>();
        TextOperatorBox.ItemsSource = operators.Select(x => new OperatorOption<TextFilterOperator>(LocalizationService.Get(x.ToString()), x));
        var filter = _field == ResponseFilterField.Summary ? _draftState!.Summary : _draftState!.Keywords;
        TextOperatorBox.SelectedItem = TextOperatorBox.Items.Cast<OperatorOption<TextFilterOperator>>()
            .First(x => x.Value == (filter?.Operator ?? TextFilterOperator.Contains));
        TextValueBox.Text = filter?.Value ?? string.Empty;
        TextValueBox.Focus();
    }

    private void ConfigureValues(IEnumerable<FacetValue> available)
    {
        MultiPanel.Visibility = Visibility.Visible;
        _values.Clear();
        var selected = _field switch
        {
            ResponseFilterField.Category => _draftState!.Categories,
            ResponseFilterField.Language => _draftState!.Languages,
            _ => new HashSet<string>(_draftState!.Statuses.Select(x => x ? "enabled" : "disabled"), StringComparer.OrdinalIgnoreCase)
        };
        foreach (var value in available)
            _values.Add(new FilterValueOption(DisplayValue(value.Value), value.Value, value.Count, selected.Count == 0 || selected.Contains(value.Value)));
        _valuesView = CollectionViewSource.GetDefaultView(_values);
        ValueSearchBox.Text = string.Empty;
        ApplyValueSearch();
    }

    private void ConfigureNumber()
    {
        NumberPanel.Visibility = Visibility.Visible;
        NumberOperatorBox.ItemsSource = Enum.GetValues<NumberFilterOperator>().Select(x => new OperatorOption<NumberFilterOperator>(LocalizationService.Get(x.ToString()), x));
        var filter = _draftState!.UsageCount;
        NumberOperatorBox.SelectedItem = NumberOperatorBox.Items.Cast<OperatorOption<NumberFilterOperator>>()
            .First(x => x.Value == (filter?.Operator ?? NumberFilterOperator.GreaterThan));
        NumberValueBox.Text = filter?.Value.ToString() ?? string.Empty;
        NumberSecondValueBox.Text = filter?.SecondValue?.ToString() ?? string.Empty;
        RenderSecondNumber();
    }

    private void ConfigureDate()
    {
        DatePanel.Visibility = Visibility.Visible;
        DatePeriodBox.ItemsSource = Enum.GetValues<LastUsedFilterPeriod>().Select(x => new OperatorOption<LastUsedFilterPeriod>(LocalizationService.Get(x.ToString()), x));
        var filter = _draftState!.LastUsed;
        DatePeriodBox.SelectedItem = DatePeriodBox.Items.Cast<OperatorOption<LastUsedFilterPeriod>>()
            .First(x => x.Value == (filter?.Period ?? LastUsedFilterPeriod.All));
        FromDatePicker.SelectedDate = filter?.From?.LocalDateTime;
        ToDatePicker.SelectedDate = filter?.To?.LocalDateTime;
        RenderDateRange();
    }

    public void ApplyDraft()
    {
        if (_committedState is null || _draftState is null) return;
        UpdateDraftFromControls();
        _committedState.CopyFieldFrom(_draftState, _field);
        Applied?.Invoke(this, new FilterAppliedEventArgs(_field, FilterCommitAction.Apply));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ClearCurrentFilter()
    {
        if (_committedState is null) return;
        _committedState.Clear(_field);
        _draftState?.Clear(_field);
        Applied?.Invoke(this, new FilterAppliedEventArgs(_field, FilterCommitAction.Clear));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CancelDraft()
    {
        CancelPreview();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyPopupClosed() => CancelPreview();

    private void UpdateDraftFromControls()
    {
        if (_draftState is null) return;
        if (_field is ResponseFilterField.Summary or ResponseFilterField.Keywords)
        {
            var value = TextValueBox.Text.Trim();
            var op = (TextOperatorBox.SelectedItem as OperatorOption<TextFilterOperator>)?.Value ?? TextFilterOperator.Contains;
            var filter = string.IsNullOrEmpty(value) ? null : new TextColumnFilter(value, op);
            if (_field == ResponseFilterField.Summary) _draftState.Summary = filter; else _draftState.Keywords = filter;
        }
        else if (_field is ResponseFilterField.Category or ResponseFilterField.Language or ResponseFilterField.Status)
        {
            var selected = _values.Where(x => x.IsSelected).Select(x => x.Value).ToList();
            if (selected.Count == _values.Count) selected.Clear();
            if (_field == ResponseFilterField.Category) Replace(_draftState.Categories, selected);
            else if (_field == ResponseFilterField.Language) Replace(_draftState.Languages, selected);
            else { _draftState.Statuses.Clear(); foreach (var value in selected) _draftState.Statuses.Add(value.Equals("enabled", StringComparison.OrdinalIgnoreCase)); }
        }
        else if (_field == ResponseFilterField.UsageCount)
        {
            _draftState.UsageCount = int.TryParse(NumberValueBox.Text, out var first)
                ? new NumberColumnFilter((NumberOperatorBox.SelectedItem as OperatorOption<NumberFilterOperator>)?.Value ?? NumberFilterOperator.Equals,
                    first, int.TryParse(NumberSecondValueBox.Text, out var second) ? second : null)
                : null;
        }
        else
        {
            var period = (DatePeriodBox.SelectedItem as OperatorOption<LastUsedFilterPeriod>)?.Value ?? LastUsedFilterPeriod.All;
            _draftState.LastUsed = period == LastUsedFilterPeriod.All
                ? null
                : new LastUsedColumnFilter(period, ToOffset(FromDatePicker.SelectedDate), ToOffset(ToDatePicker.SelectedDate));
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => ApplyDraft();
    private void Clear_Click(object sender, RoutedEventArgs e) => ClearCurrentFilter();
    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelDraft();
    private void ValueSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyValueSearch();
    private void ApplyValueSearch() { if (_valuesView is not null) _valuesView.Filter = value => value is FilterValueOption option && option.Label.Contains(ValueSearchBox.Text, StringComparison.OrdinalIgnoreCase); }
    private void ValueCheck_Click(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(UpdateSelectionSummary, DispatcherPriority.Input);
    private void SelectAllCheck_Click(object sender, RoutedEventArgs e)
    {
        var selected = !_values.All(x => x.IsSelected);
        foreach (var item in _valuesView?.Cast<FilterValueOption>() ?? []) item.IsSelected = selected;
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var selected = _values.Count(x => x.IsSelected);
        SelectAllCheck.IsChecked = _values.Count == 0 ? false : selected == _values.Count ? true : selected == 0 ? false : (bool?)null;
        SelectionSummaryText.Text = string.Format(LocalizationService.Get("FilterSelectedCount"), selected, _values.Count);
        QueuePreview();
    }

    private void DraftInputChanged(object? sender, EventArgs e)
    {
        if (_configuring) return;
        if (sender == NumberOperatorBox) RenderSecondNumber();
        QueuePreview();
    }

    private void QueuePreview(bool debounce = true)
    {
        if (_configuring || _draftState is null) return;
        UpdateDraftFromControls();
        var snapshot = _draftState.Clone();
        var version = ++_previewVersion;
        CancelPreview();
        var cancellation = _previewCancellation = new CancellationTokenSource();
        _ = UpdatePreviewAsync(snapshot, version, debounce, cancellation.Token);
    }

    private async Task UpdatePreviewAsync(ResponseLibraryFilterState draft, int version, bool debounce, CancellationToken cancellationToken)
    {
        try
        {
            if (debounce) await Task.Delay(100, cancellationToken);
            var count = PreviewCountProvider is null ? _unfilteredMatchCount : await PreviewCountProvider(draft, cancellationToken);
            if (version != _previewVersion || cancellationToken.IsCancellationRequested) return;
            LastPreviewCount = count;
            PreviewCountText.Text = string.Format(LocalizationService.Get("FilterPreviewCount"), count);
        }
        catch (OperationCanceledException) { }
    }

    private void CancelPreview() { _previewCancellation?.Cancel(); _previewCancellation?.Dispose(); _previewCancellation = null; }
    private void HandlePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
    {
        if (args.Key == Key.Escape) { Cancel_Click(this, new RoutedEventArgs()); args.Handled = true; return; }
        if (args.Key == Key.Enter && !TextOperatorBox.IsDropDownOpen && !NumberOperatorBox.IsDropDownOpen && !DatePeriodBox.IsDropDownOpen)
        { ApplyDraft(); args.Handled = true; }
    }
    private void SortAscending_Click(object sender, RoutedEventArgs e) { SortRequested?.Invoke(this, new(_field, ListSortDirection.Ascending)); CloseRequested?.Invoke(this, EventArgs.Empty); }
    private void SortDescending_Click(object sender, RoutedEventArgs e) { SortRequested?.Invoke(this, new(_field, ListSortDirection.Descending)); CloseRequested?.Invoke(this, EventArgs.Empty); }
    private void NumberOperatorChanged(object sender, SelectionChangedEventArgs e) { RenderSecondNumber(); DraftInputChanged(sender, e); }
    private void DatePeriodBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { RenderDateRange(); DraftInputChanged(sender, e); }
    private void RenderSecondNumber() => NumberSecondValueBox.Visibility = (NumberOperatorBox.SelectedItem as OperatorOption<NumberFilterOperator>)?.Value == NumberFilterOperator.Between ? Visibility.Visible : Visibility.Collapsed;
    private void RenderDateRange() => DateRangePanel.Visibility = (DatePeriodBox.SelectedItem as OperatorOption<LastUsedFilterPeriod>)?.Value == LastUsedFilterPeriod.CustomRange ? Visibility.Visible : Visibility.Collapsed;
    private string DisplayValue(string value) => _field == ResponseFilterField.Status ? LocalizationService.Get(value.Equals("enabled", StringComparison.OrdinalIgnoreCase) ? "Enabled" : "Disabled") : value;
    private static string FieldKey(ResponseFilterField field) => field switch { ResponseFilterField.UsageCount => "UsageCount", ResponseFilterField.LastUsed => "LastUsed", _ => field.ToString() };
    private static DateTimeOffset? ToOffset(DateTime? value) => value is null ? null : new DateTimeOffset(value.Value);
    private static void Replace<T>(HashSet<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}

public enum FilterCommitAction { Apply, Clear }
public sealed record FilterAppliedEventArgs(ResponseFilterField Field, FilterCommitAction Action);
public sealed record OperatorOption<T>(string Label, T Value) { public override string ToString() => Label; }
public sealed record ColumnSortRequestedEventArgs(ResponseFilterField Field, ListSortDirection Direction);
public sealed class FilterValueOption(string label, string value, int count, bool selected) : INotifyPropertyChanged
{
    private bool _selected = selected;
    public string Label { get; } = label;
    public string Value { get; } = value;
    public int Count { get; } = count;
    public bool IsSelected { get => _selected; set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
