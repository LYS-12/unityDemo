using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class ForestPartySetup
{
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid = false)
    {
        var node = GameObject.CreatePrimitive(type);
        node.name = name;
        node.transform.SetParent(parent, false);
        node.transform.localPosition = position;
        node.transform.localScale = scale;
        node.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid)
            Object.DestroyImmediate(node.GetComponent<Collider>());
        return node;
    }
    private static void CreateParty(Transform party)
    {
        // Make one readable clearing without changing any shared tree asset.
        foreach (GameObject tree in Trees())
        {
            Vector3 delta = tree.transform.position - party.position;
            delta.y = 0;
            if (delta.magnitude < 7)
                tree.transform.position += delta.sqrMagnitude > .01f ? delta.normalized * (8 - delta.magnitude) : Vector3.right * 8;
        }
        var wood = Mat("HoneyWood", new Color(.49f, .29f, .16f));
        var cloth = Mat("PicnicCloth", new Color(.98f, .82f, .58f));
        var sage = Mat("PartySage", new Color(.50f, .68f, .49f));
        var warm = Mat("LanternGlow", new Color(1, .78f, .38f));
        Primitive("PicnicTable", party, PrimitiveType.Cube, new Vector3(0, .82f, .5f), new Vector3(3.1f, .18f, 1.4f), cloth, true);
        foreach (float x in new[] { -1.15f, 1.15f })
            Primitive("TableLeg", party, PrimitiveType.Cube, new Vector3(x, .38f, .5f), new Vector3(.18f, .8f, .85f), wood);
        foreach (float z in new[] { -.7f, 1.7f })
            Primitive("PicnicBench", party, PrimitiveType.Cube, new Vector3(0, .4f, z), new Vector3(3.4f, .16f, .38f), wood);
        Primitive("PartyMat", party, PrimitiveType.Cylinder, new Vector3(0, .025f, .3f), new Vector3(8, .02f, 7), sage);
        for (int i = 0; i < 5; i++)
        {
            float x = -3 + i * 1.5f;
            float height = 3.5f + Mathf.Abs(x) * .16f;
            Primitive("WarmPaperLantern", party, PrimitiveType.Sphere, new Vector3(x, height, 2.8f), new Vector3(.35f, .43f, .35f), warm);
            if (i % 2 == 0)
            {
                var light = new GameObject("PartyWarmLight").AddComponent<Light>();
                light.transform.SetParent(party, false);
                light.transform.localPosition = new Vector3(x, height - .1f, 2.8f);
                light.type = LightType.Point;
                light.color = new Color(1, .79f, .42f);
                light.intensity = 1.5f;
                light.range = 6;
            }
        }
        for (int i = 0; i < 3; i++)
            Primitive("SnackPlate", party, PrimitiveType.Cylinder, new Vector3(i - .9f, .94f, .5f), new Vector3(.5f, .04f, .5f), Mat("PlateCream", new Color(1, .96f, .86f)));
        var line = new GameObject("LanternString").AddComponent<LineRenderer>();
        line.transform.SetParent(party, false);
        line.useWorldSpace = false;
        line.positionCount = 9;
        line.widthMultiplier = .025f;
        line.sharedMaterial = wood;
        for (int i = 0; i < 9; i++)
        {
            float x = -3.5f + i * .875f;
            line.SetPosition(i, new Vector3(x, 3.5f + Mathf.Abs(x) * .16f, 2.8f));
        }
    }
    private static GameObject[] Trees()
    {
        return Object.FindObjectsOfType<Transform>().Select(t => PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject)).Where(obj => obj != null)
            .Distinct().Where(obj =>
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj).ToLowerInvariant();
                return path.Contains("/trees/") || Path.GetFileNameWithoutExtension(path).StartsWith("tree") || path.Contains("树");
            }).ToArray();
    }
    private static void CreateLeaves(int level)
    {
        Physics.SyncTransforms();
        var root = new GameObject("ForestAtmosphere");
        var leaves = root.AddComponent<ForestLeaves>();
        leaves.leafMesh = LeafMesh();
        leaves.leafMaterial = Mat("FallenLeaf", new Color(.68f, .62f, .31f));
        GameObject[] trees = Trees();
        var canopies = new List<Vector3>();
        var instances = new List<CombineInstance>();
        var random = new System.Random(700 + level);
        foreach (var tree in trees)
        {
            Vector3 position = tree.transform.position;
            if (canopies.All(p => Vector3.Distance(p, position + Vector3.up * 4.5f) > 5.5f))
                canopies.Add(position + Vector3.up * 4.5f);
            for (int i = 0; i < 14; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = .7f + (float)random.NextDouble() * 1.7f;
                Vector3 p = position + new Vector3(Mathf.Cos(angle) * radius, .07f, Mathf.Sin(angle) * radius);
                if (Physics.Raycast(p + Vector3.up * 3, Vector3.down, out RaycastHit hit, 6, ~LayerMask.GetMask("PlayerCombat", "WorldItem", "Enemy", "Companion", "ItemProjectile"), QueryTriggerInteraction.Ignore) && hit.normal.y > .7f)
                    p = hit.point + Vector3.up * .025f;
                else
                    continue;
                instances.Add(new CombineInstance { mesh = leaves.leafMesh, transform = Matrix4x4.TRS(p, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0), Vector3.one * (.12f + (float)random.NextDouble() * .1f)) });
            }
        }
        leaves.canopyPositions = canopies.Take(45).ToArray();
        var combined = new Mesh { name = "ForestGroundLeaves" + level, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        combined.CombineMeshes(instances.ToArray(), true, true);
        combined = Save(combined, Root + "/Art/Models/ForestParty/GroundLeaves" + level + ".asset");
        var litter = new GameObject("BatchedGroundLeaves", typeof(MeshFilter), typeof(MeshRenderer));
        litter.transform.SetParent(root.transform, false);
        litter.GetComponent<MeshFilter>().sharedMesh = combined;
        litter.GetComponent<MeshRenderer>().sharedMaterial = leaves.leafMaterial;
        litter.isStatic = true;
        File.AppendAllText("Docs/forest-party-setup.txt", "Scene " + level + ": " + trees.Length + " trees, " + leaves.canopyPositions.Length + " falling-leaf zones, " + instances.Count + " combined ground leaves, no colliders.\n");
    }
    private static void ConfigureInput()
    {
        string path = Root + "/Scripts/Input/GameControl.inputactions";
        var asset = UnityEngine.InputSystem.InputActionAsset.FromJson(File.ReadAllText(path));
        var ui = asset.FindActionMap("UI", true);
        if (ui.FindAction("DialogueAdvance") == null)
            ui.AddAction("DialogueAdvance", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/e");
        if (ui.FindAction("SkipCinematic") == null)
            ui.AddAction("SkipCinematic", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/escape");
        File.WriteAllText(path, asset.ToJson());
        AssetDatabase.ImportAsset(path);
        string code = UnityEngine.InputSystem.Editor.InputActionCodeGenerator.GenerateWrapperCode(asset, new UnityEngine.InputSystem.Editor.InputActionCodeGenerator.Options { className = "GameControl", sourceAssetPath = path });
        File.WriteAllText(Root + "/Scripts/Input/GameControl.cs", code);
        Object.DestroyImmediate(asset);
    }
}
