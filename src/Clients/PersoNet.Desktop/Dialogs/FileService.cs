using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Dialogs;

/// <summary>The Windows "Zapisz jako" dialog for exports, "Otwórz" for imports, and opening a saved file in its program.</summary>
public sealed class FileService : IFileService
{
    public string? AskWhereToSaveWorkbook(string fileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Zapisz raport",
            FileName = fileName,
            DefaultExt = ".xlsx",
            Filter = "Skoroszyt programu Excel (*.xlsx)|*.xlsx",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? Application.Current.MainWindow;
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? AskWhichSqlFileToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz plik z bazy danych",
            Filter = "Plik SQL (*.sql)|*.sql|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? Application.Current.MainWindow;
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public void Open(string path)
    {
        if (File.Exists(path))
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
