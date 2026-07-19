using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// IPC 运行时主机（transport-agnostic）。实现 <see cref="IRpcHost"/>，
/// 在 <see cref="IRpcTransport.MessageReceived"/> 上按 Wails v2 前缀信封分发：
/// <c>C</c> 调用、<c>E</c> 事件、<c>X</c> 通道关闭。结果经
/// <c>window.__lybox.resolve</c> 回推 Promise（弥补 Avalonia WebView 的 fire-and-forget 缺陷）。
/// </summary>
public sealed class WebViewIpcHost : IRpcHost
{
    private readonly IRpcTransport _transport;
    private readonly ConcurrentDictionary<string, RpcCommandHandler> _commands = new();
    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private readonly ConcurrentDictionary<string, List<Action<JsonElement?>>> _eventListeners = new();
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _bootstrapJs;
    private bool _bootstrapInjected;

    private const string ReadyEvent = "__lybox:ready";

    public WebViewIpcHost(IRpcTransport transport)
    {
        _transport = transport;
        _transport.MessageReceived += OnMessage;
        _bootstrapJs = LoadBootstrap();
    }

    /// <summary>前端 __lybox 运行时就绪（握手完成）。注入引导脚本后等待此任务完成。</summary>
    public Task WhenReady => _readyTcs.Task;

    /// <summary>注入 ipc.js 引导脚本到页面。幂等。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_bootstrapInjected) return;
        await _transport.ExecuteScriptAsync(_bootstrapJs, cancellationToken);
        _bootstrapInjected = true;
    }

    /// <summary>把已注册命令的清单注入页面，构建 window.go 绑定。调用前应完成所有 RegisterCommand。</summary>
    public async Task InjectBindingsAsync(CancellationToken cancellationToken = default)
    {
        var manifest = _commands.Keys.Select(id => new { id, name = id.Substring(id.LastIndexOf('.') + 1) });
        var json = JsonSerializer.Serialize(manifest, RpcEnvelope.JsonOptions);
        // JS 端 setBindings 接收 JSON 字符串
        var js = $"window.__lybox && window.__lybox.setBindings({JsonSerializer.Serialize(json)});";
        await _transport.ExecuteScriptAsync(js, cancellationToken);
    }

    public void RegisterCommand(string name, RpcCommandHandler handler)
        => _commands[name] = handler;

    public async Task EmitEventAsync(string name, object? data, CancellationToken cancellationToken = default)
    {
        var dataJson = data is null ? "null" : JsonSerializer.Serialize(data, RpcEnvelope.JsonOptions);
        var js = $"window.__lybox && window.__lybox.dispatch({JsonSerializer.Serialize(name)},{dataJson});";
        await _transport.ExecuteScriptAsync(js, cancellationToken);
    }

    public Channel<T> CreateChannel<T>(string? id = null)
    {
        var cid = id ?? Guid.NewGuid().ToString("N");
        var ch = new Channel<T>(cid, _transport);
        _channels[cid] = ch;
        return ch;
    }

    /// <summary>订阅来自前端的事件（JS 经 __lybox.emit 发送）。</summary>
    public Action OnEvent(string name, Action<JsonElement?> handler)
    {
        var list = _eventListeners.GetOrAdd(name, _ => new List<Action<JsonElement?>>());
        lock (list) list.Add(handler);
        return () => { lock (list) list.Remove(handler); };
    }

    private void OnMessage(string? body)
    {
        if (string.IsNullOrEmpty(body)) return;
        var prefix = body[0];
        var payload = body.Substring(1);
        switch (prefix)
        {
            case RpcEnvelope.PrefixCall:
                _ = HandleCallAsync(payload);
                break;
            case RpcEnvelope.PrefixEvent:
                HandleEvent(payload);
                break;
            case RpcEnvelope.PrefixChannelClose:
                HandleChannelClose(payload);
                break;
        }
    }

    private async Task HandleCallAsync(string payload)
    {
        CallMessage msg;
        try { msg = JsonSerializer.Deserialize<CallMessage>(payload, RpcEnvelope.JsonOptions)!; }
        catch (Exception) { return; /* 无法解析的调用，丢弃 */ }

        if (msg.Name == ReadyEvent) return; // 握手由事件路径处理

        if (!_commands.TryGetValue(msg.Name, out var handler))
        {
            await ResolveAsync(msg.CallbackId, $"命令未注册: {msg.Name}", null);
            return;
        }

        try
        {
            var result = await handler(msg.Args, CancellationToken.None);
            if (result is Channel ch)
            {
                var itemType = ch.GetType().IsGenericType
                    ? ch.GetType().GetGenericArguments()[0].Name
                    : "any";
                var descriptor = new { __channel = true, id = ch.Id, itemType };
                await ResolveAsync(msg.CallbackId, null, descriptor);
            }
            else
            {
                await ResolveAsync(msg.CallbackId, null, result);
            }
        }
        catch (Exception ex)
        {
            await ResolveAsync(msg.CallbackId, ex.Message, null);
        }
    }

    private void HandleEvent(string payload)
    {
        EventMessage? msg;
        try { msg = JsonSerializer.Deserialize<EventMessage>(payload, RpcEnvelope.JsonOptions); }
        catch { return; }
        if (msg is null) return;

        if (msg.Name == ReadyEvent)
        {
            _readyTcs.TrySetResult();
            return;
        }

        if (_eventListeners.TryGetValue(msg.Name, out var list))
        {
            List<Action<JsonElement?>> snapshot;
            lock (list) snapshot = list.ToList();
            foreach (var cb in snapshot)
            {
                try { cb(msg.Data); } catch { /* 单个监听器异常不影响其他 */ }
            }
        }
    }

    private void HandleChannelClose(string channelId)
    {
        if (_channels.TryRemove(channelId, out var ch))
        {
            _ = ch.CloseAsync();
        }
    }

    private async Task ResolveAsync(string callbackId, string? err, object? result)
    {
        var errJson = err is null ? "null" : JsonSerializer.Serialize(err, RpcEnvelope.JsonOptions);
        var resultJson = result is null ? "null" : JsonSerializer.Serialize(result, RpcEnvelope.JsonOptions);
        var js = $"window.__lybox && window.__lybox.resolve({JsonSerializer.Serialize(callbackId)},{errJson},{resultJson});";
        try { await _transport.ExecuteScriptAsync(js); }
        catch { /* 页面已销毁等，忽略 */ }
    }

    private static string LoadBootstrap()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("ipc.js", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("找不到嵌入式 ipc.js 资源");
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}