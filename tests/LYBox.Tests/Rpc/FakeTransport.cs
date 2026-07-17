using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Tests.Rpc;

/// <summary>
/// 测试用 IRpcTransport：捕获宿主执行的 JS（模拟 C#→JS），
/// 并允许测试模拟 JS→C# 消息。无需真实 WebView 控件。
/// </summary>
internal sealed class FakeTransport : IRpcTransport
{
    public event Action<string?>? MessageReceived;

    /// <summary>宿主经 ExecuteScriptAsync 发出的 JS 表达式（按顺序）。</summary>
    public List<string> ExecutedScripts { get; } = new();

    public Task<string?> ExecuteScriptAsync(string javaScript, CancellationToken cancellationToken = default)
    {
        ExecutedScripts.Add(javaScript);
        return Task.FromResult<string?>(null);
    }

    /// <summary>模拟前端经 invokeCSharpAction 发来的消息。</summary>
    public void SimulateFromScript(string? body) => MessageReceived?.Invoke(body);
}
