using System.Reflection;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace PersoNet.Architecture.Tests;

/// <summary>
/// Guards the Clean Architecture dependency rule: dependencies point inward only.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Domain.Abstractions.Entity).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Contracts = typeof(Contracts.Common.PagedResponse<>).Assembly;
    private static readonly Assembly DesktopPresentation = typeof(Desktop.Presentation.Shell.ShellViewModel).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("PersoNet.Application", "PersoNet.Infrastructure", "PersoNet.Contracts", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_hosts()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("PersoNet.Infrastructure", "PersoNet.Api", "PersoNet.Worker", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Contracts_do_not_expose_domain_types()
    {
        var result = Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOn("PersoNet.Domain")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Desktop_view_models_do_not_depend_on_wpf()
    {
        var result = Types.InAssembly(DesktopPresentation)
            .ShouldNot()
            .HaveDependencyOnAny("System.Windows.Controls", "System.Windows.Media", "System.Windows.Markup", "System.Windows.Threading")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
