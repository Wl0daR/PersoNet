namespace PersoNet.Desktop.Presentation.Common;

/// <summary>Up to two capital letters for an avatar, e.g. "Anna Kowalska" → "AK".</summary>
public static class Initials
{
    public static string Of(string? fullName) => fullName is null
        ? string.Empty
        : string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));

    public static string Of(string? firstName, string? lastName) => Of($"{firstName} {lastName}");
}
