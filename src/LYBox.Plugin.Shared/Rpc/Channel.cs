using System.Text.Json;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// 流式通道（借鉴 Tauri Channel，弥补 Wails 无原生流式）。
/// C# 侧创建并拥有，通过 <see cref="IRpcHost.CreateChannel{T}"/> 取得。
/// 每次 <see cref="WriteAsync"/> 把数据推送到前端
/// （经 <c>window.__lybox.channel.onData(id, json)</c>）。
/// 单订阅、自动取消：<see cref="Dispose"/> 时通知前端 onClose。
/// </summary>
public sealed class Channel<T> : Channel, IAsyncDisposable
{
    private readonly IRpcTransport _transport;
    private readonly CancellationTokenSource _cts = new();

    internal Channel(string id, IRpcTransport transport) : base(id)
    {
        _transport = transport;
    }

    /// <summary>向前端推送一条数据。</summary>
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        if (_cts.IsCancellationRequested) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var json = JsonSerializer.Serialize(item, RpcEnvelope.JsonOptions);
        var js = $"window.__lybox.channel.onData({JsonSerializer.Serialize(Id)},{json});";
        try { await _transport.ExecuteScriptAsync(js, linked.Token); }
        catch (OperationCanceledException) { }
    }

    /// <summary>关闭通道并通知前端。</summary>
    public override async ValueTask CloseAsync()
    {
        if (Closed) return;
        _cts.Cancel();
        var js = $"window.__lybox.channel.onClose({JsonSerializer.Serialize(Id)});";
        try { await _transport.ExecuteScriptAsync(js, CancellationToken.None); }
        catch { }
        MarkClosed();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _cts.Dispose();
    }
}

/// <summary>非泛型基类。</summary>
public abstract class Channel
{
    /// <summary>通道唯一标识，前端凭此订阅。</summary>
    public string Id { get; }
    /// <summary>是否已关闭。</summary>
    public bool Closed { get; private set; }

    protected Channel(string id) => Id = id;

    protected void MarkClosed() => Closed = true;

    /// <summary>关闭通道（派生类实现）。</summary>
    public abstract ValueTask CloseAsync();
}
