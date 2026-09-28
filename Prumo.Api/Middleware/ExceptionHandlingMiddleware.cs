using System.Net;
using System.Text.Json;
using FluentValidation;
using Prumo.Domain.Common;

namespace Prumo.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = context.Response;

            var errorResponse = new ErrorResponse
            {
                Success = false,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case ValidationException validationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Validation error";
                    errorResponse.Errors = validationException.Errors
                        .Select(e => new ErrorDetail
                        {
                            Field = e.PropertyName,
                            Message = e.ErrorMessage
                        })
                        .ToList();
                    _logger.LogWarning("Validation error: {Errors}", 
                        string.Join(", ", errorResponse.Errors.Select(e => $"{e.Field}: {e.Message}")));
                    break;

                case KeyNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = exception.Message;
                    _logger.LogWarning("Not found: {Message}", exception.Message);
                    break;

                // Before UnauthorizedAccessException: the SPA needs to tell this case apart
                // from "invalid credentials", and the generic 401 would drop the message.
                case EmailNotConfirmedException:
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    errorResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                    errorResponse.Message = exception.Message;
                    errorResponse.Code = EmailNotConfirmedException.Code;
                    _logger.LogInformation("Login refused: e-mail not confirmed.");
                    break;

                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Unauthorized";
                    _logger.LogWarning("Unauthorized access: {Message}", exception.Message);
                    break;

                case InvalidOperationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = exception.Message;
                    _logger.LogWarning("Invalid operation: {Message}", exception.Message);
                    break;

                // ArgumentNullException derives from ArgumentException, but a null argument
                // is our bug and not the caller's input — it falls through to the 500 below.
                case ArgumentException argumentException when exception is not ArgumentNullException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = WithoutParameterName(argumentException);
                    _logger.LogWarning("Invalid argument: {Message}", exception.Message);
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "Internal server error";
                    _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                    break;
            }

            var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await response.WriteAsync(result);
        }

        // ArgumentException appends "(Parameter 'x')" to Message. That names our own
        // method signature, which means nothing to whoever is filling in the form.
        private static string WithoutParameterName(ArgumentException exception) =>
            exception.ParamName is null
                ? exception.Message
                : exception.Message.Replace($" (Parameter '{exception.ParamName}')", string.Empty);
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }

        /// <summary>
        /// A stable code the client can branch on without depending on the message text.
        /// Set only where the distinction matters; null is the normal case.
        /// </summary>
        public string? Code { get; set; }

        public string Message { get; set; } = string.Empty;
        public List<ErrorDetail>? Errors { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ErrorDetail
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
