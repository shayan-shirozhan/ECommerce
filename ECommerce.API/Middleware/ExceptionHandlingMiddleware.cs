using ECommerce.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Domain.Exceptions;
using FluentValidation;

namespace ECommerce.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Validation failed.",
                exception.Message,
                context.RequestAborted);
        }
        catch (NotFoundException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status404NotFound,
                "Resource not found.",
                exception.Message,
                context.RequestAborted);
        }
        catch (DomainException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                "Business rule violation.",
                exception.Message,
                context.RequestAborted);
        }
        catch (BusinessRuleException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                "Business rule violation.",
                exception.Message,
                context.RequestAborted);
        }
        catch (ConcurrencyConflictException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                "Concurrency conflict.",
                exception.Message,
                context.RequestAborted);
        }
        catch (PersistenceConflictException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                "Persistence conflict.",
                exception.Message,
                context.RequestAborted);
        }
        catch (UnauthorizedException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized.",
                exception.Message,
                context.RequestAborted);
        }
        catch (ForbiddenException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden.",
                exception.Message,
                context.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unhandled exception occurred.",
                "An unhandled exception occurred.",
                context.RequestAborted);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, int statusCode, string title, string detail,
        CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }
}
