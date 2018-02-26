using System;

namespace WebSocketManager.Common
{
    public enum MessageType
    {
        Text = 1,
        ClientMethodInvocation = 2,
        ConnectionEvent = 3,
        Binary = 4,
        Ping = 5,
        Pong = 6,
    }

    public class Message
    {
        public MessageType MessageType { get; set; }
    }


    public abstract class Message<T> : Message
    {
        public T Data { get; set; }
    }

    public class TextMessage : Message<string>
    {
        public TextMessage()
        {
            this.MessageType = MessageType.Text;
        }
    }

    public class BinaryMessage : Message<byte[]>
    {
        public BinaryMessage()
        {
            this.MessageType = MessageType.Binary;
        }
    }

    public class MethodInvokationMessage : Message<string>
    {
        public MethodInvokationMessage()
        {
            this.MessageType = MessageType.ClientMethodInvocation;
        }
    }
}