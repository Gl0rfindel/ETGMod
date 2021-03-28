using System;
using System.Collections.Generic;
using System.Linq;

public class IDPool<T> {
    private Dictionary<NamespaceItemKey, T> _Storage = new Dictionary<NamespaceItemKey, T>();
    private HashSet<string> _LockedNamespaces = new HashSet<string>();
    private HashSet<string> _Namespaces = new HashSet<string>();

    public T this[string id] {
        set => Set(id, value, false);
        get => Get(id);
    }

    public int Count => _Storage.Count;

    public class NonExistantIDException : Exception {
        public NonExistantIDException(string id) : base($"Object with ID {id} doesn't exist") { }
    }

    public class BadIDElementException : Exception {
        public BadIDElementException(string name) : base($"The ID's {name} can not contain any colons or whitespace") { }
    }

    public class LockedNamespaceException : Exception {
        public LockedNamespaceException(string namesp) : base($"The ID namespace {namesp} is locked") { }
    }

    public class ItemIDExistsException : Exception {
        public ItemIDExistsException(string id) : base($"Item {id} already exists") { }
    }

    public class BadlyFormattedIDException : Exception {
        public BadlyFormattedIDException(string id) : base($"ID was improperly formatted: {id}") { }
    }

    public void LockNamespace(string namesp) => _LockedNamespaces.Add(namesp);

    private void Set(string id, T obj, bool throwOnExists) {
        Console.WriteLine($"SETTING {id}");
        var key = ResolveKey(id);

        if (_LockedNamespaces.Contains(key.Namespace))
            throw new LockedNamespaceException(key.Namespace);

        if (throwOnExists && _Storage.ContainsKey(key))
            throw new ItemIDExistsException(id);

        _Storage[key] = obj;
        _Namespaces.Add(key.Namespace);
    }

    public void Add(string id, T obj) => Set(id, obj, true);

    public T Get(string id) {
        Console.WriteLine($"GETTING {id}");
        var key = ResolveKey(id);
        if (!_Storage.TryGetValue(key, out var value))
            throw new NonExistantIDException(key.FullId);

        return value;
    }

    public void Remove(string id, bool destroy = true) {
        var key = ResolveKey(id);
        if (_LockedNamespaces.Contains(key.Namespace)) 
            throw new LockedNamespaceException(key.Namespace);

        if (!_Storage.TryGetValue(key, out var value)) 
            throw new NonExistantIDException(id);

        if (destroy && value is UnityEngine.Object o) 
            UnityEngine.Object.Destroy(o);

        _Storage.Remove(key);
    }

    public void Rename(string source, string target) {
        Console.WriteLine($"RENAMING {source} -> {target}");
        var targetKey = ResolveKey(target);
        if (_LockedNamespaces.Contains(targetKey.Namespace)) 
            throw new LockedNamespaceException(targetKey.Namespace);

        var sourceKey = ResolveKey(source);
        if (!_Storage.TryGetValue(sourceKey, out var obj)) 
            throw new NonExistantIDException(source);

        _Storage.Remove(sourceKey);
        _Storage[targetKey] = obj;
    }

    public static void VerifyID(string id) {
        if (id.Count(':') > 1) 
            throw new BadlyFormattedIDException(id);
    }

    public static string Resolve(string id) {
        id = id.Trim();
        if (id.Contains(":")) {
            VerifyID(id);
            return id;
        } else {
            return $"gungeon:{id}";
        }
    }

    private static NamespaceItemKey ResolveKey(string id)
    {
        id = id.Trim();

        if (id.ContainsWhitespace())
            throw new BadIDElementException(id);

        int colonIndex = id.IndexOf(':');
        if (colonIndex < 0)
            return new NamespaceItemKey("gungeon", id, $"gungeon:{id}");

        int second = id.IndexOf(':', colonIndex + 1);
        if (second >= 0)
            throw new BadlyFormattedIDException(id);

        string ns = id.Substring(0, colonIndex);
        string item = id.Substring(colonIndex + 1);
        return new NamespaceItemKey(ns, item, id);
    }

    public static Entry Split(string id) {
        VerifyID(id);
        string[] split = id.Split(':');
        if (split.Length != 2) 
            throw new BadlyFormattedIDException(id);
        return new Entry(split[0], split[1]);
    }

    public bool ContainsID(string id) {
        return _Storage.ContainsKey(ResolveKey(id));
    }

    public bool NamespaceIsLocked(string namesp) {
        return _LockedNamespaces.Contains(namesp);
    }

    public string[] AllIDs => _Storage.Keys.Select(k => k.FullId).ToArray();

    public IEnumerable<T> Entries => _Storage.Values;

    public IEnumerable<string> IDs => _Storage.Keys.Select(k => k.FullId);

    public IEnumerable<KeyValuePair<string, T>> Pairs {
        get 
        {
            foreach (var kv in _Storage) {
                yield return new KeyValuePair<string, T>(kv.Key.FullId, kv.Value);
            }
        }
    }

    public struct Entry
    {
        public string Namespace;
        public string Name;

        public Entry(string namesp, string name)
        {
            Namespace = namesp;
            Name = name;
        }
    }

    private readonly struct NamespaceItemKey : IEquatable<NamespaceItemKey>
    {
        public NamespaceItemKey(string ns, string itemPart, string fullId)
        {
            Namespace = ns;
            ItemPart = itemPart;
            FullId = fullId;
        }

        public string Namespace { get; }

        public string ItemPart { get; }

        public string FullId { get; }

        public override int GetHashCode()
        {
            int hc = Namespace?.GetHashCode() ?? 0;
            hc *= 17;
            hc <<= 16;
            hc ^= ItemPart?.GetHashCode() ?? 0;
            return hc;
        }

        public override bool Equals(object obj)
        {
            if (obj is NamespaceItemKey other)
            {
                return Equals(this, other);
            }

            return false;
        }

        public bool Equals(NamespaceItemKey other) => Equals(this, other);

        private static bool Equals(in NamespaceItemKey left, in NamespaceItemKey right)
        {
            return left.Namespace == right.Namespace && left.ItemPart == right.ItemPart;
        }

        public override string ToString() => FullId;
    }
}
