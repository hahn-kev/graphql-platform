using System;
using System.Buffers;

namespace HotChocolate.Execution.Processing;

internal static class PathHelper
{
    public static Path CreatePathFromContext(ISelection selection, ResultData parent, int index)
    {
        if (parent is ObjectResult)
        {
            return CreatePath(parent, selection.ResponseName);   
        }

        if (parent is ListResult)
        {
            return CreatePath(parent, index);
        }

        throw new NotSupportedException($"{parent.GetType().FullName} is not a supported parent type.");

        static Path CreatePath(ResultData parent, object segmentValue)
        {
            var segments = ArrayPool<object>.Shared.Rent(64);
            segments[0] = segmentValue;
            
            var length = Build(segments, parent);
            var path = Path.Root.Append((string) segments[length - 1]);

            if (length > 1)
            {
                for (var i = length - 2; i >= 0; i--)
                {
                    path = segments[i] switch
                    {
                        string s => path.Append(s),
                        int n => path.Append(n),
                        _ => path
                    };
                }
            }
            
            ArrayPool<object>.Shared.Return(segments);
            return path;   
        }
    }

    private static int Build(object[] segments, ResultData parent)
    {
        var segment = 1;
        var current = parent;

        while (current.Parent is not null)
        {
            if (segments.Length <= segment)
            {
                var temp = ArrayPool<object>.Shared.Rent(segments.Length * 2);
                segments.AsSpan().CopyTo(temp);
                ArrayPool<object>.Shared.Return(segments);
                segments = temp;
            }

            var i = current.ParentIndex;
            var p = current.Parent;

            switch (p)
            {
                case ObjectResult o:
                {
                    var field = o[i];

                    if (!field.IsInitialized)
                    {
                        throw new InvalidOperationException("Cannot build path from an uninitialized field.");
                    }

                    segments[segment++] = field.Name;
                    current = o;
                    break;
                }

                case ListResult l:
                    segments[i] = i;
                    current = l;
                    break;

                default:
                    throw new NotSupportedException($"{p.GetType().FullName} is not a supported parent type.");
            }
        }

        return segment;
    }
}