using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static class HoldEndpoints
{
    public static IEndpointRouteBuilder MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        var holds = app.MapGroup("/holds").WithTags("Holds");

        // 5. Current status of an application.
        holds.MapGet("/{holdId}", async (string holdId, AdmissionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetHoldAsync(holdId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToResult();
        });

        // 6. Pay the admission fee: confirm the admission.
        holds.MapPost("/{holdId}/confirm", async (
            string holdId,
            [FromHeader(Name = StudentIdentity.HeaderName)] string? studentHeader,
            AdmissionService service,
            CancellationToken cancellationToken) =>
        {
            var student = StudentIdentity.Parse(studentHeader);
            if (!student.IsSuccess)
            {
                return student.Error.ToResult();
            }

            var result = await service.ConfirmHoldAsync(holdId, student.Value, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToResult();
        });

        // 7. Cancel an application.
        holds.MapDelete("/{holdId}", async (
            string holdId,
            [FromHeader(Name = StudentIdentity.HeaderName)] string? studentHeader,
            AdmissionService service,
            CancellationToken cancellationToken) =>
        {
            var student = StudentIdentity.Parse(studentHeader);
            if (!student.IsSuccess)
            {
                return student.Error.ToResult();
            }

            var result = await service.CancelHoldAsync(holdId, student.Value, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToResult();
        });

        // 8. All applications of the student.
        app.MapGet("/me/holds", async (
            [FromHeader(Name = StudentIdentity.HeaderName)] string? studentHeader,
            AdmissionService service,
            CancellationToken cancellationToken) =>
        {
            var student = StudentIdentity.Parse(studentHeader);
            if (!student.IsSuccess)
            {
                return student.Error.ToResult();
            }

            return Results.Ok(await service.ListStudentHoldsAsync(student.Value, cancellationToken));
        }).WithTags("Holds");

        return app;
    }
}
