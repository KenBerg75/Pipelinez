using System.Collections;

namespace Pipelinez.Core.Record.Metadata;

/// <summary>
/// Represents a mutable collection of metadata records attached to a pipeline container.
/// </summary>
public class MetadataCollection : IList<MetadataRecord>
{
    private readonly IList<MetadataRecord> _internalList;
    
    /// <summary>
    /// Initializes a new empty metadata collection.
    /// </summary>
    public MetadataCollection()
    {
        _internalList = new List<MetadataRecord>();
    }

    /// <inheritdoc />
    public IEnumerator<MetadataRecord> GetEnumerator()
    {
        return _internalList.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc />
    public void Add(MetadataRecord item)
    {
        // TODO: throw if adding a duplicate key
        _internalList.Add(item);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _internalList.Clear();
    }

    /// <inheritdoc />
    public bool Contains(MetadataRecord item)
    {
        return _internalList.Contains(item);
    }

    /// <inheritdoc />
    public void CopyTo(MetadataRecord[] array, int arrayIndex)
    {
        _internalList.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public bool Remove(MetadataRecord item)
    {
        return _internalList.Remove(item);
    }

    /// <inheritdoc />
    public int Count => _internalList.Count;

    /// <inheritdoc />
    public bool IsReadOnly => _internalList.IsReadOnly;

    /// <inheritdoc />
    public int IndexOf(MetadataRecord item)
    {
        return _internalList.IndexOf(item);
    }

    /// <inheritdoc />
    public void Insert(int index, MetadataRecord item)
    {
        _internalList.Insert(index, item);
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        _internalList.RemoveAt(index);
    }

    /// <summary>
    /// Determines whether the collection contains a metadata record with the specified key.
    /// </summary>
    /// <param name="key">The metadata key to look for.</param>
    /// <returns><see langword="true" /> if the key exists; otherwise, <see langword="false" />.</returns>
    public bool HasKey(string key)
    {
        return _internalList.Any(m => m.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Gets the first metadata record that matches the specified key.
    /// </summary>
    /// <param name="key">The metadata key to retrieve.</param>
    /// <returns>The matching metadata record, or <see langword="null" /> when no match exists.</returns>
    public MetadataRecord? GetByKey(string key)
    {
        if (HasKey(key))
        {
            return _internalList.FirstOrDefault(m => m.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// Gets the value associated with the specified metadata key.
    /// </summary>
    /// <param name="key">The metadata key to retrieve.</param>
    /// <returns>The metadata value, or <see langword="null" /> when no match exists.</returns>
    public string? GetValue(string key)
    {
        return GetByKey(key)?.Value;
    }

    /// <summary>
    /// Adds or updates a metadata value for the specified key.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var existingRecord = GetByKey(key);
        if (existingRecord is not null)
        {
            existingRecord.Value = value;
            return;
        }

        Add(new MetadataRecord(key, value));
    }

    /// <summary>
    /// Creates a deep copy of the metadata collection.
    /// </summary>
    /// <returns>A cloned metadata collection.</returns>
    public MetadataCollection Clone()
    {
        var clone = new MetadataCollection();

        foreach (var item in _internalList)
        {
            clone.Add(new MetadataRecord(item.Key, item.Value));
        }

        return clone;
    }

    /// <inheritdoc />
    public MetadataRecord this[int index]
    {
        get => _internalList[index];
        set => _internalList[index] = value;
    }
}
