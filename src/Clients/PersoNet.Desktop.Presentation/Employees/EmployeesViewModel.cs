using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Access;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>
/// "Pracownicy" screen: a searchable, paged list across the whole window. An employee's card opens in its own window
/// (double click, Enter or "Otwórz kartotekę"), as does the card of a new employee.
/// </summary>
public sealed partial class EmployeesViewModel(
    IEmployeesApi employees, IOrganizationApi organization, ILeaveApi leave, IAccessApi access, UserAccess user, TimeProvider time, IDialogService dialogs)
    : EditorViewModel
{
    public const int PageSize = 50;

    /// <summary>Pause after the last key press before the list is searched, so typing does not query on every key.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _pendingSearch;

    public ObservableCollection<EmployeeListItem> Items { get; } = [];

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeFormer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int Page { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int TotalPages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    public partial int TotalCount { get; set; }

    /// <summary>True when the list is empty, so the view can explain why (no employees yet, or nothing matches).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyTitle), nameof(EmptyHint))]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial EmployeeListItem? Selected { get; set; }

    public string PageInfo => TotalCount == 0
        ? string.Empty
        : $"Strona {Page} z {Math.Max(TotalPages, 1)} · {Pluralize(TotalCount)}";

    public string EmptyTitle => string.IsNullOrWhiteSpace(Search) && !IncludeFormer ? "Nie ma jeszcze pracowników" : "Nikt nie pasuje do wyszukiwania";

    public string EmptyHint => string.IsNullOrWhiteSpace(Search) && !IncludeFormer
        ? "Kliknij „Dodaj pracownika”, aby założyć pierwszą kartotekę."
        : "Sprawdź pisownię albo zaznacz „Pokaż byłych pracowników”.";

    /// <summary>Column the list is sorted by (a click on a column header); the server sorts, so every page follows it.</summary>
    [ObservableProperty]
    public partial EmployeeSort Sort { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    /// <summary>Sorts by the column; a second click on the same column reverses the order.</summary>
    [RelayCommand]
    public Task SortByAsync(EmployeeSort column, CancellationToken cancellationToken)
    {
        SortDescending = Sort == column && !SortDescending;
        Sort = column;
        return RunAsync(() => LoadPageAsync(1, Selected?.Id, cancellationToken));
    }

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(Page, Selected?.Id, cancellationToken));

    /// <summary>Runs the search from the first page.</summary>
    [RelayCommand]
    public Task SearchAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(1, Selected?.Id, cancellationToken));

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    public Task PreviousPageAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(Page - 1, Selected?.Id, cancellationToken));

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    public Task NextPageAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(Page + 1, Selected?.Id, cancellationToken));

    /// <summary>Opens the card of the employee picked in the list in its own window.</summary>
    [RelayCommand(CanExecute = nameof(CanOpen))]
    public Task OpenAsync(EmployeeListItem? item, CancellationToken cancellationToken) =>
        (item ?? Selected) is { } employee ? ShowCardAsync(employee.Id, cancellationToken) : Task.CompletedTask;

    /// <summary>Opens an empty card for a new employee.</summary>
    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) => ShowCardAsync(employeeId: null, cancellationToken);

    /// <summary>Creates the card window's view model; the tests and the screenshot tool open it the same way.</summary>
    public EmployeeCardViewModel CreateCard() => new(employees, organization, leave, access, user, time, dialogs);

    private async Task ShowCardAsync(Guid? employeeId, CancellationToken cancellationToken)
    {
        var card = CreateCard();
        await card.LoadAsync(employeeId, cancellationToken);
        await dialogs.ShowAsync(card);

        // The card may have saved, ended the employment or added contracts; the list shows the current state.
        if (card.HasChanges || employeeId is null)
        {
            await RunAsync(() => LoadPageAsync(Page, card.Current?.Id ?? Selected?.Id, cancellationToken));
        }
    }

    partial void OnIncludeFormerChanged(bool value) => _ = SearchAsync(CancellationToken.None);

    // Search as you type: each key press restarts the wait, and only the last one runs the search.
    partial void OnSearchChanged(string value)
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        _pendingSearch = new CancellationTokenSource();
        _ = SearchAfterPauseAsync(_pendingSearch.Token);
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    private async Task SearchAfterPauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDelay, time, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchAsync(cancellationToken);
    }

    private bool CanOpen(EmployeeListItem? item) => (item ?? Selected) is not null;

    private bool HasPreviousPage() => Page > 1;

    private bool HasNextPage() => Page < TotalPages;

    private async Task LoadPageAsync(int page, Guid? keepSelected, CancellationToken cancellationToken)
    {
        var result = await employees.ListAsync(new EmployeeListQuery(Search, null, IncludeFormer, Math.Max(page, 1), PageSize, Sort, SortDescending), cancellationToken);

        // Asking past the last page (e.g. after a filter shrank the list) falls back to the last one.
        if (result.Items.Count == 0 && result.TotalCount > 0 && page > 1)
        {
            result = await employees.ListAsync(new EmployeeListQuery(Search, null, IncludeFormer, result.TotalPages, PageSize, Sort, SortDescending), cancellationToken);
        }

        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }

        Page = result.Page;
        TotalPages = result.TotalPages;
        TotalCount = result.TotalCount;
        IsEmpty = Items.Count == 0;
        Selected = Items.FirstOrDefault(item => item.Id == keepSelected);
    }

    private static string Pluralize(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;

        var word = count == 1 ? "osoba"
            : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "osoby"
            : "osób";

        return $"{count} {word}";
    }
}
