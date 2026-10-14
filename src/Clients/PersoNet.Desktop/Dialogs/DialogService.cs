using System.Windows;
using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Organization;
using PersoNet.Desktop.Views;

namespace PersoNet.Desktop.Dialogs;

/// <summary>Opens each dialog view model in a modal window over the active window, with its view.</summary>
public sealed class DialogService : IDialogService
{
    public Task<bool> ShowAsync(DialogViewModel dialog)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? Application.Current.MainWindow;
        var area = SystemParameters.WorkArea;
        var window = new DialogWindow(dialog, CreateView(dialog), new Size(area.Width * 0.95, area.Height * 0.95)) { Owner = owner };

        return Task.FromResult(window.ShowModal());
    }

    /// <summary>The view that shows a dialog view model.</summary>
    public static FrameworkElement CreateView(DialogViewModel dialog) => dialog switch
    {
        EmployeeCardViewModel => new EmployeeCardView(),
        ContractDialogViewModel => new ContractDialogView(),
        LeaveFormViewModel => new LeaveFormView(),
        LeaveDecisionViewModel => new LeaveDecisionView(),
        EntitlementDialogViewModel => new EntitlementDialogView(),
        DepartmentDialogViewModel => new DepartmentDialogView(),
        PositionDialogViewModel => new PositionDialogView(),
        RoleDialogViewModel => new RoleDialogView(),
        _ => throw new ArgumentOutOfRangeException(nameof(dialog), dialog.GetType().Name, "Brak widoku dla tego okna."),
    };
}
