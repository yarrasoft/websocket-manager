using System;

namespace WebSocketManager.Common
{
    public static class MessageBriefConstants
    {
        public const string Ping = "ping";
        public const string Pong = "pong";
        public const string Connect = "connect";
        public const string Disconnect = "disconnect";
    }

    public enum MessageType
    {
        Text = 1,
        ClientMethodInvocation = 2,
        ConnectionEvent = 3,
    }

    public class Message
    {
        public MessageType MessageType { get; set; }

        /// <summary>
        /// The brief of the message, should effectively be a constant and able to be used for decision making on either end
        /// </summary>
        public string Brief { get; set; }
        public string Data { get; set; }
    }
}