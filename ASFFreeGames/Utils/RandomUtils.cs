using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Maxisoft.ASF.Utils;

#nullable enable

/// <summary>
/// Provides utility methods for generating random numbers.
/// </summary>
public static class RandomUtils {
	internal sealed class GaussianRandom {
		// A flag to indicate if there is a stored value for the next Gaussian number
		private int HasNextGaussian;

		private const int True = 1;
		private const int False = 0;

		// The stored value for the next Gaussian number
		private double NextGaussianValue;

		/// <summary>
		/// Fills the provided span with non-zero random bytes.
		/// </summary>
		/// <param name="data">The span to fill with non-zero random bytes.</param>
		private void GetNonZeroBytes(Span<byte> data) {
			Span<byte> bytes = stackalloc byte[sizeof(long)];

			static void fill(Span<byte> bytes) {
				// use this method to use a RNGs function that's still included with the ASF trimmed binary
				// do not try to refactor or optimize this without testing
				byte[] rng = RandomNumberGenerator.GetBytes(bytes.Length);
				((ReadOnlySpan<byte>) rng).CopyTo(bytes);
			}

			fill(bytes);
			int c = 0;

			for (int i = 0; i < data.Length; i++) {
				byte value;

				do {
					value = bytes[c];
					c++;

					if (c >= bytes.Length) {
						fill(bytes);
						c = 0;
					}
				} while (value == 0);

				data[i] = value;
			}
		}

		/// <summary>
		/// Generates a random double value.
		/// </summary>
		/// <returns>A random double value.</returns>
		private double NextDouble() {
			if (Interlocked.CompareExchange(ref HasNextGaussian, False, True) == True) {
				return NextGaussianValue;
			}

			Span<byte> bytes = stackalloc byte[2 * sizeof(long)];

			Span<ulong> ulongs = MemoryMarshal.Cast<byte, ulong>(bytes);
			double u1;

			do {
				GetNonZeroBytes(bytes);
				u1 = ulongs[0] / (double) ulong.MaxValue;
			} while (u1 <= double.Epsilon);

			double u2 = ulongs[1] / (double) ulong.MaxValue;

			// Box-Muller formula
			double r = Math.Sqrt(-2.0 * Math.Log(u1));
			double theta = 2.0 * Math.PI * u2;

			if (Interlocked.CompareExchange(ref HasNextGaussian, True, False) == False) {
				NextGaussianValue = r * Math.Sin(theta);
			}

			return r * Math.Cos(theta);
		}

		/// <summary>
		/// Generates a random number from a normal distribution with the specified mean and standard deviation.
		/// </summary>
		/// <param name="mean">The mean of the normal distribution.</param>
		/// <param name="standardDeviation">The standard deviation of the normal distribution.</param>
		/// <returns>A random number from the normal distribution.</returns>
		/// <remarks>
		/// This method uses the overridden NextDouble method to get a normally distributed random number.
		/// </remarks>
		public double NextGaussian(double mean, double standardDeviation) {
			// Use the overridden NextDouble method to get a normally distributed random
			double rnd;

			do {
				rnd = NextDouble();
			} while (!double.IsFinite(rnd));

			return mean + (standardDeviation * rnd);
		}
	}
}
