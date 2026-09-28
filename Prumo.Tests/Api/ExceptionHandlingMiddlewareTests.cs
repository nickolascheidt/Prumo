using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Prumo.Api.Middleware;

namespace Prumo.Tests.Api
{
    public class ExceptionHandlingMiddlewareTests
    {
        private static async Task<(int StatusCode, string Message)> InvokeThrowing(Exception exception)
        {
            var middleware = new ExceptionHandlingMiddleware(
                _ => throw exception,
                NullLogger<ExceptionHandlingMiddleware>.Instance);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var message = JsonDocument.Parse(body).RootElement.GetProperty("message").GetString();

            return (context.Response.StatusCode, message!);
        }

        [Fact]
        public async Task ArgumentException_becomes_400_carrying_the_message()
        {
            var (statusCode, message) = await InvokeThrowing(new ArgumentException("CPF is required."));

            Assert.Equal(400, statusCode);
            Assert.Equal("CPF is required.", message);
        }

        [Fact]
        public async Task ArgumentException_with_a_parameter_name_does_not_leak_it_to_the_client()
        {
            // ArgumentException appends "(Parameter 'x')" to Message. That is internal
            // detail, not something to show a user filling in a form.
            var (statusCode, message) = await InvokeThrowing(
                new ArgumentException("Category name is required.", "request"));

            Assert.Equal(400, statusCode);
            Assert.Equal("Category name is required.", message);
        }

        [Fact]
        public async Task ArgumentNullException_stays_500_because_a_null_argument_is_our_bug()
        {
            var (statusCode, message) = await InvokeThrowing(new ArgumentNullException("request"));

            Assert.Equal(500, statusCode);
            Assert.Equal("Internal server error", message);
        }
    }
}
