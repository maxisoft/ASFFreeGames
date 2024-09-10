using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Maxisoft.ASF.HttpClientSimple;

namespace Maxisoft.ASF.Redlib.Instances;

[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public interface IRedlibInstanceList {
	Task<List<Uri>> ListInstances([NotNull] SimpleHttpClient httpClient, CancellationToken cancellationToken);
}
