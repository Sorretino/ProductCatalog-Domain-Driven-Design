using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.Contracts;
using ProductCatalog.Application.Exceptions;
using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Api.Middleware;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (status, title, detail) = exception switch
        {
            ValidationException => (400, "Dados inválidos", "Revise os campos informados."),
            DomainException domain => (400, "Regra de domínio violada", domain.Message),
            ProductNotFoundException missing => (404, "Produto não encontrado", missing.Message),
            ProductConcurrencyException conflict => (412, "Versão desatualizada", conflict.Message),
            PreconditionException precondition => (precondition.StatusCode, "Pré-condição inválida", precondition.Message),
            _ => (500, "Erro interno", "Não foi possível concluir a operação.")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled error. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }

        ProblemDetails problem = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).Distinct().ToArray()))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
