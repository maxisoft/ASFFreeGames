using System;
using System.Linq;
using System.Threading.Tasks;
using ArchiSteamFarm;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Web.GitHub;
using ArchiSteamFarm.Web.GitHub.Data;

namespace Maxisoft.ASF.Github;

public class GithubPluginUpdater(Lazy<Version> version) {
	public const string RepositoryName = "maxisoft/ASFFreeGames";
	public bool CanUpdate { get; internal set; } = true;

	private Version CurrentVersion => version.Value;

	public async Task<Uri?> GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced) {
		ArgumentNullException.ThrowIfNull(asfVersion);
		ArgumentException.ThrowIfNullOrEmpty(asfVariant);

		if (!CanUpdate) {
			return null;
		}

		if (string.IsNullOrEmpty(RepositoryName)) {
			//ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError(Strings.FormatWarningFailedWithError(nameof(RepositoryName)));

			return null;
		}

		ReleaseResponse? releaseResponse = await GitHubService.GetLatestRelease(RepositoryName).ConfigureAwait(false);

		if (releaseResponse == null) {
			return null;
		}

		if (releaseResponse.IsPreRelease) {
			return null;
		}

		Version newVersion = new(releaseResponse.Tag.ToUpperInvariant().TrimStart('V'));

		if (!forced && (CurrentVersion >= newVersion)) {
			// Allow same version to be re-updated when we're updating ASF release and more than one asset is found - potential compatibility difference
			if ((CurrentVersion > newVersion) || !asfUpdate || (releaseResponse.Assets.Count(static asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) < 2)) {
				//ASF.ArchiLogger.LogGenericInfo(Strings.FormatPluginUpdateNotFound(Name, Version, newVersion));

				return null;
			}
		}

		if (releaseResponse.Assets.Count == 0) {
			//ASF.ArchiLogger.LogGenericWarning(Strings.FormatPluginUpdateNoAssetFound(Name, Version, newVersion));

			return null;
		}

		ReleaseAsset? asset = releaseResponse.Assets.FirstOrDefault(static asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && (asset.Size > (1 << 18)));

		if ((asset == null) || !releaseResponse.Assets.Contains(asset)) {
			//ASF.ArchiLogger.LogGenericWarning(Strings.FormatPluginUpdateNoAssetFound(Name, Version, newVersion));

			return null;
		}

		//.ArchiLogger.LogGenericInfo(Strings.FormatPluginUpdateFound(Name, Version, newVersion));

		return asset.DownloadURL;
	}
}
