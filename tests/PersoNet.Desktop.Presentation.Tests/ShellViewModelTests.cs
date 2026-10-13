using System.ComponentModel;
using PersoNet.Desktop.Presentation.Shell;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void Changing_title_raises_property_changed()
    {
        var viewModel = new ShellViewModel();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Title = "Kartoteka";

        raised.ShouldBe([nameof(ShellViewModel.Title)]);
    }
}
