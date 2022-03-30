using System.Runtime.CompilerServices;

namespace Maxisoft.Utils {
	public static class Math2 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NumberOfTrailingZeros(int x) {
			const int numbit = sizeof(int) * 8;

			unchecked {
				if (x < 0) {
					return numbit;
				}

				return (int) NumberOfTrailingZeros((uint) x);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint NumberOfTrailingZeros(uint x) {
			const uint numbit = sizeof(uint) * 8;

			unchecked {
				if (x == 0) {
					return numbit;
				}

				var res = (uint) NumberOfTrailingZeros((ulong) x);

				return res & 0x1F;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long NumberOfTrailingZeros(long x) {
			const int numbit = sizeof(long) * 8;

			unchecked {
				if (x < 0) {
					return numbit;
				}

				var res = (long) NumberOfTrailingZeros((ulong) x);

				return res;
			}
		}

		public static ulong NumberOfTrailingZeros(ulong i) {
			unchecked {
				const ulong log2Of64 = 6;
				const ulong numbit = sizeof(ulong) * 8;

				if (i == 0) {
					return numbit;
				}

				var n = numbit - 1;

				for (ulong j = 0; j < log2Of64; j++) {
					var power = (int) (numbit >> (int) (j + 1));

					var y = i << power;

					if (y != 0) {
						n -= (ulong) power;
						i = y;
					}
				}

				return n - ((i << 1) >> (int) (numbit - 1));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong NumberOfLeadingZeros(ulong i) {
			const ulong numbit = sizeof(ulong) * 8;

			if (i == 0) {
				return numbit;
			}

			var n = 1UL;

			if (i >> 32 == 0) {
				n += 32;
				i <<= 32;
			}

			if (i >> 48 == 0) {
				n += 16;
				i <<= 16;
			}

			if (i >> 56 == 0) {
				n += 8;
				i <<= 8;
			}

			if (i >> 60 == 0) {
				n += 4;
				i <<= 4;
			}

			if (i >> 62 == 0) {
				n += 2;
				i <<= 2;
			}

			n -= i >> 63;

			return n;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long NumberOfLeadingZeros(long i) {
			unchecked {
				if (i < 0) {
					return 0;
				}

				return (long) NumberOfLeadingZeros((ulong) i);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint NumberOfLeadingZeros(uint i) {
			const ulong numbit = sizeof(uint) * 8;

			if (i == 0) {
				return (uint) numbit;
			}

			unchecked {
				return (uint) (NumberOfLeadingZeros((ulong) i) - numbit);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NumberOfLeadingZeros(int i) {
			unchecked {
				if (i < 0) {
					return 0;
				}

				return (int) NumberOfLeadingZeros((uint) i);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Log2(ulong x) {
			if (x == 0) {
				return 0;
			}

			return sizeof(ulong) * 8 - 1 - NumberOfLeadingZeros(x);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Log2(uint x) {
			if (x == 0) {
				return 0;
			}

			return sizeof(uint) * 8 - 1 - NumberOfLeadingZeros(x);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Log2(int x) {
			return x < 0 ? 0 : (int) Log2((uint) x);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Log2(long x) {
			return x < 0 ? 0 : (long) Log2((ulong) x);
		}
	}
}
