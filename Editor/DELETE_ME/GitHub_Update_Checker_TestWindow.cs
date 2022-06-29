
using UnityEditor;
using UnityEngine;

public class GitHub_Update_Checker_TestWindow : EditorWindow {
    GitHub_Update_Checker githubUpdateChecker;

    [MenuItem("PeanutTools/GitHub_Update_Checker_TestWindow")]
    public static void ShowWindow() {
        var window = GetWindow<GitHub_Update_Checker_TestWindow>();
        window.titleContent = new GUIContent("GitHub_Update_Checker_TestWindow");
        window.minSize = new Vector2(250, 50);
    }

    void OnEnable() {
        githubUpdateChecker = new GitHub_Update_Checker() {
            githubOwner = "imagitama",
            githubRepo = "webextension-boilerplate",
            currentVersion = "v1.0.0",
            allowManualCheck = true,
            allowInstall = false
        };
    }

    void OnGUI() {
        githubUpdateChecker.Render();
    }
}