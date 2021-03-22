#pragma warning disable RECS0018

using System.Collections.Generic;
using System.Collections;
using System;

internal class AssetSpriteCollectionLookup : IDictionary<string, AssetMetadata>
{
    private Dictionary<string, AssetMetadata> _sprites;
    private Dictionary<string, AssetMetadata> _unprocessedSprites;

    public AssetSpriteCollectionLookup(string name)
    {
        Name = name;
        _sprites = new Dictionary<string, AssetMetadata>();
        _unprocessedSprites = new Dictionary<string, AssetMetadata>();
    }

    public AssetMetadata this[string key]
    {
        get => _sprites[key];
        set
        {
            _sprites[key] = value;
            _unprocessedSprites[key] = value;
        }
    }

    public string Name { get; }

    public int UnprocessedCount => _unprocessedSprites.Count;

    public ICollection<string> Keys => _sprites.Keys;

    public ICollection<AssetMetadata> Values => _sprites.Values;

    public int Count => _sprites.Count;

    public bool IsReadOnly => false;

    public bool TakeUnprocessedChanges(out Dictionary<string, AssetMetadata> changes)
    {
        if (UnprocessedCount == 0)
        {
            changes = null;
            return false;
        }

        changes = _unprocessedSprites;
        _unprocessedSprites = new Dictionary<string, AssetMetadata>();
        return true;
    }

    public void Add(string key, AssetMetadata value)
    {
        _sprites.Add(key, value);
        _unprocessedSprites.Add(key, value);
    }

    public void Add(KeyValuePair<string, AssetMetadata> item)
    {
        ((IDictionary<string, AssetMetadata>)_sprites).Add(item);
        ((IDictionary<string, AssetMetadata>)_unprocessedSprites).Add(item);
    }

    public void Clear()
    {
        _sprites.Clear();
        _unprocessedSprites.Clear();
    }

    public bool Contains(KeyValuePair<string, AssetMetadata> item)
    {
        return ((IDictionary<string, AssetMetadata>)_sprites).Contains(item);
    }

    public bool ContainsKey(string key) => _sprites.ContainsKey(key);

    public void CopyTo(KeyValuePair<string, AssetMetadata>[] array, int arrayIndex)
    {
        ((IDictionary<string, AssetMetadata>)_sprites).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<string, AssetMetadata>> GetEnumerator() => _sprites.GetEnumerator();

    public bool Remove(string key)
    {
        if (_sprites.Remove(key))
        {
            _unprocessedSprites.Remove(key);
            return true;
        }

        return false;
    }

    bool ICollection<KeyValuePair<string, AssetMetadata>>.Remove(KeyValuePair<string, AssetMetadata> item)
    {
        if (((IDictionary<string, AssetMetadata>)_sprites).Remove(item))
        {
            ((IDictionary<string, AssetMetadata>)_unprocessedSprites).Remove(item);
            return true;
        }

        return false;
    }

    public bool TryGetValue(string key, out AssetMetadata value)
    {
        return _sprites.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
