using System;
using System.Diagnostics.CodeAnalysis;
using Maxisoft.Utils.Collections.Spans;

namespace BloomFilter;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("Design", "CA1051")]
public ref struct StringBloomFilterSpan {
	public readonly int HashFunctionCount;

	/// <summary>
	///     The ratio of false to true bits in the filter. E.g., 1 true bit in a 10 bit filter means a truthiness of 0.1.
	/// </summary>
	public float Truthiness => (float) TrueBits() / HashBits.Count;

	public BitSpan HashBits;

	/// <inheritdoc />
	/// <summary>
	///     Creates a new Bloom filter.
	/// </summary>
	/// <param name="bitSpan">The anticipated number of items to be added to the filter. More than this number of items can be added, but the error rate will exceed what is expected.</param>
	/// <param name="errorRate">The accepable false-positive rate (e.g., 0.01F = 1%)</param>
	public StringBloomFilterSpan(BitSpan bitSpan, float errorRate) : this(bitSpan, SolveK(bitSpan.Count, errorRate)) { }

	/// <summary>
	///     Creates a new Bloom filter.
	/// </summary>
	/// <param name="bitSpan">The anticipated number of items to be added to the filter. More than this number of items can be added, but the error rate will exceed what is expected.</param>
	/// <param name="k">The number of hash functions to use.</param>
	public StringBloomFilterSpan(BitSpan bitSpan, int k = 1) {
		// validate the params are in range
		if (bitSpan.Count < 1) {
			throw new ArgumentOutOfRangeException(nameof(bitSpan), bitSpan.Count, "capacity must be > 0");
		}

		HashFunctionCount = k;
		HashBits = bitSpan;
	}

	/// <summary>
	///     Adds a new item to the filter. It cannot be removed.
	/// </summary>
	/// <param name="item">The item.</param>
	public void Add([JetBrains.Annotations.NotNull] in string item) {
		// start flipping bits for each hash of item
#pragma warning disable CA1062
		int primaryHash = item.GetHashCode(StringComparison.Ordinal);
#pragma warning restore CA1062
		int secondaryHash = HashString(item);

		for (int i = 0; i < HashFunctionCount; i++) {
			int hash = ComputeHash(primaryHash, secondaryHash, i);
			HashBits[hash] = true;
		}
	}

	/// <summary>
	///     Checks for the existance of the item in the filter for a given probability.
	/// </summary>
	/// <param name="item"> The item. </param>
	/// <returns> The <see cref="bool" />. </returns>
	public bool Contains([JetBrains.Annotations.NotNull] in string item) {
#pragma warning disable CA1062
		int primaryHash = item.GetHashCode(StringComparison.Ordinal);
#pragma warning restore CA1062
		int secondaryHash = HashString(item);

		for (int i = 0; i < HashFunctionCount; i++) {
			int hash = ComputeHash(primaryHash, secondaryHash, i);

			if (HashBits[hash] == false) {
				return false;
			}
		}

		return true;
	}

	public int Populate(ReadOnlySpan<byte> span) {
		int leftOver = HashBits.Count % 8 == 0 ? 0 : 1;
		int c = 0;

		if (span.Length != (HashBits.Count / 8) + leftOver) {
			throw new ArgumentOutOfRangeException(nameof(span));
		}

		foreach (byte b in span.Slice(0, span.Length - leftOver)) {
			int mask = 1;

			for (int i = 0; i < 8; i++) {
				HashBits[c] = (b & mask) != 0;
				mask = mask << 1;
				c++;
			}
		}

		if (leftOver != 0) {
			byte b = span[^1];
			int mask = 1;

			while (c < HashBits.Count) {
				HashBits[c] = (b & mask) != 0;
				mask = mask << 1;
				c++;
			}
		}

		return c;
	}

	/// <summary>
	/// </summary>
	/// <param name="m"></param>
	/// <param name="errorRate"></param>
	/// <param name="maxK"></param>
	/// <seealso href="https://hur.st/bloomfilter/?n=&p=1.0E-5&m=1024KB&k=" />
	/// <returns></returns>
	public static int SolveK(int m, double errorRate, int maxK = 32) {
		double bestN = double.MinValue;
		int bestK = 0;
		bool noProgress = false;

		// TODO faster algo
		// Like searching from both end and start
		// Or use newton gradient methods

		for (int k = 0; k < maxK; k++) {
			double n = m / (-k / Math.Log(1 - Math.Exp(Math.Log(errorRate) / k)));

			if (n > bestN) {
				bestN = n;
				bestK = k;
			}
			else if (noProgress) {
				break;
			}
			else {
				noProgress = true;
			}
		}

		return bestK;
	}

	public byte[] ToArray() {
		byte[] res = new byte[(HashBits.Count / 8) + (HashBits.Count % 8 == 0 ? 0 : 1)];

		for (int i = 0; i < HashBits.Count; i++) {
			res[i / 8] |= (byte) ((HashBits[i] ? 1 : 0) << (i % 8));
		}

		return res;
	}

	/// <summary>
	///     Performs Dillinger and Manolios double hashing.
	/// </summary>
	/// <param name="primaryHash"> The primary hash. </param>
	/// <param name="secondaryHash"> The secondary hash. </param>
	/// <param name="i"> The i. </param>
	/// <returns> The <see cref="int" />. </returns>
	private int ComputeHash(int primaryHash, int secondaryHash, int i) {
		unchecked {
			int resultingHash = (primaryHash + (i * secondaryHash)) % HashBits.Count;

			return Math.Abs(resultingHash);
		}
	}

	/// <summary>
	///     Hashes a string using Bob Jenkin's "One At A Time" method from Dr. Dobbs (http://burtleburtle.net/bob/hash/doobs.html).
	///     Runtime is suggested to be 9x+9, where x = input.Length.
	/// </summary>
	/// <param name="s">The string to hash.</param>
	/// <returns>The hashed result.</returns>
	private static int HashString(string s) {
		int hash = 0;

		unchecked {
			foreach (char t in s) {
				hash += t;
				hash += hash << 10;
				hash ^= hash >> 6;
			}

			hash += hash << 3;
			hash ^= hash >> 11;
			hash += hash << 15;
		}

		return hash;
	}

	/// <summary>
	///     The true bits.
	/// </summary>
	/// <returns> The <see cref="int" />. </returns>
	private int TrueBits() {
		int output = 0;

		foreach (bool bit in HashBits) {
			if (bit) {
				output++;
			}
		}

		return output;
	}
}
