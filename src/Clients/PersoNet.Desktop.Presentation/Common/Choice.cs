namespace PersoNet.Desktop.Presentation.Common;

/// <summary>An entry of a drop-down list; <see cref="Id"/> is null for the "none" entry.</summary>
public sealed record Choice(Guid? Id, string Name);
