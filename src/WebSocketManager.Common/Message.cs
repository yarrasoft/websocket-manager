using System;

namespace WebSocketManager.Common
{
    public enum MessageType
    {
        Text = 1,
        ClientMethodInvocation = 2,
        ConnectionEvent = 3,
        Ping = 5,
        Pong = 6,
    }

    public class Message
    {
        public MessageType MessageType { get; set; }
        public string Data { get; set; }
    }
}