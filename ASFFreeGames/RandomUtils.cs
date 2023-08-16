using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Maxisoft.ASF;

#nullable enable

public static class RandomUtils {
	/// <summary>
	///     Generates a random number from a normal distribution with the specified mean and standard deviation.
	/// </summary>
	/// <param name="random">The random number generator to use.</param>
	/// <param name="mean">The mean of the normal distribution.</param>
	/// <param name="standardDeviation">The standard deviation of the normal distribution.</param>
	/// <returns>A random number from the normal distribution.</returns>
	/// <remarks>
	///     This method uses the Box-Muller transform to convert two uniformly distributed random numbers into two normally distributed random numbers.
	/// </remarks>
	public static double NextGaussian([NotNull] this RandomNumberGenerator random, double mean, double standardDeviation) {
		Debug.Assert(random != null, nameof(random) + " != null");

		// Generate two uniform random numbers
		Span<byte> bytes = stackalloc byte[8];
		random.GetBytes(bytes);
		double u1 = BitConverter.ToUInt32(bytes) / (double) uint.MaxValue;
		random.GetBytes(bytes);
		double u2 = BitConverter.ToUInt32(bytes) / (double) uint.MaxValue;

		// Apply the Box-Muller formula
		double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

		// Scale and shift to get a random number with the desired mean and standard deviation
		double randNormal = mean + (standardDeviation * randStdNormal);

		return randNormal;
	}

	internal sealed class GaussianRandom : RandomNumberGenerator {
		// A flag to indicate if there is a stored value for the next Gaussian number
		private bool HasNextGaussian;

		// The stored value for the next Gaussian number
		private double NextGaussianValue;

		public override void GetBytes(byte[] data) => Fill(data);

		public override void GetNonZeroBytes(byte[] data) => Fill(data);

		private double NextDouble() {
			if (HasNextGaussian) {
				HasNextGaussian = false;

				return NextGaussianValue;
			}

			// Generate two uniform random numbers
			Span<byte> bytes = stackalloc byte[8];
			GetBytes(bytes);
			float u1 = BitConverter.ToUInt32(bytes) / (float) uint.MaxValue;
			GetBytes(bytes);
			float u2 = BitConverter.ToUInt32(bytes) / (float) uint.MaxValue;

			// Apply the Box-Muller formula
			float r = MathF.Sqrt(-2.0f * MathF.Log(u1));
			float theta = 2.0f * MathF.PI * u2;

			// Store one of the values for next time
			NextGaussianValue = r * MathF.Sin(theta);
			HasNextGaussian = true;

			// Return the other value
			return r * MathF.Cos(theta);
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
