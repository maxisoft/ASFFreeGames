﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ASFFreeGames.Configurations;
using Maxisoft.ASF.Redlib;
using Maxisoft.ASF.Redlib.Html;
using Maxisoft.ASF.Redlib.Instances;
using Xunit;

namespace Maxisoft.ASF.Tests.Redlib;

public class RedlibInstanceListTests {
	[Fact]
	public async void Test() {
		RedlibInstanceList lister = new(new ASFFreeGamesOptions());
		List<Uri> uris = await RedlibInstanceList.ListFromEmbedded(default(CancellationToken)).ConfigureAwait(false);

		Assert.NotEmpty(uris);
	}
}