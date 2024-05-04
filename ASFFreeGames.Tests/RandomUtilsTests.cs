#pragma warning disable CA1707 // Identifiers should not contain underscores
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;

namespace Maxisoft.ASF.Tests;

public class RandomUtilsTests {
	// A static method to provide test data for the theory
	public static TheoryData<double, double, int, double> GetTestData() =>
		new TheoryData<double, double, int, double> {
			// mean, std, sample size, margin of error
			{ 0, 1, 10000, 0.05 },
			{ 10, 2, 10000, 0.1 },
			{ -5, 3, 50000, 0.15 },
			{ 20, 5, 100000, 0.2 }
		};

	// A test method to check if the mean and standard deviation of the normal distribution are close to the expected values
	[Theory]
	[MemberData(nameof(GetTestData))]
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public void NextGaussian_Should_Have_Expected_Mean_And_Std(double mean, double standardDeviation, int sampleSize, double marginOfError) {
		// Arrange
		RandomUtils.GaussianRandom rng = new();

		// Act
		// Generate a large number of samples from the normal distribution
		double[] samples = Enumerable.Range(0, sampleSize).Select(_ => rng.NextGaussian(mean, standardDeviation)).ToArray();

		// Calculate the sample mean and sample standard deviation using local functions
		double sampleMean = Mean(samples);
		double sampleStd = StandardDeviation(samples);

		// Assert
		// Check if the sample mean and sample standard deviation are close to the expected values within the margin of error
		Assert.InRange(sampleMean, mean - marginOfError, mean + marginOfError);
		Assert.InRange(sampleStd, standardDeviation - marginOfError, standardDeviation + marginOfError);
	}

	// Local function to calculate the mean of a span of doubles
	private static double Mean(ReadOnlySpan<double> values) {
		// Check if the span is empty
		if (values.IsEmpty) {
			// Throw an exception
			throw new InvalidOperationException("The span is empty.");
		}

		// Sum up all the values
		double sum = 0;

		foreach (double value in values) {
			sum += value;
		}

		// Divide by the number of values
		return sum / values.Length;
	}

	// Local function to calculate the standard deviation of a span of doubles
	private static double StandardDeviation(ReadOnlySpan<double> values) {
		// Calculate the mean using the local function
		double mean = Mean(values);

		// Sum up the squares of the differences from the mean
		double sumOfSquares = 0;

		foreach (double value in values) {
			sumOfSquares += (value - mean) * (value - mean);
		}

		// Divide by the number of values and take the square root
		return Math.Sqrt(sumOfSquares / values.Length);
	}
}
#pragma warning restore CA1707 // Identifiers should not contain underscores
