using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Maxisoft.Utils.Collections.Spans
{
    [SuppressMessage("Design", "CA1034")]
	public ref partial struct BitSpan
    {
        public static int ComputeLongArraySize(int numBits)
        {
            var n = numBits / LongNumBit;
            if (numBits % LongNumBit != 0)
            {
                n += 1;
            }

            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSpan Zeros(int numBits)
        {
            return new BitSpan(new long[ComputeLongArraySize(numBits)]);
        }

        public static BitSpan CreateFromBuffer<TSpan>(Span<TSpan> buff) where TSpan : unmanaged
        {
            var castedSpan = MemoryMarshal.Cast<TSpan, long>(buff);
            return new BitSpan(castedSpan);
        }

        public ref struct Enumerator
        {
            /// <summary>The span being enumerated.</summary>
            private readonly BitSpan _bitSpan;

            /// <summary>The next index to yield.</summary>
            private int _index;

            /// <summary>Initialize the enumerator.</summary>
            /// <param name="dict">The dict to enumerate.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(BitSpan bitSpan)
            {
                _bitSpan = bitSpan;
                _index = -1;
            }

            /// <summary>Advances the enumerator to the next element of the dict.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                var index = _index + 1;
                if (index >= _bitSpan.Count)
                {
                    return false;
                }

                _index = index;
                return true;
            }

            /// <summary>Gets the element at the current position of the enumerator.</summary>
            public bool Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _bitSpan.Get(_index);
            }
        }
    }
}
