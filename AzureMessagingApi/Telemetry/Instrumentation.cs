using System.Diagnostics;

namespace AzureMessagingApi.Telemetry;

public static class Instrumentation
{
    public const string ServiceName = "AzureMessagingApi";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
}
