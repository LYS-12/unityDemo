using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static class GameplayValidation
{
    [MenuItem("Tools/Fox/Validate Inventory And Combat")]
    public static void Validate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var notes = new List<string>();
        var errors = new List<string>();
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            GameplayCatalog catalog = Resources.Load<GameplayCatalog>("Gameplay/GameplayCatalog");
            Require(catalog != null, "GameplayCatalog missing", errors);
            if (catalog == null)
                throw new Exception(string.Join("\n", errors));
            Require(catalog.startingItems.Length == 4 && catalog.startingItems.Distinct().Count() == 4, "Expected four unique starting definitions", errors);
            Require(catalog.startingCount == 3 && catalog.initialHealth == 5 && catalog.healthCap == 10, "Unexpected stock/health defaults", errors);
            Require(catalog.hudPrefab != null && catalog.explosionMaterial != null, "HUD/explosion material missing", errors);
            foreach (string guid in AssetDatabase.FindAssets("t:KitchenObjectSO", new[] { "Assets/_Project/Data/Items" }))
            {
                KitchenObjectSO item = AssetDatabase.LoadAssetAtPath<KitchenObjectSO>(AssetDatabase.GUIDToAssetPath(guid));
                Require(item.Sprite != null && item.Prefab != null && item.maxDurability == 3, item.name + ": invalid icon/prefab/durability", errors);
                Require(item.Prefab != null && item.Prefab.GetComponent<WorldItemPickup>() != null, item.name + ": no world pickup", errors);
                Require(item.canThrow == (item.effect != ItemEffectKind.None), item.name + ": quest/combat policy mismatch", errors);
            }
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Project/Scripts/Input/GameControl.inputactions");
            foreach (string action in new[] { "Player/Move", "Player/Interact", "Player/Use", "Player/Throw", "UI/ToggleInventory", "UI/Stow", "UI/Cancel" })
                Require(input.FindAction(action) != null, "Missing input: " + action, errors);
            for (int level = 1; level <= 3; level++)
            {
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene" + level + ".unity", OpenSceneMode.Single);
                Player player = Object.FindObjectOfType<Player>();
                GameplayBootstrap bootstrap = Object.FindObjectOfType<GameplayBootstrap>();
                Require(player != null && player.GetComponent<PlayerItemController>() != null && player.GetComponent<PlayerHealth>() != null &&
                    player.GetComponent<PlayerEffects>() != null && player.GetComponent<Rigidbody>() != null, "GameScene" + level + ": player configuration incomplete", errors);
                Require(bootstrap != null && bootstrap.catalog == catalog && bootstrap.player == player && bootstrap.canvasRoot != null &&
                    bootstrap.canvasRoot.GetComponent<SceneUIRoot>() != null && bootstrap.GetComponent<ItemProjectilePool>() != null,
                    "GameScene" + level + ": bootstrap configuration incomplete", errors);
                EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
                Require(level == 1 ? spawner != null : spawner == null, "Wrong combat scene scope", errors);
                if (spawner == null)
                    continue;
                Require(spawner.navigation != null && spawner.enemyPrefab != null && spawner.spawnAnchors.Length == 3, "Missing navigation/enemy/anchors", errors);
                Require(spawner.spawnAnchors.All(anchor => anchor != null), "Fox search anchor missing", errors);
                FoxEscortQuest quest = Object.FindObjectOfType<FoxEscortQuest>();
                Require(quest != null && quest.foxes.Length == 3 && quest.cinematic != null && quest.cinematic.intro != null && quest.cinematic.reunion != null, "Fox quest/Timelines missing", errors);
                Require(Object.FindObjectOfType<FoxPlacementGoal>() == null, "Legacy first-level goal remains active", errors);
                NavMeshDataInstance navigation = NavMesh.AddNavMeshData(spawner.navigation);
                try
                {
                    Require(NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 3, NavMesh.AllAreas), "Player start is outside navigation", errors);
                    if (quest != null)
                        foreach (FoxCompanion fox in quest.foxes)
                            foreach (Vector3 point in new[] { fox.transform.position, fox.arrivalPoint.position })
                            {
                                var escortPath = new NavMeshPath();
                                Require(NavMesh.CalculatePath(point, start.position, NavMesh.AllAreas, escortPath) && escortPath.status == NavMeshPathStatus.PathComplete, "Fox search/arrival is disconnected: " + fox.name + " " + point, errors);
                            }
                    for (int anchorIndex = 0; anchorIndex < spawner.spawnAnchors.Length; anchorIndex++)
                    {
                        int connected = 0;
                        Transform anchor = spawner.spawnAnchors[anchorIndex];
                        for (int x = -4; x <= 4; x++)
                            for (int z = -4; z <= 4; z++)
                            {
                                if (x * x + z * z > 16)
                                    continue;
                                if (!NavMesh.SamplePosition(anchor.position + new Vector3(x, 0, z) * 0.5f, out NavMeshHit hit, 2, NavMesh.AllAreas))
                                    continue;
                                var path = new NavMeshPath();
                                if (NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                                    connected++;
                            }
                        notes.Add("Fox anchor " + anchorIndex + ": connected samples=" + connected);
                        Require(connected > 0, "Fox anchor " + anchorIndex + ": no connected candidate", errors);
                    }
                }
                finally { navigation.Remove(); }
                foreach (MeshFilter mesh in spawner.enemyPrefab.GetComponentsInChildren<MeshFilter>(true))
                    Require(AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/_Project/Art/Models/Enemies/"), "Enemy mesh references player source", errors);
                Animator animator = spawner.enemyPrefab.GetComponentInChildren<Animator>(true);
                Require(animator != null && AssetDatabase.GetAssetPath(animator.runtimeAnimatorController).Contains("/Enemies/"), "Enemy controller not independent", errors);
                if (animator != null)
                {
                    var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                    Require(controller != null && controller.parameters.Any(parameter => parameter.name == "isWalking" && parameter.type == AnimatorControllerParameterType.Bool), "Missing enemy walking parameter", errors);
                    Animator actualPlayerAnimator = player.GetComponentInChildren<Animator>(true);
                    Require(AssetDatabase.GetAssetPath(actualPlayerAnimator.runtimeAnimatorController).Contains("/Ghost/"), "Player ghost controller missing", errors);
                    foreach (string action in new[] { "IdleHover", "MoveFloat", "Wave", "Startle", "Bow" })
                        Require(actualPlayerAnimator.runtimeAnimatorController.animationClips.Any(clip => clip.name.EndsWith(action)), "Ghost clip missing: " + action, errors);
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                        Require(AssetDatabase.GetAssetPath(clip).Contains("/Enemies/"), "Enemy animation not independent: " + clip.name, errors);
                }
                notes.Add("Three gameplay scenes configured; only GameScene1 has a spawner. Enemy mesh/controller/clips independent.");
            }
        }
        finally
        {
            File.WriteAllLines("Docs/gameplay-reference-validation.txt", new[] { "Errors: " + errors.Count }.Concat(errors).Concat(notes));
            if (previous.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        if (errors.Count > 0)
            throw new Exception(string.Join("\n", errors));
        Debug.Log("Inventory/combat reference validation passed.");
    }

    private static void Require(bool condition, string error, List<string> errors)
    {
        if (!condition)
            errors.Add(error);
    }

    public static void ValidateAndBuild()
    {
        Validate();
        ProjectValidation.ValidateAndBuild();
    }
}
