namespace Maxisoft.ASF.Reddit;

public readonly record struct RedditGameEntry(string Identifier, ERedditGameEntryKind Kind, long Date) {
	/// <summary>
	///     Indicates that the entry a DLC or a required game linked to a free DLC entry
	/// </summary>
	public bool IsForDlc => Kind.HasFlag(ERedditGameEntryKind.Dlc);

	public bool IsFreeToPlay => Kind.HasFlag(ERedditGameEntryKind.FreeToPlay);

	public void Deconstruct(out string identifier, out long date, out bool freeToPlay, out bool dlc) {
		identifier = Identifier;
		date = Date;
		freeToPlay = IsFreeToPlay;
		dlc = IsForDlc;
	}
}
