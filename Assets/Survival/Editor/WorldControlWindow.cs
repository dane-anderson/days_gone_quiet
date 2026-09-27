#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor-only controls: nothing is drawn over the Game view.</summary>
public sealed class WorldControlWindow : EditorWindow
{
    private SurvivalWorld world;
    private PrisonYardCameraViews views;
    private PrisonInteriorTour tour;
    private Vector2 scroll;
    private string[] scenePaths = Array.Empty<string>();
    private string[] sceneNames = Array.Empty<string>();
    private int selectedScene;
    private int actorFocus;
    private PrisonYardCameraViews.YardView focusView;
    private double lastCameraTick;
    private int lastCameraFrame = -1;
    private bool focusInside;
    private const float ActorCameraSmoothing = 6f;
    private double nextRepaint;
    private bool showTraining;
    private string doorFeedback;
    private string modelFeedback;
    private const string TrainingConfig = "Assets/Survival/Config/survival_config.yaml";
    private const string TrainCommand = "/opt/homebrew/Caskroom/miniconda/base/envs/rick-rl/bin/mlagents-learn Assets/Survival/Config/survival_config.yaml --results-dir=../results --run-id=survival-separate-brains";
    private const string DashboardCommand = "/opt/homebrew/Caskroom/miniconda/base/envs/rick-rl/bin/tensorboard --logdir ../results --port 6006";
    private static readonly string[] CameraNames =
    {
        "1  Yard", "2  Front gate", "3  Courtyard", "4  Tower",
        "5  Follow Rick", "6  Cellblock", "7  Tied walkers", "8  Cutaway"
    };

    private bool TrainerConnected => Application.isPlaying && Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;
    private bool Touring => tour != null && tour.IsTourActive;

    [MenuItem("Days Gone Quiet/World Control", priority = 1)]
    public static void Open()
    {
        Type inspector = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        WorldControlWindow window = GetWindow<WorldControlWindow>("World Control", inspector ?? typeof(SceneView));
        window.minSize = new Vector2(350f, 440f);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("World Control");
        minSize = new Vector2(350f, 440f);
        RefreshScenes();
        RefreshReferences();
        lastCameraTick = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorTick;
        EditorApplication.playModeStateChanged += PlayStateChanged;
        EditorApplication.hierarchyChanged += RefreshReferences;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorTick;
        EditorApplication.playModeStateChanged -= PlayStateChanged;
        EditorApplication.hierarchyChanged -= RefreshReferences;
        actorFocus = 0;
        lastCameraFrame = -1;
    }

    private void PlayStateChanged(PlayModeStateChange state)
    {
        actorFocus = 0;
        lastCameraTick = EditorApplication.timeSinceStartup;
        lastCameraFrame = -1;
        RefreshReferences();
        Repaint();
    }

    private void EditorTick()
    {
        double now = EditorApplication.timeSinceStartup;
        // Editor ticks are independent of simulation speed and can outnumber Game
        // frames. Sample each rendered frame once, using elapsed real time.
        bool cameraFrameReady = !Application.isPlaying || EditorApplication.isPaused || lastCameraFrame != Time.frameCount;
        if (actorFocus != 0 && !Touring && cameraFrameReady)
        {
            float delta = Mathf.Clamp((float)(now - lastCameraTick), 0f, .1f);
            lastCameraTick = now;
            lastCameraFrame = Time.frameCount;
            UpdateActorCamera(!Application.isPlaying, delta);
        }
        else if (actorFocus == 0 || Touring)
            lastCameraTick = now;
        if (EditorApplication.timeSinceStartup >= nextRepaint)
        {
            nextRepaint = EditorApplication.timeSinceStartup + 0.15;
            if (world == null || views == null)
                RefreshReferences();
            Repaint();
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(7f);
        EditorGUILayout.LabelField("DAYS GONE QUIET", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("World, survivors and training", EditorStyles.miniLabel);
        DrawLocation();
        DrawCameras();
        if (world == null)
        {
            EditorGUILayout.HelpBox("This scene has no survival world. Open PrisonYard for Rick and Negan. Other scenes keep their existing behavior.", MessageType.Info);
            DrawTour();
            EditorGUILayout.EndScrollView();
            return;
        }
        DrawSession();
        DrawSurvivors();
        DrawSettings();
        DrawDoors();
        DrawTour();
        DrawTraining();
        GUILayout.Space(10f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawLocation()
    {
        Section("Location");
        EditorGUILayout.LabelField("Open scene", SceneManager.GetActiveScene().name);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal();
            if (scenePaths.Length > 0)
                selectedScene = EditorGUILayout.Popup(selectedScene, sceneNames);
            if (GUILayout.Button("Open", GUILayout.Width(54f)) && scenePaths.Length > 0)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    actorFocus = 0;
                    EditorSceneManager.OpenScene(scenePaths[selectedScene]);
                    RefreshReferences();
                    GUIUtility.ExitGUI();
                }
            }
            if (GUILayout.Button("↻", GUILayout.Width(27f))) RefreshScenes();
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawCameras()
    {
        Section("Camera");
        using (new EditorGUI.DisabledScope(views == null || Touring))
        {
            for (int row = 0; row < 4; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < 2; column++)
                {
                    int index = row * 2 + column;
                    if (GUILayout.Button(CameraNames[index], GUILayout.Height(24f)))
                    {
                        actorFocus = 0;
                        RecordCamera();
                        views.ShowView((PrisonYardCameraViews.YardView)index);
                        if (tour != null) tour.RefreshExteriorVisibility();
                        DirtyCamera();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(world == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Focus Rick")) SetActorFocus(1);
                if (GUILayout.Button("Focus Negan")) SetActorFocus(2);
                if (GUILayout.Button("Focus both")) SetActorFocus(3);
                EditorGUILayout.EndHorizontal();
            }
        }
        if (actorFocus != 0)
            EditorGUILayout.LabelField("Following " + (actorFocus == 1 ? "Rick" : actorFocus == 2 ? "Negan" : "both survivors") + " · choose a camera to stop", EditorStyles.miniLabel);
    }

    private void DrawSession()
    {
        Section("Simulation");
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Press Play to start the prison survival simulation.", MessageType.None);
            if (GUILayout.Button("Play simulation", GUILayout.Height(29f))) EditorApplication.isPlaying = true;
            return;
        }
        string state = Touring ? "Manual exploration" : world.IsPaused ? "Paused" : TrainerConnected ? "Trainer connected" : world.scriptedPreview ? "Scripted preview — not learned" : "Policy / idle — see each survivor";
        EditorGUILayout.LabelField(state, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Round " + world.Round + "   ·   " + world.Elapsed.ToString("0.0") + " / " + world.episodeSeconds.ToString("0") + " seconds");
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(Touring))
        {
            if (GUILayout.Button(world.IsPaused ? "Resume" : "Pause", GUILayout.Height(27f))) world.SetPaused(!world.IsPaused);
            if (GUILayout.Button("Reset round", GUILayout.Height(27f))) world.ResetRound();
        }
        if (GUILayout.Button("Stop", GUILayout.Width(58f), GUILayout.Height(27f)))
        {
            if (Touring) tour.EndTour();
            EditorApplication.isPlaying = false;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSurvivors()
    {
        Section("Survivors");
        SurvivorRow(world.rick, "Rick", "Escape and use doors");
        SurvivorRow(world.negan, "Negan", "Fight with Lucille");
    }

    private static void SurvivorRow(SurvivalAgent survivor, string name, string purpose)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(name + "  ·  " + purpose, EditorStyles.boldLabel);
        if (survivor == null)
            EditorGUILayout.LabelField("Not present in this scene");
        else
        {
            string state = !Application.isPlaying ? "Ready in scene" : !survivor.enabled ? "Outside selected training focus" : !survivor.IsAlive ? "Down" : "Active";
            EditorGUILayout.LabelField(state + "   ·   Health " + survivor.Health.ToString("0") + "   ·   Kills " + survivor.Kills);
            EditorGUILayout.LabelField(survivor.PolicyStatus ?? "Idle", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Select " + name, EditorStyles.miniButton)) Selection.activeGameObject = survivor.gameObject;
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawSettings()
    {
        Section("Round settings");
        using (new EditorGUI.DisabledScope(Touring))
        {
            EditorGUI.BeginChangeCheck();
            SurvivalWorld.TrainingFocus focus = (SurvivalWorld.TrainingFocus)EditorGUILayout.Popup("Training focus", (int)world.trainingFocus,
                new[] { "Rick — escape", "Negan — bat combat", "Both — separate brains" });
            bool preview = EditorGUILayout.Toggle(new GUIContent("Scripted preview", "Demonstrates navigation and combat without claiming learned behavior. A connected trainer or assigned model takes precedence."), world.scriptedPreview);
            float speed;
            using (new EditorGUI.DisabledScope(Application.isPlaying && !TrainerConnected))
                speed = EditorGUILayout.Slider("Training speed", world.simulationSpeed, 1f, 20f);
            float seconds = EditorGUILayout.Slider("Round length", world.episodeSeconds, 10f, 180f);
            int available = world.walkers != null ? world.walkers.Length : 0;
            int count = EditorGUILayout.IntSlider("Active walkers", world.activeWalkerCount, 0, Mathf.Max(0, available));
            float walkerSpeed = EditorGUILayout.Slider("Walker speed", world.walkerSpeed, 0.2f, 4f);
            float health = EditorGUILayout.Slider("Walker health", world.walkerHealth, 10f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(world, "Change survival settings");
                world.trainingFocus = focus;
                world.scriptedPreview = preview;
                world.simulationSpeed = speed;
                world.episodeSeconds = seconds;
                world.activeWalkerCount = count;
                world.walkerSpeed = walkerSpeed;
                world.walkerHealth = health;
                Dirty(world);
            }
            EditorGUILayout.LabelField("Changing focus or walker count starts a new round. Health applies on reset.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Use 1 to watch full movements; higher values fast-forward training. Preview runs at normal speed.", EditorStyles.wordWrappedMiniLabel);
            if (!Application.isPlaying && GUILayout.Button("Save scene settings"))
                EditorSceneManager.SaveScene(world.gameObject.scene);
        }
    }

    private void DrawDoors()
    {
        Section("Doors");
        if (world.doors == null || world.doors.Length == 0)
        {
            EditorGUILayout.LabelField("No interactive doors in this scene.");
            return;
        }
        foreach (SurvivalDoor door in world.doors)
        {
            if (door == null) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(door.displayName) ? door.name : door.displayName);
            EditorGUILayout.LabelField(door.IsOpen ? "Open" : "Closed", GUILayout.Width(43f));
            if (GUILayout.Button(door.IsOpen ? "Close" : "Open", GUILayout.Width(60f)))
            {
                bool targetOpen = !door.IsOpen;
                Undo.RecordObject(door, "Change prison door");
                if (door.leaf != null) Undo.RecordObject(door.leaf, "Move prison door");
                if (door.blockingCollider != null) Undo.RecordObject(door.blockingCollider, "Change door collision");
                if (door.obstacle != null) Undo.RecordObject(door.obstacle, "Change door navigation");
                door.SetOpen(targetOpen);
                doorFeedback = door.IsOpen != targetOpen ? "Doorway occupied — move characters clear before closing." : null;
                if (!Application.isPlaying) door.startOpen = door.IsOpen;
                Dirty(door);
            }
            EditorGUILayout.EndHorizontal();
        }
        if (!string.IsNullOrEmpty(doorFeedback)) EditorGUILayout.HelpBox(doorFeedback, MessageType.Info);
        EditorGUILayout.LabelField("Closing waits for a clear doorway. Edit-mode choices become each round's starting state.", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawTour()
    {
        Section("Manual exploration");
        EditorGUILayout.LabelField(Touring ? "World paused. WASD / arrows move · Shift runs · E uses a nearby door · T exits." : "Explore as Rick. The simulation pauses and returns to its previous state afterward.", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || TrainerConnected || tour == null || !tour.HasTourActor || views == null))
        {
            if (GUILayout.Button(Touring ? "End manual tour  [T]" : "Start manual tour  [T]", GUILayout.Height(26f)))
            {
                actorFocus = 0;
                if (Touring) tour.EndTour(); else tour.StartTour();
            }
        }
        if (TrainerConnected) EditorGUILayout.LabelField("Manual touring is unavailable while a trainer is connected.", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawTraining()
    {
        GUILayout.Space(9f);
        showTraining = EditorGUILayout.Foldout(showTraining, "Training and dashboard", true);
        if (!showTraining) return;
        EditorGUILayout.HelpBox("RickEscape and NeganCombat are separate policies. Scripted preview is a demonstration, not learned teamwork. Choose a focus, reset the round, and use the trainer to learn or resume a brain.", MessageType.Info);
        DrawModels();
        EditorGUILayout.LabelField("From the Unity project folder, paste this command, wait for the trainer, then press Play:", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.SelectableLabel(TrainCommand, EditorStyles.textArea, GUILayout.Height(72f));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy training command")) EditorGUIUtility.systemCopyBuffer = TrainCommand;
        if (GUILayout.Button("Show config"))
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TrainingConfig));
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.LogWarning("Training config is not present at " + path);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("For the learning charts, run in another project terminal:", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.SelectableLabel(DashboardCommand, EditorStyles.textArea, GUILayout.Height(48f));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy dashboard command")) EditorGUIUtility.systemCopyBuffer = DashboardCommand;
        if (GUILayout.Button("Open dashboard")) Application.OpenURL("http://localhost:6006");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Dashboard requires TensorBoard to be running. Stop a trainer with Ctrl+C; use --resume with the same run ID to continue it. A model's presence does not establish skill — evaluate survival and combat outcomes.", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawModels()
    {
        EditorGUILayout.LabelField("Assigned brains", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawModelField(world.rick, "Rick brain");
            DrawModelField(world.negan, "Negan brain");
            if (GUILayout.Button("Load newest imported brains"))
            {
                int assigned = 0;
                if (LoadNewestImportedModel(world.rick, "RickEscape")) assigned++;
                if (LoadNewestImportedModel(world.negan, "NeganCombat")) assigned++;
                modelFeedback = assigned == 0 ? "No matching imported brains found. Import RickEscape.onnx and NeganCombat.onnx into Assets/Survival/Models first."
                    : "Assigned " + assigned + " imported brain" + (assigned == 1 ? ". The other brain was left unchanged." : "s. Save scene settings to keep them.");
            }
        }
        if (Application.isPlaying)
            EditorGUILayout.LabelField("Stop Play mode to assign and save a different brain.", EditorStyles.wordWrappedMiniLabel);
        if (!string.IsNullOrEmpty(modelFeedback))
            EditorGUILayout.HelpBox(modelFeedback, MessageType.Info);
        EditorGUILayout.LabelField("Trainer exports are saved in the parent results folder. Unity can use a brain after its .onnx file is imported into Assets/Survival/Models.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("Use RickEscape and NeganCombat exports here. The original Rick brain belongs to the old scene.", EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Show training results"))
        {
            string results = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", "results"));
            if (Directory.Exists(results)) EditorUtility.RevealInFinder(results);
            else modelFeedback = "The results folder will appear when the trainer saves its first run.";
        }
        GUILayout.Space(6f);
    }

    private static void DrawModelField(SurvivalAgent survivor, string label)
    {
        BehaviorParameters behavior = survivor != null ? survivor.GetComponent<BehaviorParameters>() : null;
        using (new EditorGUI.DisabledScope(behavior == null))
        {
            EditorGUI.BeginChangeCheck();
            ModelAsset next = (ModelAsset)EditorGUILayout.ObjectField(label, behavior != null ? behavior.Model : null, typeof(ModelAsset), false);
            if (EditorGUI.EndChangeCheck() && behavior != null)
                AssignModel(behavior, next);
        }
    }

    private static bool LoadNewestImportedModel(SurvivalAgent survivor, string policyName)
    {
        BehaviorParameters behavior = survivor != null ? survivor.GetComponent<BehaviorParameters>() : null;
        if (behavior == null) return false;
        string[] paths = AssetDatabase.FindAssets(policyName, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), policyName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetExtension(path), ".onnx", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => File.GetLastWriteTimeUtc(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)))).ToArray();
        foreach (string path in paths)
        {
            ModelAsset model = AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
            if (model == null) continue;
            AssignModel(behavior, model);
            return true;
        }
        return false;
    }

    private static void AssignModel(BehaviorParameters behavior, ModelAsset model)
    {
        Undo.RecordObject(behavior, "Assign survivor brain");
        behavior.BehaviorType = BehaviorType.Default;
        behavior.Model = model;
        Dirty(behavior);
    }

    private void SetActorFocus(int target)
    {
        if (world == null || views == null) return;
        actorFocus = target;
        RecordCamera();
        focusView = views.CurrentView;
        focusInside = focusView == PrisonYardCameraViews.YardView.CellblockCutaway;
        lastCameraTick = EditorApplication.timeSinceStartup;
        lastCameraFrame = -1;
        UpdateActorCamera(!Application.isPlaying, 0f);
        DirtyCamera();
    }

    private void UpdateActorCamera(bool immediate, float unscaledDelta)
    {
        if (world == null || views == null || views.CurrentView != focusView)
        {
            actorFocus = 0;
            return;
        }
        Transform a = actorFocus == 2 ? world.negan != null ? world.negan.transform : null : world.rick != null ? world.rick.transform : null;
        Transform b = actorFocus == 3 && world.negan != null ? world.negan.transform : a;
        if (a == null) a = b;
        if (a == null) return;
        Vector3 center = b != null ? (a.position + b.position) * 0.5f : a.position;
        // A little hysteresis around the doorway prevents repeated roof/view
        // transitions when a learner moves back and forth near the threshold.
        focusInside = IsFocusInside(a.position) || (b != null && IsFocusInside(b.position));
        PrisonYardCameraViews.YardView requiredView = focusInside ? PrisonYardCameraViews.YardView.CellblockCutaway : PrisonYardCameraViews.YardView.Overview;
        if (focusView != requiredView)
        {
            focusView = requiredView;
            Vector3 position = views.transform.position;
            Quaternion rotation = views.transform.rotation;
            Camera activeCamera = views.GetComponent<Camera>();
            float fieldOfView = activeCamera != null ? activeCamera.fieldOfView : 52f;
            views.ShowView(focusView);
            // ShowView selects enclosure visibility. Its preset pose must not
            // interrupt an actor-follow transition that is already in progress.
            views.transform.SetPositionAndRotation(position, rotation);
            if (activeCamera != null) activeCamera.fieldOfView = fieldOfView;
            if (tour != null) tour.RefreshExteriorVisibility();
        }
        float span = b != null ? Vector3.Distance(a.position, b.position) : 0f;
        float distance = Mathf.Clamp(8f + span * 0.85f, 8f, 35f);
        Vector3 offset = new Vector3(0f, distance * 0.9f, -distance);
        Vector3 desiredPosition = center + offset;
        Quaternion desiredRotation = Quaternion.LookRotation(-offset + Vector3.up * 0.5f);
        float blend = immediate ? 1f : 1f - Mathf.Exp(-ActorCameraSmoothing * unscaledDelta);
        views.transform.SetPositionAndRotation(Vector3.Lerp(views.transform.position, desiredPosition, blend),
            Quaternion.Slerp(views.transform.rotation, desiredRotation, blend));
        Camera camera = views.GetComponent<Camera>();
        if (camera != null) camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, 52f, blend);
        if (!Application.isPlaying) SceneView.RepaintAll();
    }

    private bool IsFocusInside(Vector3 position)
    {
        float margin = focusInside ? .45f : -.25f;
        return Mathf.Abs(position.x) < 17f + margin && position.z > 29f - margin && position.z < 48f + margin;
    }

    private void RecordCamera()
    {
        if (Application.isPlaying || views == null) return;
        Undo.RecordObject(views.transform, "Choose world camera");
        Undo.RecordObject(views, "Choose world camera");
        Camera camera = views.GetComponent<Camera>();
        if (camera != null) Undo.RecordObject(camera, "Choose world camera");
    }

    private void DirtyCamera()
    {
        if (views != null) Dirty(views);
    }

    private static void Dirty(UnityEngine.Object item)
    {
        if (Application.isPlaying || item == null) return;
        EditorUtility.SetDirty(item);
        Component component = item as Component;
        if (component != null && component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private void RefreshReferences()
    {
        world = SceneComponent<SurvivalWorld>();
        views = SceneComponent<PrisonYardCameraViews>();
        tour = SceneComponent<PrisonInteriorTour>();
    }

    private static T SceneComponent<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private void RefreshScenes()
    {
        scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.Contains("/Samples/")).OrderBy(path => path).ToArray();
        sceneNames = scenePaths.Select(path => Path.GetFileNameWithoutExtension(path) + "  (" + Path.GetDirectoryName(path) + ")").ToArray();
        selectedScene = Mathf.Max(0, Array.IndexOf(scenePaths, SceneManager.GetActiveScene().path));
    }

    private static void Section(string label)
    {
        GUILayout.Space(10f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }
}
#endif
