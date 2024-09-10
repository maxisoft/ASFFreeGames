using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASFFreeGames.Configurations;
using Maxisoft.ASF.HttpClientSimple;

namespace Maxisoft.ASF.Redlib.Instances;

public class CachedRedlibInstanceList(ASFFreeGamesOptions options, CachedRedlibInstanceListStorage storage) : IRedlibInstanceList {
	private readonly RedlibInstanceList InstanceList = new(options);

	public async Task<List<Uri>> ListInstances([NotNull] SimpleHttpClient httpClient, CancellationToken cancellationToken) {
		if (((DateTimeOffset.Now - storage.LastUpdate).Duration() > TimeSpan.FromHours(1)) || (storage.Instances.Count == 0)) {
			List<Uri> res = await InstanceList.ListInstances(httpClient, cancellationToken).ConfigureAwait(false);

			if (res.Count > 0) {
				storage.UpdateInstances(res);
			}
		}

		return storage.Instances.ToList();
	}
}
