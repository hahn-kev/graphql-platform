#nullable enable

namespace HotChocolate.Execution.Processing;

internal sealed partial class ResultBuilder
{
    private readonly ResultPool _resultPool;
    private readonly object _objectSync = new();
    private ResultBucket<ObjectResult> _objectBucket = default!;
    private readonly object _listSync = new();
    private ResultBucket<ListResult> _listBucket = default!;

    public ObjectResult RentObject(int capacity)
    {
        lock (_objectSync)
        {
            while (true)
            {
                if (_objectBucket.TryPop(out var obj))
                {
                    obj.EnsureCapacity(capacity);
                    return obj;
                }

                _objectBucket = _resultPool.GetObjectBucket();
                _resultOwner.ObjectBuckets.Add(_objectBucket);
            }
        }
    }

    public ListResult RentList(int capacity)
    {
        lock (_listSync)
        {
            while (true)
            {
                if (_listBucket.TryPop(out var obj))
                {
                    obj.EnsureCapacity(capacity);
                    return obj;
                }

                _listBucket = _resultPool.GetListBucket();
                _resultOwner.ListBuckets.Add(_listBucket);
            }
        }
    }
}
