using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Views;

/// <summary>
/// The days of a month differ in number, so their columns are made here whenever the shown month changes; each cell
/// shows the row's <see cref="TeamCalendarRow.Cells"/> entry for that day.
/// </summary>
public partial class TeamCalendarView : UserControl
{
    private TeamCalendarViewModel? _model;

    public TeamCalendarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = DataContext as TeamCalendarViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }

        BuildDayColumns();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TeamCalendarViewModel.Days))
        {
            BuildDayColumns();
        }
    }

    private void BuildDayColumns()
    {
        while (Grid.Columns.Count > 1)
        {
            Grid.Columns.RemoveAt(1);
        }

        if (_model is null)
        {
            return;
        }

        var headerTemplate = (DataTemplate)FindResource("DayHeader");
        var cellTemplate = (DataTemplate)FindResource("DayCell");
        for (var index = 0; index < _model.Days.Count; index++)
        {
            var cell = new FrameworkElementFactory(typeof(ContentPresenter));
            cell.SetBinding(ContentPresenter.ContentProperty, new Binding($"{nameof(TeamCalendarRow.Cells)}[{index}]"));
            cell.SetValue(ContentPresenter.ContentTemplateProperty, cellTemplate);

            Grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = _model.Days[index],
                HeaderTemplate = headerTemplate,
                CellTemplate = new DataTemplate { VisualTree = cell },
                Width = 38,
                MinWidth = 38,
            });
        }
    }
}
