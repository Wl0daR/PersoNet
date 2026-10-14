using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Snapshots;

/// <summary>The snapshots never save or open files.</summary>
internal sealed class SnapshotFiles : IFileService
{
    public string? AskWhereToSaveWorkbook(string fileName) => null;

    public IReadOnlyList<string> AskWhichSqlFilesToOpen() => [];

    public void Open(string path)
    {
    }
}
