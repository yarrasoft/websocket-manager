using Microsoft.Extensions.Logging;
using WebSocketManager;

namespace MvcSample.MessageHandlers
{
    public class NotificationsMessageHandler : WebSocketHandler
    {
        public NotificationsMessageHandler(WebSocketConnectionManager webSocketConnectionManager, ILogger<NotificationsMessageHandler> logger) : base(webSocketConnectionManager, logger)
        {
        }
    }
}