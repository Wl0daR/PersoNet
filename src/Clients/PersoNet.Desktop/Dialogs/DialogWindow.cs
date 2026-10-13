using System.Windows;
using System.Windows.Input;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Dialogs;

/// <summary>
/// Window that hosts one dialog view (a form or a record). It opens at the view's size, never larger than
/// the given maximum size (the screen's work area), and can then be resized like any window. "Anuluj" (IsCancel) and Esc
/// close it; the view model closes it after a successful save.
/// </summary>
public sealed class DialogWindow : Window
{
    private static readonly Uri ListSelection = new("pack://application:,,,/PersoNet.Desktop;component/Themes/ListSelection.xaml");

    private readonly DialogViewModel _viewModel;
    private bool _isModal;

    public DialogWindow(DialogViewModel viewModel, FrameworkElement view, Size maxSize)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(view);

        _viewModel = viewModel;
        DataContext = viewModel;
        SetBinding(TitleProperty, nameof(DialogViewModel.Title));

        // Room for the title bar and the window frame.
        const double ChromeWidth = 16;
        const double ChromeHeight = 40;
        MaxWidth = maxSize.Width;
        MaxHeight = maxSize.Height;
        if (!double.IsNaN(view.Width))
        {
            view.Width = Math.Min(view.Width, maxSize.Width - ChromeWidth);
        }

        if (!double.IsNaN(view.Height))
        {
            view.Height = Math.Min(view.Height, maxSize.Height - ChromeHeight);
        }

        Content = view;
        MinWidth = 360;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ListSelection });

        // Once open, the window keeps its size and the view follows when the user resizes it.
        ContentRendered += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;
            view.Width = double.NaN;
            view.Height = double.NaN;
        };

        // Keyboard focus starts in the first field, so the user can type straight away.
        Loaded += (_, _) => view.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    /// <summary>Shows the window as modal over its owner; true when it was closed after a save.</summary>
    public bool ShowModal()
    {
        _isModal = true;
        return ShowDialog() == true || _viewModel.HasChanges;
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        if (_isModal)
        {
            DialogResult = result;
        }
        else
        {
            Close();
        }
    }
}
