namespace PersoNet.Desktop.Presentation.Common;

/// <summary>Files the user saves from the app or reads into it: where they are, and opening one afterwards.</summary>
public interface IFileService
{
    /// <summary>Asks where to save an Excel workbook, suggesting <paramref name="fileName"/>; null when the user cancels.</summary>
    string? AskWhereToSaveWorkbook(string fileName);

    /// <summary>Asks which SQL dump files to read (one or more); empty when the user cancels.</summary>
    IReadOnlyList<string> AskWhichSqlFilesToOpen();

    /// <summary>Opens a saved file in the program Windows uses for it (Excel for a workbook).</summary>
    void Open(string path);
}
