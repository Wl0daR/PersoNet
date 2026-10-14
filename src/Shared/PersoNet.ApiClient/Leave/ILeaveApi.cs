using PersoNet.Contracts.Leave;

namespace PersoNet.ApiClient.Leave;

/// <summary>Leave requests, approvals and annual leave balances in PersoNet.Api. Failures throw <see cref="ApiException"/>.</summary>
public interface ILeaveApi
{
    Task<IReadOnlyList<LeaveRequestResponse>> ListAsync(LeaveState? state, int? year, Guid? employeeId, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> RejectAsync(Guid id, string reason, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> CancelAsync(Guid id, string? note, CancellationToken cancellationToken);

    Task<LeaveBalanceResponse> GetBalanceAsync(Guid employeeId, int year, CancellationToken cancellationToken);

    Task<IReadOnlyList<HolidayResponse>> GetHolidaysAsync(int year, CancellationToken cancellationToken);

    /// <summary>Who is on leave in the month: the whole company, or a department with the departments under it.</summary>
    Task<LeaveCalendarResponse> GetCalendarAsync(int year, int month, Guid? departmentId, CancellationToken cancellationToken);

    Task<LeaveBalanceResponse> SetEntitlementAsync(Guid employeeId, int year, SaveLeaveEntitlementRequest request, CancellationToken cancellationToken);
}
