using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PricingExceptions.API.Observability;

// Instrumentation hooks only; production exporters and dashboards are deployment choices.
public static class PricingExceptionsTelemetry
{
    public const string Name = "BlueHarvest.PricingExceptions";
    public static readonly ActivitySource Activities = new(Name);
    private static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Mutations = Meter.CreateCounter<long>("pricing_exceptions.mutations");
    public static readonly Counter<long> Replays = Meter.CreateCounter<long>("pricing_exceptions.replays");
    public static readonly Counter<long> Conflicts = Meter.CreateCounter<long>("pricing_exceptions.conflicts");
}
