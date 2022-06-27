#pragma warning disable CS4014,CS1998
using UnityEditor;
using UnityEngine;

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net.Http;
using Octokit;

public class GitHub_Update_Checker {
    public enum States {
        Waiting,
        Idle,
        Checking,
        Downloading,
        Error
    }

    private static readonly GitHubClient github = new GitHubClient(new ProductHeaderValue("GitHub_Update_Checker"));
    private static readonly int maximumSecondsBetweenRequests = 60; 

    // user settings
    public string githubOwner; // "imagitama"
    public string githubRepo; // "Unity-GitHub-Update-Checker"
    public string currentVersion; // "1.2.3"
    public bool initialCheck = true;
    public bool autoCheck = false; // disabled by default to prevent rate limit issues
    public bool allowManualCheck = false; // disabled by default to prevent rate limit issues
    public bool allowDownload = true;
    public bool allowInstall = true;

    public States state = States.Waiting;
    public int autoCheckIntervalMs = 60000; // warning: github rate limits your IP to 60 requests per hour
    public Octokit.Release availableRelease;
    public string downloadedReleaseAssetPath;

    public GitHub_Update_Checker() {
        if (initialCheck) {
            Debug.Log("Performing initial check...");

            CheckForAvailableReleaseWithDelay();
        } else {
            Debug.Log("Skipping initial check");
        }

        if (autoCheck) {
            StartAutoChecking();
        } else {
            Debug.Log("Skipping auto-check");
        }
    }

    int GetNow() {
        return (int)DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    int GetTimestampOfLastCheck() {
        return EditorPrefs.GetInt("GitHub_Update_Checker_LastCheck", GetNow());
    }

    int GetSecondsSinceLastCheck() {
        return GetNow() - GetTimestampOfLastCheck();
    }

    void UpdateTimestampOfLastCheck() {
        EditorPrefs.SetInt("GitHub_Update_Checker_LastCheck", GetNow());
    }

    async Task<IReadOnlyList<Octokit.Release>> GetAllReleases() {   
        Debug.Log("Getting all releases...");

        var releases = await github.Repository.Release.GetAll("imagitama", "vrc-questifyer");

        Debug.Log("Found " + releases.Count + " releases");

        return releases;
    }

    async Task StartAutoChecking() {
        Debug.Log("Starting auto-checking every " + autoCheckIntervalMs + "ms...");

        await CheckForAvailableReleaseWithDelay();

        while (autoCheck == true) {
            await Task.Delay(autoCheckIntervalMs);

            if (state != States.Downloading) {
                await CheckForAvailableReleaseWithDelay();
            }
        }
    }

    public string GetVersionNumber(string versionStr) {
        if (versionStr.Substring(0, 1).ToLower() == "v") {
            return versionStr.Substring(1);
        }
        return versionStr;
    }

    public async Task CheckForAvailableReleaseWithDelay() {
        try {
            int secondsSinceLastCheck = GetSecondsSinceLastCheck();

            if (secondsSinceLastCheck < maximumSecondsBetweenRequests) {
                int secondsToWait = maximumSecondsBetweenRequests - secondsSinceLastCheck;

                Debug.Log("It has been " + secondsSinceLastCheck + " seconds since our last check, waiting " + secondsToWait + " seconds...");
                
                await Task.Delay(secondsToWait * 1000);
                
                Debug.Log("Wait complete, checking...");
            }
            
            await CheckForAvailableRelease();
        } catch (Exception exception) {
            state = States.Error;
            HandleException(exception);
        }
    }

    public async Task CheckForAvailableRelease() {
        try {
            state = States.Checking;

            UpdateTimestampOfLastCheck();

            var releases = await GetAllReleases();
            Version bestVersionSoFar = new Version(GetVersionNumber(currentVersion));
            Octokit.Release possibleAvailableRelease = null;

            foreach (var release in releases) {
                if (release.TagName == null || release.TagName == "") {
                    throw new Exception("Release does not have a tag name!");
                }

                string thisVersionNumber = GetVersionNumber(release.TagName);
                Version thisVersion = new Version(thisVersionNumber);

                if (thisVersion.CompareTo(bestVersionSoFar) == 1) {
                    bestVersionSoFar = thisVersion;
                    possibleAvailableRelease = release;
                }
            }

            if (possibleAvailableRelease != null) {
                Debug.Log("Version " + bestVersionSoFar.ToString() + " is available");

                availableRelease = possibleAvailableRelease;
            }
            
            state = States.Idle;
        } catch (Exception exception) {
            state = States.Error;
            HandleException(exception);
        }
        
        ForceRefreshGUI();
    }

    void ManuallyCheckForAvailableRelease() {
        int secondsSinceLastCheck = GetSecondsSinceLastCheck();

        if (secondsSinceLastCheck < maximumSecondsBetweenRequests) {
            Debug.LogWarning("You are manually checking even though it has only been " + secondsSinceLastCheck.ToString() + " seconds since last check!");
        }

        CheckForAvailableRelease();
    }

    void HandleException(Exception exception) {
        Debug.LogError(exception);
    }

    // source: https://stackoverflow.com/a/7672816/1215393
    public static string GetDownloadsPath() {
        SHGetKnownFolderPath(new Guid("{374DE290-123F-4565-9164-39C4925E467B}"), 0x00004000, new IntPtr(0), out var PathPointer);
        var Result = Marshal.PtrToStringUni(PathPointer);
        Marshal.FreeCoTaskMem(PathPointer);
        return Result;
    }

    Octokit.ReleaseAsset GetBestReleaseAsset(Octokit.Release release) {
        if (release.Assets.Count >= 1) {
            return release.Assets[0];
        } else {
            return null;
        }
    }

    [DllImport("Shell32.dll")]
    static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    public async Task DownloadAvailableRelease() {
        try {
            state = States.Downloading;

            Debug.Log("Downloading available release...");

            Debug.Log("There are " + availableRelease.Assets.Count + " assets for release \"" + availableRelease.Name + "\"");

            Octokit.ReleaseAsset bestAsset = GetBestReleaseAsset(availableRelease);

            string downloadUrl = "";
            string downloadPath = GetDownloadsPath();

            if (bestAsset != null) {
                Debug.Log("Downloading the best asset \"" + bestAsset.Name + "\"...");

                downloadUrl = bestAsset.BrowserDownloadUrl;
                downloadPath += "\\" + bestAsset.Name;
            } else {
                Debug.Log("Best asset not found, using source code zip...");

                downloadUrl = availableRelease.ZipballUrl;
                downloadPath += "\\" + availableRelease.TagName + ".zip";
            }

            if (File.Exists(downloadPath)) {
                Debug.Log("File already exists (" + downloadPath + "), deleting...");

                File.Delete(downloadPath);
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

            var response = await client.GetAsync(downloadUrl);

            Debug.Log("Destination: " + downloadPath);

            using (var fs = new FileStream(downloadPath, System.IO.FileMode.CreateNew)) {
                await response.Content.CopyToAsync(fs);
            }

            downloadedReleaseAssetPath = downloadPath;

            Debug.Log("File has been downloaded");

            state = States.Idle;

            ForceRefreshGUI();
        } catch (Exception exception) {
            HandleException(exception);
        }
    }

    void ForceRefreshGUI() {
        GUI.FocusControl(null);
    }

    public async Task InstallDownloadedAsset() {
        System.Diagnostics.Process.Start(downloadedReleaseAssetPath);
    }

    bool CanAssetBeInstalled(string pathToAsset) {
        return pathToAsset.Contains(".unitypackage");
    }

    bool RenderButton(string label) {
        return GUILayout.Button(label, GUILayout.Width(150), GUILayout.Height(50));
    }

    void RenderText(string text) {
        var labelStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 16,
            alignment = TextAnchor.UpperCenter
        };

        GUILayout.Label(text, labelStyle);
    }

    void RenderGap() {
        EditorGUILayout.Space();
    }

    void OpenInExplorer(string itemPath) {
        itemPath = itemPath.Replace(@"/", @"\");
        System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
    }

    void OpenUrl(string url) {
        UnityEngine.Application.OpenURL(url);
    }

    public void Render() {
        if (githubRepo == null || currentVersion == null) {
            return;
        }

        switch (state) {
            case States.Waiting:
                RenderText("Waiting to check for updates...");
                return;
            case States.Checking:
                RenderText("Checking for updates...");
                RenderGap();
                break;
            case States.Error:
                RenderText("Failed to check for updates");
                RenderGap();
                break;
            case States.Downloading:
                RenderText("Downloading...");
                RenderGap();
                break;
            default:
                if (availableRelease != null) {
                    RenderText("A newer version is available: " + availableRelease.TagName);

                    RenderGap();

                    if (downloadedReleaseAssetPath != null) {
                        RenderText("Package has been downloaded");

                        RenderGap();

                        if (allowInstall && CanAssetBeInstalled(downloadedReleaseAssetPath)) {
                            if (RenderButton("Install")) {
                                InstallDownloadedAsset();
                                
                                RenderGap();
                            }
                        }

                        if (RenderButton("Open Folder")) {
                            OpenInExplorer(downloadedReleaseAssetPath);
                        }
                    } else {
                        if (allowDownload) {
                            if (RenderButton("Download")) {
                                DownloadAvailableRelease();
                                
                                RenderGap();
                            }
                        }

                        if (RenderButton("View on GitHub")) {
                            OpenUrl("https://www.github.com/" + githubOwner + "/" + githubRepo + "/releases/tag/" + availableRelease.TagName);
                        }
                    }
                } else {
                    RenderText("No new version is available");
                }
            break;
        }

        if (state == States.Idle && allowManualCheck) {
            RenderGap();

            if (RenderButton("Check For Updates")) {
                ManuallyCheckForAvailableRelease();
            }
        }
    }
}