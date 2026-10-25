using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ChohyoPdf.Designer.Services
{
    /// <summary>モデル全体（入れ子のオブジェクトとコレクションを含む）の変更を 1 つのイベントにまとめて通知する。</summary>
    public sealed class ChangeTracker : IDisposable
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties = new ConcurrentDictionary<Type, PropertyInfo[]>();

        private readonly object _root;
        private readonly HashSet<INotifyPropertyChanged> _objects = new HashSet<INotifyPropertyChanged>();
        private readonly HashSet<INotifyCollectionChanged> _collections = new HashSet<INotifyCollectionChanged>();

        public ChangeTracker(object root)
        {
            _root = root;
            Attach(root);
        }

        /// <summary>何かが変わった（sender は変わったオブジェクト、文字列はプロパティ名。コレクションの変更なら null）。</summary>
        public event Action<object, string?>? Changed;

        public void Dispose() => Detach();

        private void Attach(object? obj)
        {
            switch (obj)
            {
                case null:
                    return;
                case INotifyCollectionChanged collection:
                    if (!_collections.Add(collection)) return;
                    collection.CollectionChanged += OnCollectionChanged;
                    foreach (object item in (IEnumerable)collection) Attach(item);
                    return;
                case INotifyPropertyChanged notify:
                    if (!_objects.Add(notify)) return;
                    notify.PropertyChanged += OnPropertyChanged;
                    foreach (var p in GetChildProperties(obj.GetType())) Attach(p.GetValue(obj));
                    return;
            }
        }

        private void Detach()
        {
            foreach (var o in _objects) o.PropertyChanged -= OnPropertyChanged;
            foreach (var c in _collections) c.CollectionChanged -= OnCollectionChanged;
            _objects.Clear();
            _collections.Clear();
        }

        private void Reattach()
        {
            Detach();
            Attach(_root);
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 子のオブジェクトやコレクションが差し替えられたら、監視をやり直す
            var p = sender == null || e.PropertyName == null ? null : GetChildProperties(sender.GetType()).FirstOrDefault(x => x.Name == e.PropertyName);
            if (p != null) Reattach();
            Changed?.Invoke(sender!, e.PropertyName);
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Reattach();
            Changed?.Invoke(sender!, null);
        }

        private static PropertyInfo[] GetChildProperties(Type type)
            => ChildProperties.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead
                    && (typeof(INotifyPropertyChanged).IsAssignableFrom(p.PropertyType) || typeof(INotifyCollectionChanged).IsAssignableFrom(p.PropertyType)))
                .ToArray());
    }

    /// <summary>デザインの状態（JSON）を記録して、元に戻す・やり直す。</summary>
    public sealed class UndoManager
    {
        private const int Limit = 200;
        private readonly LinkedList<string> _undo = new LinkedList<string>();
        private readonly Stack<string> _redo = new Stack<string>();
        private string _current = "";

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        /// <summary>記録をやり直す（ファイルを開いたときなど）。</summary>
        public void Reset(string state)
        {
            _undo.Clear();
            _redo.Clear();
            _current = state;
        }

        /// <summary>今の状態を記録する。前回と同じなら何もしない。</summary>
        public bool Commit(string state)
        {
            if (state == _current) return false;
            _undo.AddLast(_current);
            if (_undo.Count > Limit) _undo.RemoveFirst();
            _redo.Clear();
            _current = state;
            return true;
        }

        public string? Undo()
        {
            if (_undo.Count == 0) return null;
            _redo.Push(_current);
            _current = _undo.Last!.Value;
            _undo.RemoveLast();
            return _current;
        }

        public string? Redo()
        {
            if (_redo.Count == 0) return null;
            _undo.AddLast(_current);
            _current = _redo.Pop();
            return _current;
        }
    }
}
