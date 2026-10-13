using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Organization;

internal static class OrganizationNames
{
    public static string Normalize(string value, int maxLength, string what)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{what} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{what} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
