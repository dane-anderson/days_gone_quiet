#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PrisonYardCameraViews))]
public class PrisonYardCameraViewsEditor : Editor
{
    private static bool showLearningLab = true;
    private static bool showDifficulty = true;
    private static bool showRewards = true;
    private static bool showArenaAndReset = false;
    private static bool showEpisodeAndDecisions = false;
    private static bool showTrainingSpeed = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PrisonYardCameraViews views = (PrisonYardCameraViews)target;
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Prison Yard Views", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click a view below. In Play mode, press 1–5 to switch without stopping the game.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1  Full Yard", GUILayout.Height(32f)))
                ApplyView(views, views.ShowOverview, "Full Yard View");
            if (GUILayout.Button("2  Front Gate", GUILayout.Height(32f)))
                ApplyView(views, views.ShowFrontGate, "Front Gate View");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("3  Courtyard", GUILayout.Height(32f)))
                ApplyView(views, views.ShowCourtyard, "Courtyard View");
            if (GUILayout.Button("4  Guard Tower", GUILayout.Height(32f)))
                ApplyView(views, views.ShowGuardTower, "Guard Tower View");
        }

        if (GUILayout.Button("5  Follow Rick", GUILayout.Height(36f)))
            ApplyView(views, views.ShowFollowRick, "Follow Rick View");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Current view", Nicify(views.CurrentView));

        DrawLearningLab();
    }

    private static void DrawLearningLab()
    {
        EditorGUILayout.Space(10f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            showLearningLab = EditorGUILayout.Foldout(
                showLearningLab,
                "Rick Learning Lab — Adjustable Attributes",
                true,
                EditorStyles.foldoutHeader);

            if (!showLearningLab)
                return;

            RLExperimentControls controls = FindAnyObjectByType<RLExperimentControls>();
            if (controls == null)
            {
                EditorGUILayout.HelpBox(
                    "No RL Experiment Controls component was found in this scene.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "You are in Play mode. Changes work live, but Unity will undo them when Play mode stops."
                    : "These values are saved with the scene and control Rick's learning experiment.",
                Application.isPlaying ? MessageType.Warning : MessageType.Info);

            SerializedObject learningSettings = new SerializedObject(controls);
            learningSettings.Update();
            EditorGUI.BeginChangeCheck();

            DrawSettingsGroup(learningSettings, ref showDifficulty, "Difficulty",
                "rickMoveSpeed", "walkerMoveSpeed");
            DrawSettingsGroup(learningSettings, ref showRewards, "Rewards",
                "fullEpisodeSurvivalReward", "caughtPenalty", "boundaryPenalty");
            DrawSettingsGroup(learningSettings, ref showArenaAndReset, "Arena And Reset",
                "arenaHalfExtent", "walkerSpawnHalfExtent", "minimumWalkerSpawnDistance", "walkerSpawnY");
            DrawSettingsGroup(learningSettings, ref showEpisodeAndDecisions, "Episode And Decisions",
                "maxEpisodeSteps", "decisionPeriod");
            DrawSettingsGroup(learningSettings, ref showTrainingSpeed, "Training Speed",
                "trainingTimeScale");

            bool changed = EditorGUI.EndChangeCheck();
            learningSettings.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(controls);
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(controls.gameObject.scene);
            }

            EditorGUILayout.Space(5f);
            if (GUILayout.Button("Open Days Gone Quiet Dashboard", GUILayout.Height(30f)))
                EditorApplication.ExecuteMenuItem("Days Gone Quiet/Open Training Dashboard");
        }
    }

    private static void DrawSettingsGroup(
        SerializedObject settings,
        ref bool expanded,
        string label,
        params string[] propertyNames)
    {
        expanded = EditorGUILayout.Foldout(expanded, label, true);
        if (!expanded)
            return;

        EditorGUI.indentLevel++;
        foreach (string propertyName in propertyNames)
        {
            SerializedProperty property = settings.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }
        EditorGUI.indentLevel--;
    }

    [MenuItem("Days Gone Quiet/Add Prison Yard Camera Views")]
    private static void AddCameraViews()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Prison Yard Camera Views", "The scene needs a Main Camera first.", "OK");
            return;
        }

        PrisonYardCameraViews views = camera.GetComponent<PrisonYardCameraViews>();
        if (views == null)
            views = Undo.AddComponent<PrisonYardCameraViews>(camera.gameObject);

        Undo.RecordObjects(new UnityEngine.Object[] { views, camera, camera.transform }, "Add Prison Yard Camera Views");
        views.RestoreDefaultSettings();
        views.ShowOverview();
        EditorUtility.SetDirty(views);
        EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        Selection.activeGameObject = camera.gameObject;
        Debug.Log("Prison Yard camera views are ready. Select Main Camera or press 1–5 in Play mode.");
    }

    private static void ApplyView(PrisonYardCameraViews views, Action action, string undoName)
    {
        Camera camera = views.GetComponent<Camera>();
        Undo.RecordObjects(new UnityEngine.Object[] { views, camera, views.transform }, undoName);
        action();
        EditorUtility.SetDirty(views);
        EditorUtility.SetDirty(camera);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(views.gameObject.scene);

        SceneView.RepaintAll();
    }

    private static string Nicify(PrisonYardCameraViews.YardView view)
    {
        return ObjectNames.NicifyVariableName(view.ToString());
    }
}
#endif
