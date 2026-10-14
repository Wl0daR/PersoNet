using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>Answers the save dialog with <see cref="SavePath"/> (null: the user cancels) and records opened files.</summary>
internal sealed class FakeFileService : IFileService
{
    public string? SavePath { get; set; }

    public string? SuggestedName { get; private set; }

    public List<string> Opened { get; } = [];

    public string? AskWhereToSaveWorkbook(string fileName)
    {
        SuggestedName = fileName;
        return SavePath;
    }

    public void Open(string path) => Opened.Add(path);
}
