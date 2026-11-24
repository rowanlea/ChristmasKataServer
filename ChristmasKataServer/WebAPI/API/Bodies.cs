namespace WebAPI.API;

public static class Bodies
{
    public static async Task<byte[]?> ReadBounded(HttpRequest request, int limit, CancellationToken cancellationToken)
    {
        if (request.ContentLength > limit) return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    public static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

    public static string Team(HttpContext context) => context.Request.Headers["X-Team-Name"].ToString();
}
