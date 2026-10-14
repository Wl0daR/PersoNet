using PersoNet.Contracts.Leave;
using PersoNet.Contracts.SelfService;

namespace PersoNet.ApiClient.Portal;

/// <summary>
/// The employee and manager portal: the signed-in person's own record and leave, and their team's requests.
/// </summary>
public interface IPortalApi
{
    Task<MyProfileResponse> GetProfileAsync(CancellationToken cancellationToken);

    Task<LeaveBalanceResponse> GetBalanceAsync(int year, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeaveRequestResponse>> ListMyRequestsAsync(int? year, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> CreateMyRequestAsync(MyLeaveRequest request, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> WithdrawAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The people who report to the signed-in manager, with their annual leave in <paramref name="year"/>.</summary>
    Task<IReadOnlyList<TeamMemberResponse>> ListTeamAsync(int year, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeaveRequestResponse>> ListTeamRequestsAsync(LeaveState? state, int? year, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken);

    Task<LeaveRequestResponse> RejectAsync(Guid id, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<HolidayResponse>> GetHolidaysAsync(int year, CancellationToken cancellationToken);
}
