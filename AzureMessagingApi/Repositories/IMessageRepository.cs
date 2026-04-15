using AzureMessagingApi.Models;

namespace AzureMessagingApi.Repositories;

public interface IMessageRepository
{
    Task<Message> CreateAsync(Message message);
    Task<Message?> GetByIdAsync(string id, string recipientId);
    Task<IEnumerable<Message>> GetByRecipientAsync(string recipientId);
    Task<IEnumerable<Message>> GetUnreadByRecipientAsync(string recipientId);
    Task<Message> UpdateAsync(Message message);
}
