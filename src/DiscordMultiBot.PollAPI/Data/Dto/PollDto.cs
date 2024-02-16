using System.Collections;

namespace DiscordMultiBot.PollService.Data.Dto;

public sealed class PollOptions : IList<string>
{
    private IList<string> _list;

    public PollOptions()
    {
        _list = new List<string>();
    }

    public PollOptions(IEnumerable<string> items)
    {
        _list = new List<string>(items);
    }

    public override string ToString()
    {
        return String.Join("++", _list);
    }

    public static PollOptions FromString(string s)
    {
        return new PollOptions(s.Split("++"));
    }

    public IEnumerator<string> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_list).GetEnumerator();
    }

    public void Add(string item)
    {
        _list.Add(item);
    }

    public void Clear()
    {
        _list.Clear();
    }

    public bool Contains(string item)
    {
        return _list.Contains(item);
    }

    public void CopyTo(string[] array, int arrayIndex)
    {
        _list.CopyTo(array, arrayIndex);
    }

    public bool Remove(string item)
    {
        return _list.Remove(item);
    }

    public int Count => _list.Count;

    public bool IsReadOnly => _list.IsReadOnly;

    public int IndexOf(string item)
    {
        return _list.IndexOf(item);
    }

    public void Insert(int index, string item)
    {
        _list.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
    }

    public string this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }
}

public enum PollType
{
    Numeric,
    Binary
}

public record PollDto(ulong Id, ulong ChannelId, PollType Type, PollOptions Options, bool IsAnonymous = false, PollMetadataDto? Metadata = null);
public record PollMetadataDto(ulong ChannelId, ulong MessageId);