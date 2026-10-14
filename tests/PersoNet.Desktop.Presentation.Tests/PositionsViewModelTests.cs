using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class PositionsViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeOrganizationApi _api = new();
    private readonly FakeDialogService _dialogs = new();

    private Task AddAsync(PositionsViewModel viewModel, string name)
    {
        _dialogs.When<PositionDialogViewModel>(dialog =>
        {
            dialog.EditName = name;
            return dialog.SaveAsync(Cancellation);
        });
        return viewModel.NewAsync(Cancellation);
    }

    [Fact]
    public async Task New_window_creates_and_edit_window_renames_the_position()
    {
        var viewModel = new PositionsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);

        await AddAsync(viewModel, "Programista");
        viewModel.Selected!.Name.ShouldBe("Programista");

        _dialogs.When<PositionDialogViewModel>(dialog =>
        {
            dialog.Title.ShouldBe("Stanowisko – Programista");
            dialog.EditName.ShouldBe("Programista");
            dialog.EditName = "Starszy programista";
            return dialog.SaveAsync(Cancellation);
        });
        await viewModel.EditAsync(null, Cancellation);

        viewModel.Positions.Single().Name.ShouldBe("Starszy programista");
        viewModel.Selected!.Name.ShouldBe("Starszy programista");
    }

    [Fact]
    public async Task Show_inactive_includes_deactivated_positions()
    {
        var viewModel = new PositionsViewModel(_api, _dialogs);
        await AddAsync(viewModel, "Księgowa");
        await viewModel.ToggleActiveAsync(Cancellation);
        viewModel.Positions.ShouldBeEmpty();

        viewModel.ShowInactive = true;
        await viewModel.LoadAsync(Cancellation);

        viewModel.Positions.Single().IsActive.ShouldBeFalse();
        viewModel.Selected = viewModel.Positions.Single();
        viewModel.ToggleActiveText.ShouldBe("Aktywuj");
    }

    [Fact]
    public async Task Duplicate_name_is_shown_under_the_name_field()
    {
        var viewModel = new PositionsViewModel(_api, _dialogs);
        await AddAsync(viewModel, "Księgowa");

        var dialog = new PositionDialogViewModel(_api, [], editing: null);
        dialog.EditName = "Księgowa";
        await dialog.SaveAsync(Cancellation);

        dialog.GetError(nameof(dialog.EditName)).ShouldBe("Stanowisko o tej nazwie już istnieje.");
        dialog.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Empty_name_is_flagged_without_calling_the_api()
    {
        var dialog = new PositionDialogViewModel(_api, [], editing: null);

        await dialog.SaveAsync(Cancellation);

        _api.Positions.ShouldBeEmpty();
        dialog.GetError(nameof(dialog.EditName)).ShouldBe("Podaj nazwę stanowiska.");
    }

    [Fact]
    public async Task Hr_ties_a_position_to_departments_and_the_list_names_them()
    {
        var production = _api.Add("Produkcja", "PRO");
        _api.Add("Lakiernia", "LAK", production.Id);
        _api.Add("Archiwum", "ARCH", active: false);
        var viewModel = new PositionsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);

        _dialogs.When<PositionDialogViewModel>(dialog =>
        {
            // The tree in order with the child indented; an inactive department is not offered.
            dialog.Departments.Select(option => (option.Name, option.Indent)).ShouldBe([("Produkcja", 0d), ("Lakiernia", 20d)]);
            dialog.DepartmentsSummary.ShouldBe("Nie wybrano działu: stanowisko będzie do wyboru w każdym dziale.");

            dialog.DepartmentFilter = "lak";
            dialog.Departments.Select(option => option.IsVisible).ShouldBe([false, true]);
            dialog.Departments[1].IsChecked = true;
            dialog.DepartmentsSummary.ShouldBe("Wybrano 1 dział.");

            dialog.EditName = "Lakiernik";
            return dialog.SaveAsync(Cancellation);
        });
        await viewModel.NewAsync(Cancellation);
        await AddAsync(viewModel, "Brygadzista");

        viewModel.Positions.Select(row => (row.Name, row.Departments)).ShouldBe([("Lakiernik", "Lakiernia"), ("Brygadzista", "każdy dział")]);
    }
}
