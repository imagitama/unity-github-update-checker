# GitHub Update Checker

A Unity script that checks if a newer release of your Unity plugin is available on GitHub.

**This tool assumes your GitHub releases are tagged with a version number. eg. 1.0.0 or v1.0.0**

## Usage

This script is designed to be included in the distribution of your Unity plugin:

1.  Clone the repo into your Unity script directory. Delete the directory `DELETE_ME` as it contains some test stuff.
2.  Instantiate the checker:

        void OnEnable() {
            GitHub_Update_Checker githubUpdateChecker = new GitHub_Update_Checker() {
                githubOwner = "your_name",
                githubRepo = "your_repo_name",
                currentVersion = File.ReadAllText("Assets/myplugin/VERSION.txt", System.Text.Encoding.UTF8)
            };
        }

3.  Render the output:

        void OnGUI() {
            githubUpdateChecker.Render();
        }

Tested in editor windows but should work inside custom inspectors.

## Rate limiting

Auto-check is disabled by default because **GitHub rate limits your IP to 60 requests per hour**. If multiple Unity scripts use this plugin it will cause them all to break. Use that toggle wisely.

## Properties - Configuration

All of these properties are public so you can configure it as much as you want and so you can build your own checker.

\* = required

### string githubOwner\*

The owner of the repo.

### string githubRepo\*

The name of the repo to check.

### string currentVersion\*

The current version to use when checking for new versions.

Supports any version you can give to `System.Version` and can include a "v". eg. `1.0.0` or `v1.0.0`

### bool initialCheck: true

If to check for new versions when the script starts.

### bool autoCheck: false

If to automatically check for versions every minute (the maximum for rate-limiting).

### bool allowManualCheck: false

Allow the user to click a button to manually check for versions. Disabled by default to avoid rate-limiting issues.

### bool allowDownload: true

Allow the user to download the 1st asset of the release to their Downloads folder (`~/Downloads/owner_repo/myfile.asset`). If no binary is set, the sourcecode ZIP is downloaded.

### bool allowInstall: true

Allow the user to automatically install the asset (if it is a .unitypackage). The user always has the option of opening the containing folder.

## Properties - Other

### int autoCheckIntervalMs: 60000

The interval (in ms) for auto-checking. See rate-limiting section.

### States state

The state of the script - waiting, idle, checking for releases, downloading, error.

### Release availableRelease

A GitHub release if it is newer than our release (or null if none found).

### string downloadedReleaseAssetPath

The path to the asset that was downloaded (or null if none downloaded yet).

## Ideas for future

- detect if any assets have already been downloaded
- disable manual check button if it has been less than 1 minute to avoid rate-limiting
