using System;
using System.Reflection;

namespace Maxisoft.ASF.Utils.Workarounds;

public sealed class AsyncLocal<T> {
	// ReSharper disable once StaticMemberInGenericType
	private static readonly Type? AsyncLocalType;

#pragma warning disable CA1810
	static AsyncLocal() {
#pragma warning restore CA1810
		try {
			AsyncLocalType = Type.GetType("System.Threading.AsyncLocal`1")
				?.MakeGenericType(typeof(T));
		}
		catch (InvalidOperationException) {
			// ignore
		}

		try {
			AsyncLocalType ??= Type.GetType("System.Threading.AsyncLocal")
				?.MakeGenericType(typeof(T));
		}

		catch (InvalidOperationException) {
			// ignore
		}
	}

	private readonly object? Delegate;
	private T? NonSafeValue;

	/// <summary>Instantiates an <see cref="AsyncLocal{T}"/> instance that does not receive change notifications.</summary>
	public AsyncLocal() {
		if (AsyncLocalType is not null) {
			try {
				Delegate = Activator.CreateInstance(AsyncLocalType)!;
			}
			catch (Exception) {
				// ignored
			}
		}
	}

	/// <summary>Gets or sets the value of the ambient data.</summary>
	/// <value>The value of the ambient data. If no value has been set, the returned value is default(T).</value>
	public T? Value {
		get {
			if (Delegate is not null) {
				try {
					PropertyInfo? property = Delegate.GetType().GetProperty("Value");

					if (property is not null) {
						return (T) property.GetValue(Delegate)!;
					}
				}
				catch (Exception) {
					// ignored
				}
			}

			return (T) NonSafeValue!;
		}
		set {
			if (Delegate is not null) {
				try {
					PropertyInfo? property = Delegate.GetType().GetProperty("Value");

					if (property is not null) {
						property.SetValue(Delegate, value);

						return;
					}
				}
				catch (Exception) {
					// ignored
				}
			}

			NonSafeValue = value;
		}
	}
}
