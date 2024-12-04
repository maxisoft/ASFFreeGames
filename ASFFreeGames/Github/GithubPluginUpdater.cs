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

	private static void LogGenericError(string message) {
		if (string.IsNullOrEmpty(message)) {
			return;
		}

		ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError($"{nameof(GithubPluginUpdater)}: {message}");
	}

	private static void LogGenericDebug(string message) {
		if (string.IsNullOrEmpty(message)) {
			return;
		}

		ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericDebug($"{nameof(GithubPluginUpdater)}: {message}");
	}

	public async Task<Uri?> GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced) {
		ArgumentNullException.ThrowIfNull(asfVersion);
		ArgumentException.ThrowIfNullOrEmpty(asfVariant);

		if (!CanUpdate) {
			LogGenericDebug("CanUpdate is false");

			return null;
		}

		if (string.IsNullOrEmpty(RepositoryName)) {
			LogGenericError("RepositoryName is null or empty");

			return null;
		}

		ReleaseResponse? releaseResponse = await GitHubService.GetLatestRelease(RepositoryName).ConfigureAwait(false);

		if (releaseResponse == null) {
			LogGenericError("GetLatestRelease returned null");

			return null;
		}

		if (releaseResponse.IsPreRelease) {
			LogGenericError("GetLatestRelease returned pre-release");

			return null;
		}

		if (stable && ((releaseResponse.PublishedAt - DateTime.UtcNow).Duration() < TimeSpan.FromHours(3))) {
			LogGenericDebug("GetLatestRelease returned too recent");

			return null;
		}

		Version newVersion = new(releaseResponse.Tag.ToUpperInvariant().TrimStart('V'));

		if (!forced && (CurrentVersion >= newVersion)) {
			// Allow same version to be re-updated when we're updating ASF release and more than one asset is found - potential compatibility difference
			if ((CurrentVersion > newVersion) || !asfUpdate || (releaseResponse.Assets.Count(static asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) < 2)) {
				return null;
			}
		}

		if (releaseResponse.Assets.Count == 0) {
			LogGenericError($"GetLatestRelease for version {newVersion} returned no assets");

			return null;
		}

		ReleaseAsset? asset = releaseResponse.Assets.FirstOrDefault(static asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && (asset.Size > (1 << 18)));

		if ((asset == null) || !releaseResponse.Assets.Contains(asset)) {
			LogGenericError($"GetLatestRelease for version {newVersion} returned no valid assets");

			return null;
		}

		LogGenericDebug($"GetLatestRelease for version {newVersion} returned asset {asset.Name} with url {asset.DownloadURL}");

		return asset.DownloadURL;
	}
}
