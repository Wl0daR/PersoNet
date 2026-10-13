using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class DepartmentsViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeOrganizationApi _api = new();
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task Load_builds_the_tree_from_the_flat_list()
    {
        var it = _api.Add("Dział IT", "IT");
        _api.Add("Zespół aplikacji", "IT-APP", it.Id);
        _api.Add("Kadry", "KAD");
        var viewModel = new DepartmentsViewModel(_api, _dialogs);

        await viewModel.LoadAsync(Cancellation);

        viewModel.Roots.Select(root => root.Department.Code).ShouldBe(["IT", "KAD"]);
        viewModel.Roots[0].Children.Single().Department.Code.ShouldBe("IT-APP");
    }

    [Fact]
    public async Task Department_under_a_hidden_inactive_parent_is_shown_at_the_top()
    {
        var old = _api.Add("Stary dział", "OLD", active: false);
        _api.Add("Zespół", "TEAM", old.Id);
        var viewModel = new DepartmentsViewModel(_api, _dialogs);

        await viewModel.LoadAsync(Cancellation);

        viewModel.Roots.Single().Department.Code.ShouldBe("TEAM");
    }

    [Fact]
    public async Task Parent_list_never_offers_the_department_itself_or_its_children()
    {
        var it = _api.Add("Dział IT", "IT");
        var app = _api.Add("Zespół aplikacji", "IT-APP", it.Id);
        var hr = _api.Add("Kadry", "KAD");
        var viewModel = new DepartmentsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);

        await viewModel.EditAsync(viewModel.Roots.Single(root => root.Id == it.Id), Cancellation);

        var dialog = _dialogs.Shown.OfType<DepartmentDialogViewModel>().ShouldHaveSingleItem();
        dialog.Title.ShouldBe("Dział – Dział IT");
        dialog.EditCode.ShouldBe("IT");
        dialog.ParentOptions.Select(option => option.Id).ShouldBe([null, hr.Id]);
        dialog.ParentOptions.ShouldNotContain(option => option.Id == app.Id);
    }

    [Fact]
    public async Task New_after_selecting_a_department_proposes_it_as_parent_and_save_creates_child()
    {
        var it = _api.Add("Dział IT", "IT");
        var viewModel = new DepartmentsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Roots.Single();

        _dialogs.When<DepartmentDialogViewModel>(dialog =>
        {
            dialog.Title.ShouldBe("Nowy dział");
            dialog.EditParent.Id.ShouldBe(it.Id);
            dialog.EditName = "Zespół danych";
            dialog.EditCode = "it-data";
            return dialog.SaveAsync(Cancellation);
        });
        await viewModel.NewAsync(Cancellation);

        viewModel.ErrorMessage.ShouldBeNull();
        viewModel.Roots.Single().Children.Single().Department.Code.ShouldBe("IT-DATA");
        viewModel.Selected!.Department.ParentId.ShouldBe(it.Id);
    }

    [Fact]
    public async Task Taken_code_is_shown_under_the_code_field_and_keeps_the_window_open()
    {
        _api.Add("Dział IT", "IT");
        var viewModel = new DepartmentsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);
        var dialog = new DepartmentDialogViewModel(_api, viewModel.Roots, editing: null, parentId: null);

        dialog.EditName = "Drugi IT";
        dialog.EditCode = "IT";
        await dialog.SaveAsync(Cancellation);

        dialog.GetError(nameof(dialog.EditCode)).ShouldBe("Dział o tym kodzie już istnieje.");
        dialog.ErrorMessage.ShouldBeNull();
        dialog.IsBusy.ShouldBeFalse();
        dialog.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Empty_fields_are_flagged_without_calling_the_api_and_editing_clears_the_flag()
    {
        var dialog = new DepartmentDialogViewModel(_api, [], editing: null, parentId: null);

        dialog.SaveCommand.Execute(null);

        _api.Departments.ShouldBeEmpty();
        dialog.GetError(nameof(dialog.EditName)).ShouldBe("Podaj nazwę działu.");
        dialog.GetError(nameof(dialog.EditCode)).ShouldNotBeNull();

        dialog.EditName = "Kadry";

        dialog.GetError(nameof(dialog.EditName)).ShouldBeNull();
        dialog.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task Error_not_tied_to_a_field_is_shown_for_the_whole_form()
    {
        var it = _api.Add("Dział IT", "IT");
        _api.Add("Zespół aplikacji", "IT-APP", it.Id);
        var viewModel = new DepartmentsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Roots.Single();

        await viewModel.ToggleActiveAsync(Cancellation);

        viewModel.ErrorMessage.ShouldBe("Najpierw dezaktywuj poddziały tego działu.");
        viewModel.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task Empty_tree_is_reported_so_the_screen_can_explain_what_to_do()
    {
        var viewModel = new DepartmentsViewModel(_api, _dialogs);

        await viewModel.LoadAsync(Cancellation);

        viewModel.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Toggle_active_deactivates_and_hides_the_department()
    {
        _api.Add("Dział IT", "IT");
        var viewModel = new DepartmentsViewModel(_api, _dialogs);
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Roots.Single();

        await viewModel.ToggleActiveAsync(Cancellation);

        viewModel.Roots.ShouldBeEmpty();
        viewModel.Selected.ShouldBeNull();
    }
}
