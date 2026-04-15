using Microsoft.AspNetCore.Mvc;
using AzureMessagingApi.Models;
using AzureMessagingApi.Services;

namespace AzureMessagingApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messageService;

    public MessagesController(MessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpPost]
    public async Task<ActionResult<Message>> SendMessage([FromBody] SendMessageRequest request)
    {
        var message = await _messageService.SendMessageAsync(request);
        return CreatedAtAction(nameof(GetMessages), new { userId = message.RecipientId }, message);
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<IEnumerable<Message>>> GetMessages(string userId)
    {
        var messages = await _messageService.GetMessagesAsync(userId);
        return Ok(messages);
    }

    [HttpGet("{userId}/unread")]
    public async Task<ActionResult<IEnumerable<Message>>> GetUnreadMessages(string userId)
    {
        var messages = await _messageService.GetUnreadMessagesAsync(userId);
        return Ok(messages);
    }

    [HttpPatch("{id}/read")]
    public async Task<ActionResult<Message>> MarkAsRead(string id, [FromQuery] string recipientId)
    {
        var message = await _messageService.MarkAsReadAsync(id, recipientId);
        if (message == null) return NotFound();
        return Ok(message);
    }
}
