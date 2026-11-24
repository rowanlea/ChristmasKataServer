namespace WebAPI.Scoring;

public static class RenderLayout
{
    public const int Canvas = 1200;
    private const double OriginX = 600.0;
    private const double OriginY = 120.0;
    private const double SpanX = 1000.0;
    private const double RowRise = 0.866;
    private const double SampleRadiusK = 0.14;

    public static double Step(int n) => n > 1 ? SpanX / (n - 1) : SpanX;

    public static (double x, double y) Centre(int n, int r, int p)
    {
        var h = Step(n);
        return (OriginX + (p - r / 2.0) * h, OriginY + r * RowRise * h);
    }

    public static double SampleRadius(int n) => SampleRadiusK * Step(n);
}
