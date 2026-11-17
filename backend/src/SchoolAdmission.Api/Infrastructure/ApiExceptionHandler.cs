using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Last line of defence: turns any exception into a problem-details response. Bad input becomes a
/// 4xx (never a 500), and no stack trace or SQL ever reaches the client.
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody to answer.
            return true;
        }

        var error = Map(exception);
        if (error.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Request {Method} {Path} failed with {Code}", httpContext.Request.Method, httpContext.Request.Path, error.Code);
        }
        else
        {
            logger.LogInformation("Request {Method} {Path} rejected with {Code}: {Message}", httpContext.Request.Method, httpContext.Request.Path, error.Code, exception.Message);
        }

        httpContext.Response.StatusCode = error.StatusCode;
        if (error.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            httpContext.Response.Headers.RetryAfter = "2";
        }

        var problem = new ProblemDetails
        {
            Status = error.StatusCode,
            Title = error.Title,
            Detail = error.Detail,
        };
        problem.Extensions["code"] = error.Code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private static ApiError Map(Exception exception) => exception switch
    {
        BadHttpRequestException badRequest => new ApiError(
            badRequest.StatusCode,
            "INVALID_REQUEST",
            "Invalid request",
            "The request could not be read. Send a valid JSON body with the expected field types."),

        // Safety net behind input validation: text the database cannot store is the client's fault.
        _ when IsUnstorableText(exception) => new ApiError(
            StatusCodes.Status400BadRequest,
            "INVALID_REQUEST",
            "Invalid request",
            "The request contains characters that cannot be stored (for example a NUL byte)."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => new ApiError(
            StatusCodes.Status409Conflict,
            "CONFLICT",
            "Conflict",
            "The request conflicts with data that was saved at the same moment. Please refresh and try again."),

        _ when IsTransientDatabaseError(exception) => new ApiError(
            StatusCodes.Status503ServiceUnavailable,
            "SERVICE_UNAVAILABLE",
            "Service temporarily unavailable",
            "The server is busy or cannot reach the database right now. Please try again in a moment."),

        _ => new ApiError(
            StatusCodes.Status500InternalServerError,
            "INTERNAL_ERROR",
            "Unexpected error",
            "Something went wrong on the server. Please try again."),
    };

    private static bool IsUnstorableText(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is EncoderFallbackException
                || current is PostgresException { SqlState: PostgresErrorCodes.CharacterNotInRepertoire or PostgresErrorCodes.UntranslatableCharacter })
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTransientDatabaseError(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException postgres when postgres.SqlState is
                    PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected or
                    PostgresErrorCodes.LockNotAvailable or
                    PostgresErrorCodes.TooManyConnections or
                    PostgresErrorCodes.AdminShutdown or
                    PostgresErrorCodes.CannotConnectNow:
                case NpgsqlException { IsTransient: true }:
                case TimeoutException:
                    return true;
            }
        }

        return false;
    }
}
