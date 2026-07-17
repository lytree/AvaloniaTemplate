using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LYBox.Plugin.Shared.Rpc;
using LYBox.WebView.Ipc;
using Xunit;

namespace LYBox.Tests.Rpc;

/// <summary>
/// WebViewIpcHost 单元测试：用 FakeTransport 验证 Wails 前缀信封分发（C/E/X）、
/// Promise 回推、Channel 流式、事件双向、握手与绑定注入。不依赖真实 WebView 控件。
/// </summary>
public class WebViewIpcHostTests
{
    // —— 握手 __lybox:ready ——

    [Fact]
    public async Task WhenReady_未收到_ready_事件前不完成()
    {
        var (host, _) = Create();
        var task = host.WhenReady;

        // 给调度器一点时间，确认未被意外完成
        await Task.Delay(50);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public async Task WhenReady_收到_ready_事件后完成()
    {
        var (host, transport) = Create();
        var readyPayload = "E" + JsonSerializer.Serialize(new EventMessage { Name = "__lybox:ready" });

        transport.SimulateFromScript(readyPayload);

        await host.WhenReady.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(host.WhenReady.IsCompleted);
    }

    [Fact]
    public async Task WhenReady_幂等_重复_ready_不抛()
    {
        var (host, transport) = Create();
        var readyPayload = "E" + JsonSerializer.Serialize(new EventMessage { Name = "__lybox:ready" });

        transport.SimulateFromScript(readyPayload);
        await host.WhenReady.WaitAsync(TimeSpan.FromSeconds(2));

        // 第二次应无副作用
        transport.SimulateFromScript(readyPayload);
        Assert.True(host.WhenReady.IsCompleted);
    }

    // —— 命令分发 (C 前缀) ——

    [Fact]
    public async Task Call_已注册命令_回推结果()
    {
        var (host, transport) = Create();
        var argsCaptured = new List<JsonElement>();
        host.RegisterCommand("svc.echo", (args, ct) =>
        {
            argsCaptured.AddRange(args);
            return Task.FromResult<object?>(new { ok = true, echoed = true });
        });

        var payload = "C" + JsonSerializer.Serialize(new CallMessage
        {
            Name = "svc.echo",
            Args = new[]
            {
                JsonDocument.Parse("42").RootElement.Clone(),
                JsonDocument.Parse("\"hello\"").RootElement.Clone(),
            },
            CallbackId = "cb-1"
        });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => transport.ExecutedScripts.Any(s => s.Contains("resolve") && s.Contains("cb-1")));

        Assert.Equal(2, argsCaptured.Count);
        Assert.Equal(JsonValueKind.Number, argsCaptured[0].ValueKind);
        Assert.Equal(42, argsCaptured[0].GetInt32());
        Assert.Equal(JsonValueKind.String, argsCaptured[1].ValueKind);
        Assert.Equal("hello", argsCaptured[1].GetString());

        var resolveJs = transport.ExecutedScripts.Single(s => s.Contains("resolve") && s.Contains("cb-1"));
        Assert.Contains("\"ok\":true", resolveJs);
        Assert.Contains("\"echoed\":true", resolveJs);
        Assert.Contains("null", resolveJs); // err 为 null
    }

    [Fact]
    public async Task Call_未注册命令_回推错误()
    {
        var (host, transport) = Create();

        var payload = "C" + JsonSerializer.Serialize(new CallMessage
        {
            Name = "svc.missing",
            Args = Array.Empty<JsonElement>(),
            CallbackId = "cb-2"
        });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => transport.ExecutedScripts.Any(s => s.Contains("resolve") && s.Contains("cb-2")));

        var resolveJs = transport.ExecutedScripts.Single(s => s.Contains("resolve") && s.Contains("cb-2"));
        Assert.Contains("命令未注册", resolveJs);
        Assert.Contains("svc.missing", resolveJs);
    }

    [Fact]
    public async Task Call_处理器抛异常_回推错误消息()
    {
        var (host, transport) = Create();
        host.RegisterCommand("svc.throw", (_, __) => throw new InvalidOperationException("boom-from-handler"));

        var payload = "C" + JsonSerializer.Serialize(new CallMessage
        {
            Name = "svc.throw",
            CallbackId = "cb-3"
        });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => transport.ExecutedScripts.Any(s => s.Contains("resolve") && s.Contains("cb-3")));

        var resolveJs = transport.ExecutedScripts.Single(s => s.Contains("resolve") && s.Contains("cb-3"));
        Assert.Contains("boom-from-handler", resolveJs);
    }

    [Fact]
    public async Task Call_返回_Channel_回推通道描述符()
    {
        var (host, transport) = Create();
        Channel<int>? captured = null;
        host.RegisterCommand("svc.stream", (_, __) =>
        {
            captured = host.CreateChannel<int>("ch-fixed");
            return Task.FromResult<object?>(captured);
        });

        var payload = "C" + JsonSerializer.Serialize(new CallMessage
        {
            Name = "svc.stream",
            CallbackId = "cb-4"
        });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => transport.ExecutedScripts.Any(s => s.Contains("resolve") && s.Contains("cb-4")));

        Assert.NotNull(captured);
        var resolveJs = transport.ExecutedScripts.Single(s => s.Contains("resolve") && s.Contains("cb-4"));
        Assert.Contains("__channel", resolveJs);
        Assert.Contains("ch-fixed", resolveJs);
        // itemType 为泛型参数类型名
        Assert.Contains("Int32", resolveJs);
    }

    [Fact]
    public async Task Call_无法解析的_JSON_静默丢弃_不回推()
    {
        var (host, transport) = Create();
        host.RegisterCommand("svc.any", (_, __) => Task.FromResult<object?>(1));

        transport.SimulateFromScript("C{这不是合法JSON");
        await Task.Delay(50);

        // 无任何 resolve 脚本被发送
        Assert.DoesNotContain(transport.ExecutedScripts, s => s.Contains("resolve"));
    }

    // —— 事件 (E 前缀) ——

    [Fact]
    public async Task EmitEventAsync_C_到_JS_经_dispatch()
    {
        var (host, transport) = Create();

        await host.EmitEventAsync("app.tick", new { count = 7 });

        Assert.NotEmpty(transport.ExecutedScripts);
        var js = transport.ExecutedScripts[^1];
        Assert.Contains("window.__lybox", js);
        Assert.Contains("dispatch", js);
        Assert.Contains("app.tick", js);
        Assert.Contains("\"count\":7", js);
    }

    [Fact]
    public async Task EmitEventAsync_null_data_发送_null()
    {
        var (host, transport) = Create();

        await host.EmitEventAsync("app.empty", null);

        var js = transport.ExecutedScripts[^1];
        Assert.Contains("dispatch", js);
        Assert.Contains("null", js);
    }

    [Fact]
    public async Task JS_emit_触发_OnEvent_监听器()
    {
        var (host, transport) = Create();
        JsonElement? received = null;
        host.OnEvent("user.click", data => received = data);

        var payload = "E" + JsonSerializer.Serialize(new EventMessage
        {
            Name = "user.click",
            Data = JsonDocument.Parse("{\"x\":10,\"y\":20}").RootElement.Clone()
        });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => received is not null);
        Assert.NotNull(received);
        Assert.Equal(JsonValueKind.Object, received!.Value.ValueKind);
        Assert.Equal(10, received.Value.GetProperty("x").GetInt32());
        Assert.Equal(20, received.Value.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task OnEvent_取消订阅_不再触发()
    {
        var (host, transport) = Create();
        var callCount = 0;
        var unsub = host.OnEvent("app.tick", _ => callCount++);

        var payload = "E" + JsonSerializer.Serialize(new EventMessage { Name = "app.tick" });
        transport.SimulateFromScript(payload);
        await WaitForAsync(() => callCount == 1);
        Assert.Equal(1, callCount);

        unsub();
        transport.SimulateFromScript(payload);
        await Task.Delay(50);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task OnEvent_多个监听器_均触发()
    {
        var (host, transport) = Create();
        var a = 0;
        var b = 0;
        host.OnEvent("app.multi", _ => a++);
        host.OnEvent("app.multi", _ => b++);

        var payload = "E" + JsonSerializer.Serialize(new EventMessage { Name = "app.multi" });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => a == 1 && b == 1);
        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public async Task OnEvent_单监听器异常_不影响其他监听器()
    {
        var (host, transport) = Create();
        var secondCalled = false;
        host.OnEvent("app.safe", _ => throw new InvalidOperationException("listener boom"));
        host.OnEvent("app.safe", _ => secondCalled = true);

        var payload = "E" + JsonSerializer.Serialize(new EventMessage { Name = "app.safe" });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => secondCalled);
        Assert.True(secondCalled);
    }

    // —— 通道 (Channel<T>) ——

    [Fact]
    public async Task Channel_WriteAsync_推送_onData()
    {
        var (host, transport) = Create();
        var ch = host.CreateChannel<string>("ch-write-1");

        await ch.WriteAsync("hello");
        await ch.WriteAsync("world");

        var dataScripts = transport.ExecutedScripts
            .Where(s => s.Contains("channel.onData") && s.Contains("ch-write-1"))
            .ToList();
        Assert.Equal(2, dataScripts.Count);
        Assert.Contains("hello", dataScripts[0]);
        Assert.Contains("world", dataScripts[1]);
    }

    [Fact]
    public async Task Channel_CloseAsync_推送_onClose_并标记关闭()
    {
        var (host, transport) = Create();
        var ch = host.CreateChannel<int>("ch-close-1");

        await ch.CloseAsync();

        Assert.True(ch.Closed);
        Assert.Contains(transport.ExecutedScripts, s => s.Contains("channel.onClose") && s.Contains("ch-close-1"));
    }

    [Fact]
    public async Task Channel_CloseAsync_幂等_重复关闭不重复推送()
    {
        var (host, transport) = Create();
        var ch = host.CreateChannel<int>("ch-close-2");

        await ch.CloseAsync();
        await ch.CloseAsync();
        await ch.CloseAsync();

        var closeScripts = transport.ExecutedScripts.Count(s => s.Contains("channel.onClose") && s.Contains("ch-close-2"));
        Assert.Equal(1, closeScripts);
    }

    [Fact]
    public async Task Channel_关闭后_WriteAsync_静默丢弃()
    {
        var (host, transport) = Create();
        var ch = host.CreateChannel<int>("ch-after-close");

        await ch.CloseAsync();
        var scriptsBefore = transport.ExecutedScripts.Count;
        await ch.WriteAsync(99);

        Assert.Equal(scriptsBefore, transport.ExecutedScripts.Count);
    }

    [Fact]
    public async Task Channel_DisposeAsync_关闭通道()
    {
        var (host, transport) = Create();
        var ch = host.CreateChannel<int>("ch-dispose");

        await ch.DisposeAsync();

        Assert.True(ch.Closed);
        Assert.Contains(transport.ExecutedScripts, s => s.Contains("channel.onClose") && s.Contains("ch-dispose"));
    }

    [Fact]
    public async Task Channel_自动生成_Id_唯一()
    {
        var (host, _) = Create();
        var a = host.CreateChannel<int>();
        var b = host.CreateChannel<int>();

        Assert.NotEmpty(a.Id);
        Assert.NotEmpty(b.Id);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public async Task JS_关闭通道_X_前缀_触发_CloseAsync()
    {
        var (host, transport) = Create();
        var ch = host.CreateChannel<int>("ch-js-close");

        transport.SimulateFromScript("Xch-js-close");

        await WaitForAsync(() => ch.Closed);
        Assert.True(ch.Closed);
        Assert.Contains(transport.ExecutedScripts, s => s.Contains("channel.onClose") && s.Contains("ch-js-close"));
    }

    [Fact]
    public async Task JS_关闭未知通道_无副作用()
    {
        var (host, transport) = Create();
        var scriptsBefore = transport.ExecutedScripts.Count;

        transport.SimulateFromScript("Xnonexistent-channel");
        await Task.Delay(50);

        Assert.Equal(scriptsBefore, transport.ExecutedScripts.Count);
    }

    // —— 绑定注入 ——

    [Fact]
    public async Task InitializeAsync_注入引导脚本_且幂等()
    {
        var (host, transport) = Create();

        await host.InitializeAsync();
        var firstCount = transport.ExecutedScripts.Count;
        Assert.Equal(1, firstCount);
        var bootstrap = transport.ExecutedScripts[0];
        Assert.True(
            bootstrap.Contains("__lybox") || bootstrap.Contains("invokeCSharpAction") || bootstrap.Contains("function"),
            "引导脚本应包含 __lybox 运行时标记");

        await host.InitializeAsync();
        Assert.Equal(firstCount, transport.ExecutedScripts.Count);
    }

    [Fact]
    public async Task InjectBindingsAsync_发送命令清单()
    {
        var (host, transport) = Create();
        host.RegisterCommand("ns.Svc.foo", (_, __) => Task.FromResult<object?>(null));
        host.RegisterCommand("ns.Svc.bar", (_, __) => Task.FromResult<object?>(null));

        await host.InjectBindingsAsync();

        var js = transport.ExecutedScripts[^1];
        Assert.Contains("setBindings", js);
        Assert.Contains("ns.Svc.foo", js);
        Assert.Contains("ns.Svc.bar", js);
    }

    [Fact]
    public async Task InjectBindingsAsync_空命令清单_仍发送空数组()
    {
        var (host, transport) = Create();

        await host.InjectBindingsAsync();

        var js = transport.ExecutedScripts[^1];
        Assert.Contains("setBindings", js);
        // 空清单序列化为 "[]"
        Assert.Contains("[]", js);
    }

    // —— 边界情况 ——

    [Fact]
    public async Task OnMessage_null_忽略()
    {
        var (host, transport) = Create();
        var scriptsBefore = transport.ExecutedScripts.Count;

        transport.SimulateFromScript(null);
        await Task.Delay(50);

        Assert.Equal(scriptsBefore, transport.ExecutedScripts.Count);
    }

    [Fact]
    public async Task OnMessage_空字符串_忽略()
    {
        var (host, transport) = Create();
        var scriptsBefore = transport.ExecutedScripts.Count;

        transport.SimulateFromScript("");
        await Task.Delay(50);

        Assert.Equal(scriptsBefore, transport.ExecutedScripts.Count);
    }

    [Fact]
    public async Task OnMessage_未知前缀_忽略()
    {
        var (host, transport) = Create();
        var scriptsBefore = transport.ExecutedScripts.Count;

        transport.SimulateFromScript("Zsome-payload");
        await Task.Delay(50);

        Assert.Equal(scriptsBefore, transport.ExecutedScripts.Count);
    }

    [Fact]
    public async Task Call_无参数_正常分发()
    {
        var (host, transport) = Create();
        var handlerCalled = false;
        host.RegisterCommand("svc.noparams", (args, ct) =>
        {
            handlerCalled = true;
            Assert.Empty(args);
            return Task.FromResult<object?>(42);
        });

        var payload = "C" + JsonSerializer.Serialize(new CallMessage
        {
            Name = "svc.noparams",
            CallbackId = "cb-noparams"
        });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => transport.ExecutedScripts.Any(s => s.Contains("resolve") && s.Contains("cb-noparams")));
        Assert.True(handlerCalled);
    }

    [Fact]
    public async Task RegisterCommand_同名覆盖_后注册生效()
    {
        var (host, transport) = Create();
        var firstCalled = false;
        var secondCalled = false;

        host.RegisterCommand("svc.override", (_, __) => { firstCalled = true; return Task.FromResult<object?>(1); });
        host.RegisterCommand("svc.override", (_, __) => { secondCalled = true; return Task.FromResult<object?>(2); });

        var payload = "C" + JsonSerializer.Serialize(new CallMessage { Name = "svc.override", CallbackId = "cb-override" });
        transport.SimulateFromScript(payload);

        await WaitForAsync(() => transport.ExecutedScripts.Any(s => s.Contains("resolve") && s.Contains("cb-override")));
        Assert.False(firstCalled);
        Assert.True(secondCalled);
    }

    // —— 辅助 ——

    private static (WebViewIpcHost host, FakeTransport transport) Create()
    {
        var transport = new FakeTransport();
        var host = new WebViewIpcHost(transport);
        return (host, transport);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow + timeout.Value;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException($"等待条件超时（{timeout.Value.TotalSeconds}s）");
    }
}
