using System;
using System.Collections.Generic;

namespace Maxisoft.ASF.Redlib.Instances;

public record CachedRedlibInstanceListStorage(ICollection<Uri> Instances, DateTimeOffset LastUpdate) {
	public ICollection<Uri> Instances { get; private set; } = Instances;
	public DateTimeOffset LastUpdate { get; private set; } = LastUpdate;

	/// <summary>
	///     Updates the list of instances and its last update time
	/// </summary>
	/// <param name="instances">The list of instances to update</param>
	internal void UpdateInstances(ICollection<Uri> instances) {
		Instances = instances;
		LastUpdate = DateTimeOffset.Now;
	}
}
