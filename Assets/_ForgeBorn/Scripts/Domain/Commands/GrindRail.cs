namespace ForgeBorn.Domain.Commands
{
    /// <summary>
    /// Where the grind guide line and the "strong" assist hold the pointer: a little below the edge tip. With a set angle the bevel no longer
    /// depends on this distance (it only has to be inside the wheel footprint and the facet zone), so one value serves every wheel.
    /// </summary>
    public static class GrindRail
    {
        public const double OffsetMeters = 0.002;

        public static double OffsetM(AbrasiveWheel wheel) { return OffsetMeters; }
    }
}
