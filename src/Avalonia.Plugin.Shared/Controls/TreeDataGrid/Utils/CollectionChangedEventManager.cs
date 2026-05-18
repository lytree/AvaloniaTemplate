using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

#nullable enable

namespace Avalonia.Controls.Utils
{
    internal interface ICollectionChangedListener
    {
        void PreChanged(INotifyCollectionChanged sender, NotifyCollectionChangedEventArgs e);
        void Changed(INotifyCollectionChanged sender, NotifyCollectionChangedEventArgs e);
        void PostChanged(INotifyCollectionChanged sender, NotifyCollectionChangedEventArgs e);
    }

    internal class CollectionChangedEventManager
    {
        private readonly ConditionalWeakTable<INotifyCollectionChanged, List<WeakReference<ICollectionChangedListener>>> _entries =
            new();
        private readonly ConditionalWeakTable<INotifyCollectionChanged, NotifyCollectionChangedEventHandler> _handlers =
            new();

        public static CollectionChangedEventManager Instance { get; } = new CollectionChangedEventManager();

        private CollectionChangedEventManager()
        {
        }

        public void AddListener(INotifyCollectionChanged collection, ICollectionChangedListener listener)
        {
            collection = collection ?? throw new ArgumentNullException(nameof(collection));
            listener = listener ?? throw new ArgumentNullException(nameof(listener));
            Dispatcher.UIThread.VerifyAccess();

            if (!_entries.TryGetValue(collection, out var listeners))
            {
                listeners = new List<WeakReference<ICollectionChangedListener>>();
                _entries.Add(collection, listeners);

                NotifyCollectionChangedEventHandler handler = (s, e) => OnNotifyCollectionChanged(s, e);
                collection.CollectionChanged += handler;
                _handlers.Add(collection, handler);
            }

            listeners.Add(new WeakReference<ICollectionChangedListener>(listener));
        }

        public void RemoveListener(INotifyCollectionChanged collection, ICollectionChangedListener listener)
        {
            collection = collection ?? throw new ArgumentNullException(nameof(collection));
            listener = listener ?? throw new ArgumentNullException(nameof(listener));
            Dispatcher.UIThread.VerifyAccess();

            if (_entries.TryGetValue(collection, out var listeners))
            {
                for (var i = 0; i < listeners.Count; ++i)
                {
                    if (listeners[i].TryGetTarget(out var target) && target == listener)
                    {
                        listeners.RemoveAt(i);

                        if (listeners.Count == 0)
                        {
                            if (_handlers.TryGetValue(collection, out var handler))
                                collection.CollectionChanged -= handler;
                            _entries.Remove(collection);
                        }

                        return;
                    }
                }
            }

            throw new InvalidOperationException(
                "Collection listener not registered for this collection/listener combination.");
        }

        private void OnNotifyCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            static void Notify(
                INotifyCollectionChanged incc,
                NotifyCollectionChangedEventArgs args,
                List<WeakReference<ICollectionChangedListener>> listeners)
            {
                foreach (var l in listeners)
                {
                    if (l.TryGetTarget(out var target))
                    {
                        target.PreChanged(incc, args);
                    }
                }

                foreach (var l in listeners)
                {
                    if (l.TryGetTarget(out var target))
                    {
                        target.Changed(incc, args);
                    }
                }

                foreach (var l in listeners)
                {
                    if (l.TryGetTarget(out var target))
                    {
                        target.PostChanged(incc, args);
                    }
                }
            }

            if (sender is INotifyCollectionChanged incc && _entries.TryGetValue(incc, out var listeners))
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    Notify(incc, e, listeners);
                }
                else
                {
                    var inccCapture = incc;
                    var eCapture = e;
                    var listenersCapture = listeners;
                    Dispatcher.UIThread.Post(() => Notify(inccCapture, eCapture, listenersCapture));
                }
            }
        }
    }
}
