#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

public static class PrisonInteriorValidator
{
    [MenuItem("Days Gone Quiet/Validate Connected Prison Interior", priority = 101)]
    public static void Validate()
    {
        PrisonInteriorTour tour = UnityEngine.Object.FindAnyObjectByType<PrisonInteriorTour>();
        Require(tour != null, "Interior tour component exists");
        Require(tour.rick != null && tour.walker != null && tour.views != null && tour.tourSpawn != null, "Tour references are assigned");
        Transform cells = tour.transform.Find("Twelve Cells - Two Levels");
        Require(cells != null && cells.childCount == 12, "Twelve cells exist");
        RestrainedWalkerVisual[] captives = tour.GetComponentsInChildren<RestrainedWalkerVisual>();
        Require(captives.Length == 4, "Four restrained walkers exist");
        foreach (RestrainedWalkerVisual captive in captives)
        {
            Require(captive.GetComponentsInChildren<Collider>(true).Length == 0, "Restrained walker has no training collision");
            Require(captive.GetComponentInChildren<Agent>(true) == null && captive.GetComponentInChildren<WalkerChase>(true) == null, "Restrained walker is separate from the learning agents");
            Require(captive.GetComponentInChildren<SpriteRenderer>(true)?.sprite != null, "Restrained walker sprite assigned");
        }
        BehaviorParameters behavior = tour.rick.GetComponent<BehaviorParameters>();
        Require(behavior != null && behavior.Model != null, "Rick retains saved brain");
        Require(behavior.BrainParameters.VectorObservationSize == 6 && behavior.BrainParameters.ActionSpec.NumContinuousActions == 2, "Saved observation/action contract preserved");
        Require(tour.rick.walker == tour.walker.transform && tour.walker.target == tour.rick.transform, "Original chase references preserved");
        Require(tour.interiorColliders.Length > 20 && tour.exteriorRenderers.Length > 10, "Physical interior and cutaway assigned");
        Require(tour.transform.Find("Upper Walkway and Stairs/Smooth Stair Collision")?.GetComponent<Collider>() != null, "Stair ramp exists");
        Require(tour.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m != null)), "No missing materials");
        ValidateEnclosure(tour);
        Debug.Log("PRISON INTERIOR VALIDATION: PASS. Twelve cells, four separate restrained walkers, structural collision, cutaway, stair route, materials, and Rick's saved brain contract verified.");
    }

    [MenuItem("Days Gone Quiet/Test Interior Tour in Play Mode", priority = 102)]
    public static void TestTour()
    {
        Require(Application.isPlaying, "Enter Play mode before testing the tour");
        PrisonInteriorTour tour = UnityEngine.Object.FindAnyObjectByType<PrisonInteriorTour>();
        Require(tour != null && !tour.IsTourActive, "Tour exists and is stopped");
        RickAgent rick = tour.rick;
        BehaviorParameters behavior = rick.GetComponent<BehaviorParameters>();
        DecisionRequester requester = rick.GetComponent<DecisionRequester>();
        Vector3 before = rick.transform.position;
        int maxStep = rick.MaxStep;
        var model = behavior.Model;
        bool agentEnabled = rick.enabled, requesterEnabled = requester.enabled, walkerEnabled = tour.walker.enabled;
        try
        {
            tour.StartTour();
            Require(tour.IsTourActive && !rick.enabled && !requester.enabled && !tour.walker.enabled, "Tour pauses simulation");
            CharacterController cc = rick.GetComponent<CharacterController>();
            Require(cc != null && cc.enabled, "Tour uses physical character controller");
            Walk(cc, new Vector3(0, 0, 32));
            Require(rick.transform.position.z > 31.5f, "Yard door connects to interior");
            CheckTourCamera(tour);
            Walk(cc, new Vector3(4, 0, 32));
            Walk(cc, new Vector3(4, 0, 40));
            Walk(cc, new Vector3(4, 0, 44.2f));
            CheckTourCamera(tour);
            Require(tour.views.transform.position.y < 3.85f, "Ground-floor camera remains below the gallery");
            Walk(cc, new Vector3(4, 0, 38));
            Walk(cc, new Vector3(10.5f, 0, 38));
            Walk(cc, new Vector3(10.5f, 0, 30));
            Walk(cc, new Vector3(13, 0, 30));
            Walk(cc, new Vector3(13, 0, 40.3f));
            Require(rick.transform.position.y > 4.9f, "Rick climbs the stairs to the upper gallery");
            CheckTourCamera(tour);
            Walk(cc, new Vector3(9, 0, 40.3f));
            Walk(cc, new Vector3(9, 0, 44.2f));
            Require(rick.transform.position.z > 43.5f && rick.transform.position.y > 4.9f, "Rick enters an upper cell");
            CheckTourCamera(tour);
        }
        finally { tour.EndTour(); }
        Require(!tour.IsTourActive && rick.enabled == agentEnabled && requester.enabled == requesterEnabled && tour.walker.enabled == walkerEnabled, "Simulation resumes with original enabled states");
        Require(Vector3.Distance(before, rick.transform.position) < .01f && rick.MaxStep == maxStep && behavior.Model == model, "Tour restores pose, episode limit, and model");
        Debug.Log("PRISON TOUR PLAY TEST: PASS. Rick walked from the yard through the entrance, entered downstairs and upstairs cells with an enclosed camera, climbed to the gallery, and returned to the original simulation with the same model and episode settings.");
    }

    private static void CheckTourCamera(PrisonInteriorTour tour)
    {
        tour.views.SnapToTourView();
        tour.RefreshExteriorVisibility();
        Require(tour.exteriorRenderers.All(r => r == null || !r.forceRenderingOff), "Roof and walls stay visible during tours");
        Require(tour.views.transform.position.y < 8.6f, "Tour camera stays beneath the concrete ceiling");
        Vector3 offset = tour.views.transform.position - tour.rick.transform.position;
        Require(offset.magnitude > .3f && offset.magnitude < 4.5f, "Tour camera maintains a safe following distance");
    }

    private static void ValidateEnclosure(PrisonInteriorTour tour)
    {
        var previous = tour.views.CurrentView;
        Camera camera = tour.views.GetComponent<Camera>();
        try
        {
            tour.views.ShowCellblock();
            tour.RefreshExteriorVisibility();
            Physics.SyncTransforms();
            Require(tour.exteriorRenderers.All(r => r == null || !r.forceRenderingOff), "Indoor view keeps enclosure visible");
            int checkedRays = 0;
            foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f })
            {
                camera.aspect = aspect;
                for (int y = 0; y <= 10; y++)
                    for (int x = 0; x <= 16; x++)
                    {
                        Ray ray = camera.ViewportPointToRay(new Vector3(.02f + x * .96f / 16, .02f + y * .96f / 10, 0));
                        bool enclosed = Physics.RaycastAll(ray, 90, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Any(hit => {
                                Renderer r = hit.collider.GetComponent<Renderer>();
                                return r != null && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy
                                    && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                            });
                        Require(enclosed, "Indoor camera has no sky gap at viewport " + x + "," + y + " aspect " + aspect);
                        checkedRays++;
                    }
            }
            tour.views.ShowCellblockCutaway();
            tour.RefreshExteriorVisibility();
            Require(tour.exteriorRenderers.All(r => r == null || r.forceRenderingOff), "Optional cutaway hides enclosure");
            Debug.Log("PRISON ROOF CAMERA VALIDATION: PASS. " + checkedRays + " viewport rays hit visible enclosure at 4:3, 16:9 and 21:9; optional cutaway works separately.");
        }
        finally
        {
            camera.ResetAspect();
            tour.views.ShowView(previous);
            tour.RefreshExteriorVisibility();
        }
    }

    private static void Walk(CharacterController cc, Vector3 destination)
    {
        for (int i = 0; i < 700; i++)
        {
            Vector3 delta = destination - cc.transform.position;
            delta.y = 0;
            if (delta.magnitude < .07f) return;
            cc.Move(Vector3.ClampMagnitude(delta, .06f) + Vector3.down * .035f);
            Physics.SyncTransforms();
        }
        throw new InvalidOperationException("Route blocked near " + cc.transform.position + " while walking toward " + destination);
    }
    private static void Require(bool pass, string message)
    { if (!pass) throw new InvalidOperationException("PRISON VALIDATION FAILED: " + message); }
}
#endif
