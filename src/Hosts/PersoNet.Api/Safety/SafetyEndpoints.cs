using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Safety;
using PersoNet.Contracts.Safety;

namespace PersoNet.Api.Safety;

/// <summary>
/// Medical examinations and trainings (BHP): the company's list of trainings and each employee's examinations and
/// completed trainings. Reading needs <see cref="Permissions.SafetyRead"/>, changes need <see cref="Permissions.SafetyManage"/>.
/// </summary>
internal static class SafetyEndpoints
{
    public static RouteGroupBuilder MapSafetyEndpoints(this RouteGroupBuilder api)
    {
        var courses = api.MapGroup("/training-courses").WithTags("Badania i szkolenia");

        courses.MapGet("/", async (bool? includeInactive, TrainingCourseService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)))
            .RequireAuthorization(Permissions.SafetyRead)
            .WithName("ListTrainingCourses").WithSummary("Lista szkoleń w firmie");

        courses.MapPost("/", async (SaveTrainingCourseRequest request, TrainingCourseService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/training-courses/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("CreateTrainingCourse").WithSummary("Dodaj szkolenie do listy");

        courses.MapPut("/{id:guid}", async (Guid id, SaveTrainingCourseRequest request, TrainingCourseService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("UpdateTrainingCourse").WithSummary("Zmień szkolenie z listy");

        courses.MapPost("/{id:guid}/deactivate", async (Guid id, TrainingCourseService service, CancellationToken cancellationToken) =>
                (await service.SetActiveAsync(id, active: false, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("DeactivateTrainingCourse").WithSummary("Szkolenie nie jest już prowadzone");

        courses.MapPost("/{id:guid}/activate", async (Guid id, TrainingCourseService service, CancellationToken cancellationToken) =>
                (await service.SetActiveAsync(id, active: true, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("ActivateTrainingCourse").WithSummary("Przywróć szkolenie na listę");

        api.MapGet("/employees/{employeeId:guid}/safety", async (Guid employeeId, EmployeeSafetyService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(employeeId, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SafetyRead)
            .WithTags("Badania i szkolenia")
            .WithName("GetEmployeeSafety").WithSummary("Badania lekarskie i szkolenia pracownika oraz brakujące szkolenia obowiązkowe");

        api.MapPost("/employees/{employeeId:guid}/medical-exams", async (Guid employeeId, SaveMedicalExamRequest request, EmployeeSafetyService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateExamAsync(employeeId, request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/medical-exams/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithTags("Badania i szkolenia")
            .WithName("CreateMedicalExam").WithSummary("Wpisz badanie lekarskie pracownika");

        var exams = api.MapGroup("/medical-exams").WithTags("Badania i szkolenia").RequireAuthorization(Permissions.SafetyManage);

        exams.MapPut("/{id:guid}", async (Guid id, SaveMedicalExamRequest request, EmployeeSafetyService service, CancellationToken cancellationToken) =>
                (await service.UpdateExamAsync(id, request, cancellationToken)).ToOk())
            .WithName("UpdateMedicalExam").WithSummary("Popraw badanie lekarskie");

        exams.MapDelete("/{id:guid}", async (Guid id, EmployeeSafetyService service, CancellationToken cancellationToken) =>
                (await service.DeleteExamAsync(id, cancellationToken)).ToNoContent())
            .WithName("DeleteMedicalExam").WithSummary("Usuń badanie wpisane przez pomyłkę");

        api.MapPost("/employees/{employeeId:guid}/trainings", async (Guid employeeId, SaveEmployeeTrainingRequest request, EmployeeSafetyService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateTrainingAsync(employeeId, request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/trainings/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithTags("Badania i szkolenia")
            .WithName("CreateEmployeeTraining").WithSummary("Wpisz szkolenie, które pracownik ukończył");

        var trainings = api.MapGroup("/trainings").WithTags("Badania i szkolenia").RequireAuthorization(Permissions.SafetyManage);

        trainings.MapPut("/{id:guid}", async (Guid id, SaveEmployeeTrainingRequest request, EmployeeSafetyService service, CancellationToken cancellationToken) =>
                (await service.UpdateTrainingAsync(id, request, cancellationToken)).ToOk())
            .WithName("UpdateEmployeeTraining").WithSummary("Popraw szkolenie pracownika");

        trainings.MapDelete("/{id:guid}", async (Guid id, EmployeeSafetyService service, CancellationToken cancellationToken) =>
                (await service.DeleteTrainingAsync(id, cancellationToken)).ToNoContent())
            .WithName("DeleteEmployeeTraining").WithSummary("Usuń szkolenie wpisane przez pomyłkę");

        api.MapPost("/safety/import", async (ImportSafetyRequest request, SafetyImportService service, CancellationToken cancellationToken) =>
                (await service.ImportAsync(request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SafetyManage)
            .WithTags("Badania i szkolenia")
            .WithName("ImportSafety").WithSummary("Import listy szkoleń, szkoleń pracowników i badań z poprzedniego systemu");

        return api;
    }
}
