using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ASG.Api.Services.Bot
{
    public class OneBotClient : IAsyncDisposable
    {
        private readonly Uri _uri;
        private readonly string? _accessToken;
        private ClientWebSocket? _ws;
        public event Func<JsonObject, Task>? OnEvent;
        public OneBotClient(string url, string? accessToken)
        {
            _uri = new Uri(url);
            _accessToken = accessToken;
        }
        public async Task ConnectAsync(CancellationToken ct)
        {
            _ws = new ClientWebSocket();
            if (!string.IsNullOrEmpty(_accessToken))
            {
                _ws.Options.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            }
            await _ws.ConnectAsync(_uri, ct);
            _ = Task.Run(() => ReceiveLoopAsync(ct));
        }
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buf = new byte[1024 * 64];
            while (_ws != null && _ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var ms = new MemoryStream();
                WebSocketReceiveResult? result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", ct);
                        return;
                    }
                    ms.Write(buf, 0, result.Count);
                } while (!result.EndOfMessage);
                ms.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(ms, Encoding.UTF8);
                var text = await reader.ReadToEndAsync();
                try
                {
                    var node = JsonNode.Parse(text) as JsonObject;
                    if (node != null)
                    {
                        var handler = OnEvent;
                        if (handler != null)
                            await handler(node);
                    }
                }
                catch { }
            }
        }
        private Task SendAsync(JsonObject obj, CancellationToken ct)
        {
            var json = obj.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var bytes = Encoding.UTF8.GetBytes(json);
            return _ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
        public Task SendGroupMessageAsync(long groupId, string message, CancellationToken ct)
        {
            var obj = new JsonObject
            {
                ["action"] = "send_group_msg",
                ["params"] = new JsonObject
                {
                    ["group_id"] = groupId,
                    ["message"] = message
                },
                ["echo"] = Guid.NewGuid().ToString()
            };
            return SendAsync(obj, ct);
        }
        public Task SendPrivateMessageAsync(long userId, string message, CancellationToken ct)
        {
            var obj = new JsonObject
            {
                ["action"] = "send_private_msg",
                ["params"] = new JsonObject
                {
                    ["user_id"] = userId,
                    ["message"] = message
                },
                ["echo"] = Guid.NewGuid().ToString()
            };
            return SendAsync(obj, ct);
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_ws != null)
                {
                    if (_ws.State == WebSocketState.Open)
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None);
                    _ws.Dispose();
                }
            }
            catch { }
        }
    }
}
