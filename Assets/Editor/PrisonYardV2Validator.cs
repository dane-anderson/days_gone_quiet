using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrisonYardV2Validator
{
    private const int ExpectedColliderCount = 3;
    private const int ExpectedRigidbodyCount = 2;

    [MenuItem("Days Gone Quiet/Validate Prison Yard V2", priority = 100)]
    private static void ValidateOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        ValidationReport report = new ValidationReport(scene);

        report.Check(scene.IsValid() && scene.isLoaded,
            "Active scene is loaded", scene.path);
        report.Check(scene.name == "PrisonYard",
            "Active scene is PrisonYard", scene.name);

        List<Collider> colliders = ComponentsInScene<Collider>(scene);
        List<Rigidbody> rigidbodies = ComponentsInScene<Rigidbody>(scene);
        report.Check(colliders.Count == ExpectedColliderCount,
            $"Exactly {ExpectedColliderCount} colliders",
            $"{colliders.Count}: {DescribeComponents(colliders)}");
        report.Check(rigidbodies.Count == ExpectedRigidbodyCount,
            $"Exactly {ExpectedRigidbodyCount} rigidbodies",
            $"{rigidbodies.Count}: {DescribeComponents(rigidbodies)}");

        GameObject rick = FindUnique(scene, "Rick", report);
        GameObject walker = FindUnique(scene, "Walker", report);
        GameObject ground = FindUnique(scene, "Ground", report);

        RickAgent rickAgent = rick != null ? rick.GetComponent<RickAgent>() : null;
        WalkerChase walkerChase = walker != null ? walker.GetComponent<WalkerChase>() : null;
        BehaviorParameters behavior = rick != null ? rick.GetComponent<BehaviorParameters>() : null;
        DecisionRequester requester = rick != null ? rick.GetComponent<DecisionRequester>() : null;

        report.Check(rickAgent != null, "Rick has RickAgent");
        report.Check(walkerChase != null, "Walker has WalkerChase");
        report.Check(rick != null && rick.GetComponent<Rigidbody>() != null,
            "Rick has Rigidbody");
        report.Check(walker != null && walker.GetComponent<Rigidbody>() != null,
            "Walker has Rigidbody");
        Collider rickCollider = rick != null ? rick.GetComponent<Collider>() : null;
        Collider walkerCollider = walker != null ? walker.GetComponent<Collider>() : null;
        Collider groundCollider = ground != null ? ground.GetComponent<Collider>() : null;
        report.Check(rickCollider != null && !rickCollider.isTrigger,
            "Rick has a solid Collider");
        report.Check(walkerCollider != null && walkerCollider.isTrigger,
            "Walker has a trigger Collider");
        report.Check(groundCollider is MeshCollider && !groundCollider.isTrigger,
            "Ground has a solid MeshCollider");
        report.Check(rickAgent != null && walker != null && rickAgent.walker == walker.transform,
            "RickAgent walker reference points to Walker");
        report.Check(walkerChase != null && rick != null && walkerChase.target == rick.transform,
            "WalkerChase target points to Rick");

        report.Check(behavior != null && behavior.enabled,
            "Rick has an enabled BehaviorParameters");
        if (behavior != null)
        {
            report.Check(behavior.BrainParameters.VectorObservationSize == 6,
                "Vector observation size is 6",
                behavior.BrainParameters.VectorObservationSize.ToString());
            report.Check(behavior.BrainParameters.ActionSpec.NumContinuousActions == 2,
                "Continuous action count is 2",
                behavior.BrainParameters.ActionSpec.NumContinuousActions.ToString());
            report.Check(behavior.BrainParameters.ActionSpec.NumDiscreteActions == 0,
                "Discrete action count is 0",
                behavior.BrainParameters.ActionSpec.NumDiscreteActions.ToString());
            report.Info("Behavior name", behavior.BehaviorName);
        }

        report.Check(requester != null && requester.enabled,
            "Rick has an enabled DecisionRequester");
        if (requester != null)
        {
            report.Check(requester.DecisionPeriod >= 1 && requester.DecisionPeriod <= 20,
                "Decision period is valid", requester.DecisionPeriod.ToString());
            report.Check(requester.DecisionStep >= 0 && requester.DecisionStep < requester.DecisionPeriod,
                "Decision step is within the period", requester.DecisionStep.ToString());
        }

        ValidateExperimentControls(scene, rickAgent, walkerChase, requester, report);
        ValidateEnvironment(scene, report);
        ValidateCamera(scene, rick, report);

        report.WriteToConsole();
    }

    private static void ValidateExperimentControls(
        Scene scene,
        RickAgent rickAgent,
        WalkerChase walkerChase,
        DecisionRequester requester,
        ValidationReport report)
    {
        List<RLExperimentControls> controls = ComponentsInScene<RLExperimentControls>(scene);
        report.Check(controls.Count == 1,
            "Exactly one RLExperimentControls exists", controls.Count.ToString());
        if (controls.Count != 1)
            return;

        SerializedObject serializedControls = new SerializedObject(controls[0]);
        report.Check(ReadObjectReference(serializedControls, "rickAgent") == rickAgent,
            "Experiment controls reference RickAgent");
        report.Check(ReadObjectReference(serializedControls, "walkerChase") == walkerChase,
            "Experiment controls reference WalkerChase");
        report.Check(ReadObjectReference(serializedControls, "decisionRequester") == requester,
            "Experiment controls reference DecisionRequester");
    }

    private static void ValidateEnvironment(Scene scene, ValidationReport report)
    {
        GameObject environment = FindUnique(scene, "Prison Yard Environment", report);
        Transform artPass = environment != null
            ? environment.transform.Find("V2 Art Pass")
            : null;
        report.Check(artPass != null,
            "Prison Yard Environment/V2 Art Pass exists");

        if (artPass != null)
        {
            int artColliders = artPass.GetComponentsInChildren<Collider>(true).Length;
            int artRigidbodies = artPass.GetComponentsInChildren<Rigidbody>(true).Length;
            report.Check(artColliders == 0,
                "V2 Art Pass has no colliders", artColliders.ToString());
            report.Check(artRigidbodies == 0,
                "V2 Art Pass has no rigidbodies", artRigidbodies.ToString());
        }
    }

    private static void ValidateCamera(Scene scene, GameObject rick, ValidationReport report)
    {
        List<Camera> cameras = ComponentsInScene<Camera>(scene);
        Camera mainCamera = null;
        foreach (Camera camera in cameras)
        {
            if (camera.CompareTag("MainCamera"))
            {
                mainCamera = camera;
                break;
            }
        }

        report.Check(mainCamera != null, "A MainCamera-tagged Camera exists");
        PrisonYardCameraViews views = mainCamera != null
            ? mainCamera.GetComponent<PrisonYardCameraViews>()
            : null;
        report.Check(views != null && views.enabled,
            "Main Camera has an enabled PrisonYardCameraViews controller");

        Array viewValues = Enum.GetValues(typeof(PrisonYardCameraViews.YardView));
        report.Check(viewValues.Length == 5,
            "Camera controller defines five views", viewValues.Length.ToString());
        report.Check(HasAllViewMethods(),
            "Camera controller exposes all five view commands");

        if (views != null)
        {
            report.Check(Enum.IsDefined(typeof(PrisonYardCameraViews.YardView), views.CurrentView),
                "Current camera view is valid", views.CurrentView.ToString());

            SerializedObject serializedViews = new SerializedObject(views);
            report.Check(ReadObjectReference(serializedViews, "rick") ==
                         (rick != null ? rick.transform : null),
                "Camera controller Rick reference is assigned");
        }
    }

    private static bool HasAllViewMethods()
    {
        Type type = typeof(PrisonYardCameraViews);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        return type.GetMethod("ShowOverview", flags) != null &&
               type.GetMethod("ShowFrontGate", flags) != null &&
               type.GetMethod("ShowCourtyard", flags) != null &&
               type.GetMethod("ShowGuardTower", flags) != null &&
               type.GetMethod("ShowFollowRick", flags) != null;
    }

    private static UnityEngine.Object ReadObjectReference(SerializedObject owner, string propertyName)
    {
        SerializedProperty property = owner.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static GameObject FindUnique(Scene scene, string objectName, ValidationReport report)
    {
        List<GameObject> matches = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    matches.Add(transform.gameObject);
            }
        }

        report.Check(matches.Count == 1,
            $"Exactly one {objectName} exists", matches.Count.ToString());
        return matches.Count == 1 ? matches[0] : null;
    }

    private static List<T> ComponentsInScene<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded)
            return components;

        foreach (GameObject root in scene.GetRootGameObjects())
            components.AddRange(root.GetComponentsInChildren<T>(true));
        return components;
    }

    private static string DescribeComponents<T>(List<T> components) where T : Component
    {
        List<string> descriptions = new List<string>();
        foreach (T component in components)
            descriptions.Add($"{component.gameObject.name}/{component.GetType().Name}");
        descriptions.Sort(StringComparer.Ordinal);
        return descriptions.Count > 0 ? string.Join(", ", descriptions) : "none";
    }

    private sealed class ValidationReport
    {
        private readonly StringBuilder lines = new StringBuilder();
        private readonly Scene scene;
        private int passed;
        private int failed;

        public ValidationReport(Scene validatedScene)
        {
            scene = validatedScene;
        }

        public void Check(bool condition, string label, string detail = null)
        {
            if (condition)
            {
                passed++;
                lines.Append("PASS  ").Append(label);
            }
            else
            {
                failed++;
                lines.Append("FAIL  ").Append(label);
            }

            if (!string.IsNullOrEmpty(detail))
                lines.Append(" [").Append(detail).Append(']');
            lines.AppendLine();
        }

        public void Info(string label, string detail)
        {
            lines.Append("INFO  ").Append(label).Append(": ").Append(detail).AppendLine();
        }

        public void WriteToConsole()
        {
            string status = failed == 0 ? "PASS" : "FAIL";
            string message =
                $"PRISON YARD V2 VALIDATION: {status}\n" +
                $"Scene: {scene.path}\n" +
                $"Checks: {passed} passed, {failed} failed\n\n" +
                lines;

            if (failed == 0)
                Debug.Log(message);
            else
                Debug.LogError(message);
        }
    }
}
