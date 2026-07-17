// LYBox WebView IPC 引导脚本（Wails v2 模型 + Tauri Channel 扩展）
// 由宿主经 ExecuteScriptAsync 注入页面。提供 window.__lybox 运行时与 window.go 绑定入口。
// JS → C#：调用全局 invokeCSharpAction(body)（由 Avalonia WebView 注入），body = 前缀字符 + JSON。
// C# → JS：宿主执行 window.__lybox.resolve / dispatch / channel.onData / channel.onClose。
(function () {
  if (window.__lybox) return;

  var callbacks = new Map();             // callbackId -> {resolve, reject}
  var eventListeners = Object.create(null); // name -> Set<cb>
  var channelListeners = Object.create(null);// id -> Set<cb>

  function send(body) {
    // invokeCSharpAction 由 Avalonia.Controls.WebView 注入；未就绪时降级为 console。
    if (typeof invokeCSharpAction === 'function') {
      invokeCSharpAction(body);
    } else {
      console.error('[__lybox] invokeCSharpAction 不可用，WebView 未就绪');
    }
  }

  // JS → C#：发起 RPC 调用，返回 Promise。
  function invoke(name, args) {
    return new Promise(function (resolve, reject) {
      var id = name + '-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10);
      callbacks.set(id, { resolve: resolve, reject: reject });
      send('C' + JSON.stringify({ name: name, args: args || [], callbackId: id }));
    });
  }

  // C# → JS：回传调用结果（err 为字符串则 reject；result 带 __channel 则包装为 Channel）。
  function resolve(id, err, result) {
    var cb = callbacks.get(id);
    if (!cb) return;
    callbacks.delete(id);
    if (err) {
      cb.reject(new Error(err));
    } else if (result && typeof result === 'object' && result.__channel) {
      cb.resolve(makeChannel(result.id, result.itemType));
    } else {
      cb.resolve(result);
    }
  }

  function makeChannel(id, itemType) {
    return {
      id: id,
      itemType: itemType,
      // 前端订阅：cb(data)。返回取消订阅函数。
      on: function (cb) {
        if (!channelListeners[id]) channelListeners[id] = new Set();
        channelListeners[id].add(cb);
        return function () { (channelListeners[id] || new Set()).delete(cb); };
      },
      // 前端主动关闭通道，通知 C# 释放。
      close: function () { send('X' + id); }
    };
  }

  // C# → JS：推送通道数据。
  function channelOnData(id, data) {
    var set = channelListeners[id];
    if (set) set.forEach(function (cb) { try { cb(data); } catch (e) { console.error(e); } });
  }
  // C# → JS：通道关闭通知。
  function channelOnClose(id) {
    var set = channelListeners[id];
    if (set) set.forEach(function (cb) { try { cb(null, true); } catch (e) { console.error(e); } });
    delete channelListeners[id];
  }

  // —— 事件系统（Wails EventsOn/Emit 语义）——
  function on(name, cb) {
    if (!eventListeners[name]) eventListeners[name] = new Set();
    eventListeners[name].add(cb);
    return function () { (eventListeners[name] || new Set()).delete(cb); };
  }
  function off(name, cb) { (eventListeners[name] || new Set()).delete(cb); }
  // JS → C#：emit
  function emit(name, data) { send('E' + JSON.stringify({ name: name, data: data })); }
  // C# → JS：dispatch
  function dispatch(name, data) {
    var set = eventListeners[name];
    if (set) set.forEach(function (cb) { try { cb(data); } catch (e) { console.error(e); } });
  }

  // 宿主注入绑定清单，构建 window.go.<Ns>.<Class>.<Method>(...args) => Promise
  function setBindings(manifestJson) {
    var list = JSON.parse(manifestJson);
    window.go = window.go || {};
    for (var i = 0; i < list.length; i++) {
      var item = list[i];
      var parts = item.id.split('.');
      var methodName = parts.pop();
      var obj = window.go;
      for (var j = 0; j < parts.length; j++) {
        var p = parts[j];
        obj[p] = obj[p] || {};
        obj = obj[p];
      }
      obj[methodName] = (function (id) {
        return function () { return invoke(id, Array.prototype.slice.call(arguments)); };
      })(item.id);
    }
  }

  window.__lybox = {
    invoke: invoke,
    resolve: resolve,
    on: on,
    off: off,
    emit: emit,
    dispatch: dispatch,
    setBindings: setBindings,
    channel: { on: function (id, cb) { return makeChannel(id, '').on(cb); }, onData: channelOnData, onClose: channelOnClose }
  };

  // 通知宿主运行时就绪（宿主监听 'E' 事件 __lybox:ready 完成握手）。
  send('E' + JSON.stringify({ name: '__lybox:ready', data: null }));
})();
