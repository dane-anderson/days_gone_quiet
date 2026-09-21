#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

public static class DaysGoneQuietDashboardLauncher
{
    private const string DashboardUrl = "http://127.0.0.1:6006";
    private const string TensorBoardExecutable =
        "/opt/homebrew/Caskroom/miniconda/base/envs/rick-rl/bin/tensorboard";
    private const int DashboardPort = 6006;
    private const int MaximumStartupChecks = 60;

    private static int startupChecks;
    private static double nextStartupCheck;

    [MenuItem("Days Gone Quiet/Open Training Dashboard", priority = 1)]
    public static void OpenTrainingDashboard()
    {
        if (IsDashboardRunning())
        {
            Application.OpenURL(DashboardUrl);
            return;
        }

        if (!File.Exists(TensorBoardExecutable))
        {
            EditorUtility.DisplayDialog(
                "TensorBoard Not Found",
                "The rick-rl TensorBoard executable could not be found. " +
                "The rick-rl environment may need to be restored.",
                "OK");
            return;
        }

        string resultsDirectory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "results"));

        if (!Directory.Exists(resultsDirectory))
            Directory.CreateDirectory(resultsDirectory);

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = TensorBoardExecutable,
                Arguments =
                    $"--logdir \"{resultsDirectory}\" --port {DashboardPort} --host 127.0.0.1",
                WorkingDirectory = Path.GetDirectoryName(resultsDirectory),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
            startupChecks = 0;
            nextStartupCheck = 0d;
            EditorApplication.update -= WaitForDashboard;
            EditorApplication.update += WaitForDashboard;
            UnityEngine.Debug.Log(
                "Starting the Days Gone Quiet training dashboard at " + DashboardUrl);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Dashboard Could Not Start",
                "TensorBoard could not be started. Check the Unity Console for details.",
                "OK");
        }
    }

    private static void WaitForDashboard()
    {
        if (EditorApplication.timeSinceStartup < nextStartupCheck)
            return;

        nextStartupCheck = EditorApplication.timeSinceStartup + 0.25d;
        startupChecks++;

        if (IsDashboardRunning())
        {
            EditorApplication.update -= WaitForDashboard;
            Application.OpenURL(DashboardUrl);
            return;
        }

        if (startupChecks < MaximumStartupChecks)
            return;

        EditorApplication.update -= WaitForDashboard;
        EditorUtility.DisplayDialog(
            "Dashboard Is Taking Too Long",
            "TensorBoard did not become ready. Check the Unity Console for details.",
            "OK");
    }

    private static bool IsDashboardRunning()
    {
        try
        {
            using TcpClient client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", DashboardPort).Wait(75) &&
                client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

[CustomEditor(typeof(RLExperimentControls))]
[CanEditMultipleObjects]
public class RLExperimentControlsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(12f);

        if (GUILayout.Button(
                "Open Days Gone Quiet Dashboard",
                GUILayout.Height(34f)))
        {
            DaysGoneQuietDashboardLauncher.OpenTrainingDashboard();
        }

        EditorGUILayout.HelpBox(
            "Starts TensorBoard when needed, then opens Rick's live training charts.",
            MessageType.Info);
    }
}
#endif
