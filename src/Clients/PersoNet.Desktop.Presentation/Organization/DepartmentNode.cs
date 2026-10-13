using System.Collections.ObjectModel;
using PersoNet.Contracts.Organization;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// One department in the tree on the "Działy" screen.
/// </summary>
public sealed class DepartmentNode(DepartmentResponse department)
{
    public DepartmentResponse Department { get; } = department;

    public Guid Id => Department.Id;

    public string DisplayName =>
        Department.IsActive ? $"{Department.Name} ({Department.Code})" : $"{Department.Name} ({Department.Code}) – nieaktywny";

    public ObservableCollection<DepartmentNode> Children { get; } = [];

    /// <summary>
    /// Builds the tree from the flat API list. A department whose parent is not in the list (for example an
    /// inactive parent that is hidden) is shown at the top level, so nothing disappears.
    /// </summary>
    public static IReadOnlyList<DepartmentNode> BuildTree(IEnumerable<DepartmentResponse> departments)
    {
        var nodes = departments.ToDictionary(department => department.Id, department => new DepartmentNode(department));
        var roots = new List<DepartmentNode>();

        foreach (var node in nodes.Values.OrderBy(node => node.Department.Name, StringComparer.CurrentCulture))
        {
            if (node.Department.ParentId is { } parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    /// <summary>This node and everything below it.</summary>
    public IEnumerable<DepartmentNode> SelfAndDescendants()
    {
        yield return this;

        foreach (var child in Children)
        {
            foreach (var descendant in child.SelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }
}

/// <summary>
/// An entry in the "Dział nadrzędny" list. <see cref="Id"/> null means a top-level department.
/// </summary>
public sealed record ParentOption(Guid? Id, string Name);
