using System;

namespace Maxisoft.ASF.Reddit;

internal struct EmptyStruct : IEquatable<EmptyStruct> {
	public bool Equals(EmptyStruct other) => true;

	public override bool Equals(object? obj) => obj is EmptyStruct;

	public override int GetHashCode() => 0;

	public static bool operator ==(EmptyStruct left, EmptyStruct right) => true;

	public static bool operator !=(EmptyStruct left, EmptyStruct right) => false;
}
