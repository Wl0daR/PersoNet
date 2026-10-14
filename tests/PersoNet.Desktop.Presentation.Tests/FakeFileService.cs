using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>Answers the save dialog with <see cref="SavePath"/>, the open dialog with <see cref="OpenPaths"/> (null: the user cancels) and records opened files.</summary>
internal sealed class FakeFileService : IFileService
{
    public string? SavePath { get; set; }

    public string? SuggestedName { get; private set; }

    /// <summary>The files picked in the open dialog (none: the user cancels).</summary>
    public List<string> OpenPaths { get; } = [];

    public List<string> Opened { get; } = [];

    public string? AskWhereToSaveWorkbook(string fileName)
    {
        SuggestedName = fileName;
        return SavePath;
    }

    public IReadOnlyList<string> AskWhichSqlFilesToOpen() => OpenPaths;

    public void Open(string path) => Opened.Add(path);
}
