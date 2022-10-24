using System.Collections.Generic;

namespace Maxisoft.ASF.Reddit;

internal struct RedditGameEntryComparerOnDate : IComparer<RedditGameEntry> {
	public int Compare(RedditGameEntry x, RedditGameEntry y) => x.Date.CompareTo(y.Date);
}
