using System.Collections;

namespace Pipelinez.Core.Record.Metadata;

public class MetadataCollection : IList<MetadataRecord>
{
    private readonly IList<MetadataRecord> _internalList;
    
    public MetadataCollection()
    {
        _internalList = new List<MetadataRecord>();
    }
    
    public IEnumerator<MetadataRecord> GetEnumerator()
    {
        return _internalList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(MetadataRecord item)
    {
        // TODO: throw if adding a duplicate key
        _internalList.Add(item);
    }

    public void Clear()
    {
        _internalList.Clear();
    }

    public bool Contains(MetadataRecord item)
    {
        return _internalList.Contains(item);
    }

    public void CopyTo(MetadataRecord[] array, int arrayIndex)
    {
        _internalList.CopyTo(array, arrayIndex);
    }

    public bool Remove(MetadataRecord item)
    {
        return _internalList.Remove(item);
    }

    public int Count => _internalList.Count;

    public bool IsReadOnly => _internalList.IsReadOnly;

    public int IndexOf(MetadataRecord item)
    {
        return _internalList.IndexOf(item);
    }

    public void Insert(int index, MetadataRecord item)
    {
        _internalList.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _internalList.RemoveAt(index);
    }

    public bool HasKey(string key)
    {
        return _internalList.Any(m => m.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
    }

    public MetadataRecord? GetByKey(string key)
    {
        if (HasKey(key))
        {
            return _internalList.FirstOrDefault(m => m.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        }

        return null;
    }

    public string? GetValue(string key)
    {
        return GetByKey(key)?.Value;
    }

    public void Set(string key, string value)
    {
        var existingRecord = GetByKey(key);
        if (existingRecord is not null)
        {
            existingRecord.Value = value;
            return;
        }

        Add(new MetadataRecord(key, value));
    }

    public MetadataCollection Clone()
    {
        var clone = new MetadataCollection();

        foreach (var item in _internalList)
        {
            clone.Add(new MetadataRecord(item.Key, item.Value));
        }

        return clone;
    }

    public MetadataRecord this[int index]
    {
        get => _internalList[index];
        set => _internalList[index] = value;
    }
}
