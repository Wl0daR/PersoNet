using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PersoNet.Desktop.Views;

/// <summary>
/// After a failed save, moves keyboard focus to the first field (in form order) that shows an error.
/// </summary>
internal static class FormFocus
{
    public static void Attach(FrameworkElement view, params Control[] fieldsInOrder)
    {
        INotifyDataErrorInfo? current = null;

        void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
        {
            // Only react to a new error; an error going away is the user fixing a field, so focus stays put.
            if (sender is not INotifyDataErrorInfo errors || !errors.GetErrors(e.PropertyName).Cast<object>().Any())
            {
                return;
            }

            // Wait until all errors of this save are set and the bindings have picked them up.
            view.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                fieldsInOrder.FirstOrDefault(field => Validation.GetHasError(field) && !field.IsKeyboardFocusWithin)?.Focus());
        }

        view.DataContextChanged += (_, e) =>
        {
            if (current is not null)
            {
                current.ErrorsChanged -= OnErrorsChanged;
            }

            current = e.NewValue as INotifyDataErrorInfo;

            if (current is not null)
            {
                current.ErrorsChanged += OnErrorsChanged;
            }
        };
    }
}
