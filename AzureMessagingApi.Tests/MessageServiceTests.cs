using AzureMessagingApi.Models;
using AzureMessagingApi.Repositories;
using AzureMessagingApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AzureMessagingApi.Tests;

public class MessageServiceTests
{
    private readonly Mock<IMessageRepository> _repository = new();
    private readonly Mock<IMessagePublisher> _publisher = new();
    private readonly MessageService _sut;

    public MessageServiceTests()
    {
        _sut = new MessageService(_repository.Object, _publisher.Object, NullLogger<MessageService>.Instance);
    }

    [Fact]
    public async Task SendMessageAsync_WritesToRepositoryBeforePublishing()
    {
        var request = new SendMessageRequest { SenderId = "alice", RecipientId = "bob", Content = "hi" };
        var created = new Message { Id = "m-1", SenderId = "alice", RecipientId = "bob", Content = "hi", Status = "pending" };
        _repository.Setup(r => r.CreateAsync(It.IsAny<Message>())).ReturnsAsync(created);

        var sequence = new MockSequence();
        _repository.InSequence(sequence).Setup(r => r.CreateAsync(It.IsAny<Message>())).ReturnsAsync(created);
        _publisher.InSequence(sequence).Setup(p => p.PublishAsync(created, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.SendMessageAsync(request);

        Assert.Same(created, result);
        _repository.Verify(r => r.CreateAsync(It.Is<Message>(m =>
            m.SenderId == "alice" && m.RecipientId == "bob" && m.Content == "hi" && m.Status == "pending")), Times.Once);
        _publisher.Verify(p => p.PublishAsync(created, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_DoesNotPublish_WhenRepositoryThrows()
    {
        var request = new SendMessageRequest { SenderId = "alice", RecipientId = "bob", Content = "hi" };
        _repository.Setup(r => r.CreateAsync(It.IsAny<Message>())).ThrowsAsync(new InvalidOperationException("cosmos down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SendMessageAsync(request));

        _publisher.Verify(p => p.PublishAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMessagesAsync_DelegatesToRepository()
    {
        var messages = new[] { new Message { Id = "m-1", RecipientId = "bob" } };
        _repository.Setup(r => r.GetByRecipientAsync("bob")).ReturnsAsync(messages);

        var result = await _sut.GetMessagesAsync("bob");

        Assert.Equal(messages, result);
        _repository.Verify(r => r.GetByRecipientAsync("bob"), Times.Once);
    }

    [Fact]
    public async Task GetUnreadMessagesAsync_DelegatesToRepository()
    {
        var messages = new[] { new Message { Id = "m-1", RecipientId = "bob", Status = "delivered" } };
        _repository.Setup(r => r.GetUnreadByRecipientAsync("bob")).ReturnsAsync(messages);

        var result = await _sut.GetUnreadMessagesAsync("bob");

        Assert.Equal(messages, result);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsNull_WhenMessageNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync("missing", "bob")).ReturnsAsync((Message?)null);

        var result = await _sut.MarkAsReadAsync("missing", "bob");

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Message>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesStatusToRead_WhenMessageExists()
    {
        var stored = new Message { Id = "m-1", RecipientId = "bob", Status = "delivered" };
        _repository.Setup(r => r.GetByIdAsync("m-1", "bob")).ReturnsAsync(stored);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Message>())).ReturnsAsync((Message m) => m);

        var result = await _sut.MarkAsReadAsync("m-1", "bob");

        Assert.NotNull(result);
        Assert.Equal("read", result!.Status);
        _repository.Verify(r => r.UpdateAsync(It.Is<Message>(m => m.Id == "m-1" && m.Status == "read")), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_PassesRecipientIdAsPartitionKey()
    {
        // Regression guard: recipientId must be threaded through as partition key.
        // Omitting it forces a cross-partition scan on Cosmos, which we specifically designed around.
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((Message?)null);

        await _sut.MarkAsReadAsync("m-1", "bob");

        _repository.Verify(r => r.GetByIdAsync("m-1", "bob"), Times.Once);
    }
}
