using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class PositionsViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeOrganizationApi _api = new();

    [Fact]
    public async Task Save_creates_then_renames_the_selected_position()
    {
        var viewModel = new PositionsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);

        viewModel.EditName = "Programista";
        await viewModel.SaveAsync(Cancellation);
        viewModel.EditName = "Starszy programista";
        await viewModel.SaveAsync(Cancellation);

        viewModel.Positions.Single().Name.ShouldBe("Starszy programista");
        viewModel.Selected!.Name.ShouldBe("Starszy programista");
    }

    [Fact]
    public async Task Show_inactive_includes_deactivated_positions()
    {
        var viewModel = new PositionsViewModel(_api);
        viewModel.EditName = "Księgowa";
        await viewModel.SaveAsync(Cancellation);
        await viewModel.ToggleActiveAsync(Cancellation);
        viewModel.Positions.ShouldBeEmpty();

        viewModel.ShowInactive = true;
        await viewModel.LoadAsync(Cancellation);

        viewModel.Positions.Single().IsActive.ShouldBeFalse();
        viewModel.ToggleActiveText.ShouldBe("Dezaktywuj");
    }

    [Fact]
    public async Task Duplicate_name_is_shown_under_the_name_field()
    {
        var viewModel = new PositionsViewModel(_api);
        viewModel.EditName = "Księgowa";
        await viewModel.SaveAsync(Cancellation);

        viewModel.New();
        viewModel.EditName = "Księgowa";
        await viewModel.SaveAsync(Cancellation);

        viewModel.GetError(nameof(viewModel.EditName)).ShouldBe("Stanowisko o tej nazwie już istnieje.");
    }

    [Fact]
    public async Task Empty_name_is_flagged_without_calling_the_api()
    {
        var viewModel = new PositionsViewModel(_api);

        await viewModel.SaveAsync(Cancellation);

        _api.Positions.ShouldBeEmpty();
        viewModel.GetError(nameof(viewModel.EditName)).ShouldBe("Podaj nazwę stanowiska.");
    }
}
