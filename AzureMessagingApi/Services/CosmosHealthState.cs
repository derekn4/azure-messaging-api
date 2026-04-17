namespace AzureMessagingApi.Services;

public class CosmosHealthState
{
    public bool IsReady { get; set; }
    public string? LastError { get; set; }
    public DateTime? ReadyAt { get; set; }
}
