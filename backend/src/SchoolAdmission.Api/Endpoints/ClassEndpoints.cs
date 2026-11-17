using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static class ClassEndpoints
{
    public static IEndpointRouteBuilder MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        var classes = app.MapGroup("/classes").WithTags("Classes");

        // 1. Create a class (school admin).
        classes.MapPost(string.Empty, async (CreateClassRequest? request, AdmissionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateClassAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/classes/{result.Value.Id}", result.Value)
                : result.Error.ToResult();
        });

        // 2. All classes with seat counts.
        classes.MapGet(string.Empty, async (AdmissionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListClassesAsync(cancellationToken)));

        // 3. Seat counts of one class.
        classes.MapGet("/{classId}", async (string classId, AdmissionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetClassAsync(classId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToResult();
        });

        // 4. Apply: hold a seat for the student.
        classes.MapPost("/{classId}/holds", async (
            string classId,
            [FromHeader(Name = StudentIdentity.HeaderName)] string? studentHeader,
            AdmissionService service,
            CancellationToken cancellationToken) =>
        {
            var student = StudentIdentity.Parse(studentHeader);
            if (!student.IsSuccess)
            {
                return student.Error.ToResult();
            }

            var result = await service.CreateHoldAsync(classId, student.Value, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/holds/{result.Value.HoldId}", result.Value)
                : result.Error.ToResult();
        });

        return app;
    }
}
