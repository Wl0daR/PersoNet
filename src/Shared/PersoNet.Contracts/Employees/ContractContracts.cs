using System.Text.Json.Serialization;

namespace PersoNet.Contracts.Employees;

/// <summary>Kind of contract; sent as text, e.g. "Indefinite".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContractKind>))]
public enum ContractKind
{
    /// <summary>Umowa o pracę na okres próbny.</summary>
    Probation = 1,

    /// <summary>Umowa o pracę na czas określony.</summary>
    FixedTerm = 2,

    /// <summary>Umowa o pracę na czas nieokreślony.</summary>
    Indefinite = 3,

    /// <summary>Umowa zlecenie.</summary>
    Mandate = 4,

    /// <summary>Umowa o dzieło.</summary>
    SpecificWork = 5,

    /// <summary>Współpraca B2B.</summary>
    B2B = 6,
}

/// <summary>
/// A contract. <see cref="TerminatedOn"/> is set when the contract ended before its planned end because the employment ended.
/// </summary>
public sealed record ContractResponse(
    Guid Id, Guid EmployeeId, ContractKind Type, DateOnly StartDate, DateOnly? EndDate, decimal? WorkingTime, DateOnly? TerminatedOn = null);

/// <summary>Create and update take the same fields. <see cref="WorkingTime"/> is the fraction of full time, only for employment contracts.</summary>
public sealed record SaveContractRequest(ContractKind Type, DateOnly StartDate, DateOnly? EndDate, decimal? WorkingTime);
