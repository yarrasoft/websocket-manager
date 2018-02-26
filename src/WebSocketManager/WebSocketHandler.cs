using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WebSocketManager.Common;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace WebSocketManager
{
    public abstract class WebSocketHandler : IDisposable
    {

        /*
         * KeepAlive artifacts
        */
        Timer pingTimer;
        ConcurrentDictionary<string, DateTime> socketPingMap = new ConcurrentDictionary<string, DateTime>(2, 1);

private async void OnPingTimer(object state)
        {
            if (SendPingMessages)
            {
                TimeSpan timeoutPeriod = TimeSpan.FromSeconds(WebSocket.DefaultKeepAliveInterval.TotalSeconds * 30);

                foreach (var item in socketPingMap)
                {
                    if (item.Value < DateTime.Now.Subtract(timeoutPeriod))
                    {
                        var socket = WebSocketConnectionManager.GetSocketById(item.Key);
                        if (socket.State == WebSocketState.Open)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.Empty, "timeout", CancellationToken.None);
                        }
                    }
                    else
                    {
                        await SendMessageAsync(item.Key, new TextMessage() { Data = "ping", MessageType = MessageType.Ping });
                    }
                }
            }
        }

        /// <summary>
        /// If true, will send custom "ping" messages which must be answered with an InvokationMessage of type ping and the provided key
        /// Uses WebSocket.DefaultKeepAliveInterval as ping period
        /// Sockets which have not responded to 3 pings will be disconnected
        /// </summary>
        public bool SendPingMessages { get; set; }

        protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }
        private JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public WebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
        {
            WebSocketConnectionManager = webSocketConnectionManager;
            pingTimer = new Timer(OnPingTimer, null, WebSocket.DefaultKeepAliveInterval, WebSocket.DefaultKeepAliveInterval);
        }

        public virtual async Task OnConnected(WebSocket socket)
        {
            WebSocketConnectionManager.AddSocket(socket);

            string id = WebSocketConnectionManager.GetId(socket);

            await SendMessageAsync(socket, new TextMessage()
            {
                MessageType = MessageType.ConnectionEvent,
                Data = id,
            }).ConfigureAwait(false);

            socketPingMap.GetOrAdd(id, DateTime.Now);
        }

        public virtual async Task OnDisconnected(WebSocket socket)
        {
            await WebSocketConnectionManager.RemoveSocket(WebSocketConnectionManager.GetId(socket)).ConfigureAwait(false);
        }

        public async Task SendMessageAsync<T>(WebSocket socket, Message<T> message)
        {
            if (socket.State != WebSocketState.Open)
                return;

            byte[] messageData;

            WebSocketMessageType messageType;

            if (message.MessageType != MessageType.Binary)
            {
                var serializedMessage = JsonConvert.SerializeObject(message, _jsonSerializerSettings);
                messageData = Encoding.UTF8.GetBytes(serializedMessage);
                messageType = WebSocketMessageType.Text;
            }
            else
            {
                messageData = message.Data as byte[];
                messageType = WebSocketMessageType.Binary;
            }

            await socket.SendAsync(buffer: new ArraySegment<byte>(array: messageData,
                                                                  offset: 0,
                                                                  count: messageData.Length),
                                   messageType: messageType,
                                   endOfMessage: true,
                                   cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        
        public async Task SendMessageAsync<T>(string socketId, Message<T> message)
        {
            await SendMessageAsync(WebSocketConnectionManager.GetSocketById(socketId), message).ConfigureAwait(false);
        }

        public async Task SendMessageToAllAsync<T>(Message<T> message)
        {
            foreach (var pair in WebSocketConnectionManager.GetAll())
            {
                if (pair.Value.State == WebSocketState.Open)
                    await SendMessageAsync(pair.Value, message).ConfigureAwait(false);
            }
        }

        public async Task InvokeClientMethodAsync(string socketId, string methodName, object[] arguments)
        {
            var message = new TextMessage()
            {
                MessageType = MessageType.ClientMethodInvocation,
                Data = JsonConvert.SerializeObject(new InvocationDescriptor()
                {
                    MethodName = methodName,
                    Arguments = arguments
                }, _jsonSerializerSettings)
            };

            await SendMessageAsync(socketId, message).ConfigureAwait(false);
        }

        public async Task InvokeClientMethodToAllAsync(string methodName, params object[] arguments)
        {
            foreach (var pair in WebSocketConnectionManager.GetAll())
            {
                if (pair.Value.State == WebSocketState.Open)
                    await InvokeClientMethodAsync(pair.Key, methodName, arguments).ConfigureAwait(false);
            }
        }

        public async Task SendMessageToGroupAsync<T>(string groupID, Message<T> message)
        {
            var sockets = WebSocketConnectionManager.GetAllFromGroup(groupID);
            if (sockets != null)
            {
                foreach (var socket in sockets)
                {
                    await SendMessageAsync(socket, message);
                }
            }
        }

        public async Task SendMessageToGroupAsync<T>(string groupID, Message<T> message, string except)
        {
            var sockets = WebSocketConnectionManager.GetAllFromGroup(groupID);
            if (sockets != null)
            {
                foreach (var id in sockets)
                {
                    if(id != except)
                        await SendMessageAsync(id, message);
                }
            }
        }

        public async Task InvokeClientMethodToGroupAsync(string groupID, string methodName, params object[] arguments)
        {
            var sockets = WebSocketConnectionManager.GetAllFromGroup(groupID);
            if (sockets != null)
            {
                foreach (var id in sockets)
                {
                    await InvokeClientMethodAsync(id, methodName, arguments);
                }
            }
        }

        public async Task InvokeClientMethodToGroupAsync(string groupID, string methodName, string except, params object[] arguments)
        {
            var sockets = WebSocketConnectionManager.GetAllFromGroup(groupID);
            if(sockets != null)
            {
                foreach (var id in sockets)
                {
                    if(id != except)
                        await InvokeClientMethodAsync(id, methodName, arguments);
                }
            }
        }

        public void Pong(string key)
        {

        }

        public async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, string message)
        {
            var messageObject = JsonConvert.DeserializeObject<Message>(message);

            switch (messageObject.MessageType)
            {
                case MessageType.ClientMethodInvocation:

                    var textMessage = JsonConvert.DeserializeObject<TextMessage>(message);


                    var invocationDescriptor = JsonConvert.DeserializeObject<InvocationDescriptor>(textMessage.Data);
                    var method = this.GetType().GetMethod(invocationDescriptor.MethodName);

                    if (method == null)
                    {
                        await SendMessageAsync(socket, new TextMessage()
                        {
                            MessageType = MessageType.Text,
                            Data = $"Cannot find method {invocationDescriptor.MethodName}"
                        }).ConfigureAwait(false);
                        return;
                    }

                    try
                    {
                        method.Invoke(this, invocationDescriptor.Arguments);
                    }
                    catch (TargetParameterCountException)
                    {
                        await SendMessageAsync(socket, new TextMessage()
                        {
                            MessageType = MessageType.Text,
                            Data = $"The {invocationDescriptor.MethodName} method does not take {invocationDescriptor.Arguments.Length} parameters!"
                        }).ConfigureAwait(false);
                    }

                    catch (ArgumentException)
                    {
                        await SendMessageAsync(socket, new TextMessage()
                        {
                            MessageType = MessageType.Text,
                            Data = $"The {invocationDescriptor.MethodName} method takes different arguments!"
                        }).ConfigureAwait(false);
                    }
                    break;

                case MessageType.Ping:
                    //send pong
                    break;

                case MessageType.Pong:
                    this.OnPong(socket);
                    break;

                case MessageType.Text:
                case MessageType.ConnectionEvent:
                case MessageType.Binary:
                default:
                    this.OnMessage(messageObject);
                    break;
            }
        }

        private void OnMessage(Message messageObject)
        {
            throw new NotImplementedException();
        }

        private void OnPong(WebSocket socket)
        {
            string id = WebSocketConnectionManager.GetId(socket);
            socketPingMap[id] = DateTime.Now;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            pingTimer.Dispose();
        }
    }
}