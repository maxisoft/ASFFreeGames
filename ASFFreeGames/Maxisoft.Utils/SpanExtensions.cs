using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Maxisoft.Utils.Collections.Spans
{
    public static class SpanExtensions
    {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AddSorted<T>(this SpanList<T> list, in T item, IComparer<T>? comparer = null)
        {
            if ((uint) list.Count >= (uint) list.Capacity)
            {
                throw new InvalidOperationException("span is full");
            }

            var index = list.BinarySearch(in item, comparer);
            var res = index < 0 ? ~index : index;
            list.Insert(res, in item);
            Debug.Assert((comparer ?? Comparer<T>.Default).Compare(item, list[res]) == 0);
            return res;
        }
        
        public static Span<TOut> ASpan<TIn, TOut>(this SpanList<TIn> list)
            where TIn : struct where TOut : struct
        {
            return MemoryMarshal.Cast<TIn, TOut>(list.AsSpan());
        }

        public static SpanList<TOut> Cast<TIn, TOut>(this SpanList<TIn> list)
            where TIn : unmanaged where TOut : unmanaged
        {
            var span = MemoryMarshal.Cast<TIn, TOut>(list.Span);
            int count;
            unsafe
            {
                if (sizeof(TIn) > sizeof(TOut))
                {
                    Debug.Assert(sizeof(TIn) % sizeof(TOut) == 0);
                    count = list.Count * (sizeof(TIn) / sizeof(TOut));
                }
                else
                {
                    Debug.Assert(sizeof(TOut) % sizeof(TIn) == 0);
                    count = list.Count / (sizeof(TOut) / sizeof(TIn));
                }
            }

            return new SpanList<TOut>(span, count);
        }
    }
}