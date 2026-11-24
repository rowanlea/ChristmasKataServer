using WebAPI.Service;

namespace WebAPI.API;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IParticipantRepository repository)
    {
        var endpoint = context.GetEndpoint();
        
        if (endpoint?.Metadata.GetMetadata<SkipTeamValidationAttribute>() != null)
        {
            await _next(context);
            return;
        }

        var teamName = context.Request.Headers["X-Team-Name"].FirstOrDefault();
        var teamSecret = context.Request.Headers["X-Unique-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(teamName) || string.IsNullOrWhiteSpace(teamSecret))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing team credentials" });
            return;
        }

        var exists =  await repository.ParticipantExists(teamName, teamSecret);

        if (!exists)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid team credentials" });
            return;
        }

        await _next(context);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class SkipTeamValidationAttribute : Attribute { }