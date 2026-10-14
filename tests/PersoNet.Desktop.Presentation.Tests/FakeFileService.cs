using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>Answers the save dialog with <see cref="SavePath"/>, the open dialog with <see cref="OpenPath"/> (null: the user cancels) and records opened files.</summary>
internal sealed class FakeFileService : IFileService
{
    public string? SavePath { get; set; }

    public string? SuggestedName { get; private set; }

    /// <summary>The file picked in the open dialog (null: the user cancels).</summary>
    public string? OpenPath { get; set; }

    public List<string> Opened { get; } = [];

    public string? AskWhereToSaveWorkbook(string fileName)
    {
        SuggestedName = fileName;
        return SavePath;
    }

    public string? AskWhichSqlFileToOpen() => OpenPath;

    public void Open(string path) => Opened.Add(path);
}
