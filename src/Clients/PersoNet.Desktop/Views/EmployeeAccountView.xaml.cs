using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class EmployeeAccountView : UserControl
{
    public EmployeeAccountView()
    {
        InitializeComponent();
    }

    // Copying is a view concern: the clipboard belongs to the window, not the view model.
    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(LinkBox.Text);
            CopyText.Text = "Skopiowano";
        }
        catch (COMException)
        {
            // Another program holds the clipboard; the link can still be selected and copied by hand.
            LinkBox.Focus();
            LinkBox.SelectAll();
        }
    }
}
