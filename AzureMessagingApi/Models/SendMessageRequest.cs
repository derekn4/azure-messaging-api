namespace AzureMessagingApi.Models;

public class SendMessageRequest
{
    public string SenderId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
