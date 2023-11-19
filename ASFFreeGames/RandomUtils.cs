using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Maxisoft.ASF;

#nullable enable

public static class RandomUtils {
	internal sealed class GaussianRandom : RandomNumberGenerator {
		// A flag to indicate if there is a stored value for the next Gaussian number
		private int HasNextGaussian;

		private const int True = 1;
		private const int False = 0;

		// The stored value for the next Gaussian number
		private double NextGaussianValue;

		public override void GetBytes(byte[] data) => Fill(data);

		public override void GetNonZeroBytes(Span<byte> data) {
			Fill(data);
			Span<byte> buffer = stackalloc byte[1];

			for (int i = 0; i < data.Length; i++) {
				while (data[i] == default(byte)) {
					Fill(buffer);
					data[i] = buffer[0];
				}
			}
		}

		public override void GetNonZeroBytes(byte[] data) => GetNonZeroBytes((Span<byte>) data);

		private double NextDouble() {
			if (Interlocked.CompareExchange(ref HasNextGaussian, False, True) == True) {
				return NextGaussianValue;
			}

			Span<byte> bytes = stackalloc byte[16];
			Fill(bytes);
			Span<ulong> ulongs = MemoryMarshal.Cast<byte, ulong>(bytes);
			double u1 = ulongs[0] / (double) ulong.MaxValue;
			double u2 = ulongs[1] / (double) ulong.MaxValue;

			// Box-Muller formula
			double r = Math.Sqrt(-2.0f * Math.Log(u1));
			double theta = 2.0 * Math.PI * u2;

			if (Interlocked.CompareExchange(ref HasNextGaussian, True, False) == False) {
				NextGaussianValue = r * Math.Sin(theta);
			}

			return r * Math.Cos(theta);
		}

		/// <summary>
		///     Generates a random number from a normal distribution with the specified mean and standard deviation.
		/// </summary>
		/// <param name="mean">The mean of the normal distribution.</param>
		/// <param name="standardDeviation">The standard deviation of the normal distribution.</param>
		/// <returns>A random number from the normal distribution.</returns>
		/// <remarks>
		///     This method uses the overridden NextDouble method to get a normally distributed random number.
		/// </remarks>
		public double NextGaussian(double mean, double standardDeviation) =>

			// Use the overridden NextDouble method to get a normally distributed random number
			mean + (standardDeviation * NextDouble());
	}
}
