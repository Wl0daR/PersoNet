using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class DepartmentsViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeOrganizationApi _api = new();

    [Fact]
    public async Task Load_builds_the_tree_from_the_flat_list()
    {
        var it = _api.Add("Dział IT", "IT");
        _api.Add("Zespół aplikacji", "IT-APP", it.Id);
        _api.Add("Kadry", "KAD");
        var viewModel = new DepartmentsViewModel(_api);

        await viewModel.LoadAsync(Cancellation);

        viewModel.Roots.Select(root => root.Department.Code).ShouldBe(["IT", "KAD"]);
        viewModel.Roots[0].Children.Single().Department.Code.ShouldBe("IT-APP");
    }

    [Fact]
    public async Task Department_under_a_hidden_inactive_parent_is_shown_at_the_top()
    {
        var old = _api.Add("Stary dział", "OLD", active: false);
        _api.Add("Zespół", "TEAM", old.Id);
        var viewModel = new DepartmentsViewModel(_api);

        await viewModel.LoadAsync(Cancellation);

        viewModel.Roots.Single().Department.Code.ShouldBe("TEAM");
    }

    [Fact]
    public async Task Parent_list_never_offers_the_department_itself_or_its_children()
    {
        var it = _api.Add("Dział IT", "IT");
        var app = _api.Add("Zespół aplikacji", "IT-APP", it.Id);
        var hr = _api.Add("Kadry", "KAD");
        var viewModel = new DepartmentsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);

        viewModel.Selected = viewModel.Roots.Single(root => root.Id == it.Id);

        viewModel.ParentOptions.Select(option => option.Id).ShouldBe([null, hr.Id]);
        viewModel.ParentOptions.ShouldNotContain(option => option.Id == app.Id);
    }

    [Fact]
    public async Task New_after_selecting_a_department_proposes_it_as_parent_and_save_creates_child()
    {
        var it = _api.Add("Dział IT", "IT");
        var viewModel = new DepartmentsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Roots.Single();

        viewModel.New();
        viewModel.EditName = "Zespół danych";
        viewModel.EditCode = "it-data";
        await viewModel.SaveAsync(Cancellation);

        viewModel.ErrorMessage.ShouldBeNull();
        viewModel.Roots.Single().Children.Single().Department.Code.ShouldBe("IT-DATA");
        viewModel.Selected!.Department.ParentId.ShouldBe(it.Id);
        viewModel.IsNew.ShouldBeFalse();
    }

    [Fact]
    public async Task Taken_code_is_shown_under_the_code_field()
    {
        _api.Add("Dział IT", "IT");
        var viewModel = new DepartmentsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);

        viewModel.New();
        viewModel.EditName = "Drugi IT";
        viewModel.EditCode = "IT";
        await viewModel.SaveAsync(Cancellation);

        viewModel.GetError(nameof(viewModel.EditCode)).ShouldBe("Dział o tym kodzie już istnieje.");
        viewModel.ErrorMessage.ShouldBeNull();
        viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Empty_fields_are_flagged_without_calling_the_api_and_editing_clears_the_flag()
    {
        var viewModel = new DepartmentsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);

        await viewModel.SaveAsync(Cancellation);

        _api.Departments.ShouldBeEmpty();
        viewModel.GetError(nameof(viewModel.EditName)).ShouldBe("Podaj nazwę działu.");
        viewModel.GetError(nameof(viewModel.EditCode)).ShouldNotBeNull();

        viewModel.EditName = "Kadry";

        viewModel.GetError(nameof(viewModel.EditName)).ShouldBeNull();
        viewModel.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task Error_not_tied_to_a_field_is_shown_for_the_whole_form()
    {
        var it = _api.Add("Dział IT", "IT");
        _api.Add("Zespół aplikacji", "IT-APP", it.Id);
        var viewModel = new DepartmentsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Roots.Single();

        await viewModel.ToggleActiveAsync(Cancellation);

        viewModel.ErrorMessage.ShouldBe("Najpierw dezaktywuj poddziały tego działu.");
        viewModel.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task Empty_tree_is_reported_so_the_screen_can_explain_what_to_do()
    {
        var viewModel = new DepartmentsViewModel(_api);

        await viewModel.LoadAsync(Cancellation);

        viewModel.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Toggle_active_deactivates_and_hides_the_department()
    {
        _api.Add("Dział IT", "IT");
        var viewModel = new DepartmentsViewModel(_api);
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Roots.Single();

        await viewModel.ToggleActiveAsync(Cancellation);

        viewModel.Roots.ShouldBeEmpty();
        viewModel.IsNew.ShouldBeTrue();
    }
}
