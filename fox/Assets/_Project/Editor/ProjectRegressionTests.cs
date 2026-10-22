using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FoxProject.Tests
{
    public class ItemTransferTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        private KitchenObjectHolder CreateHolder(bool hasHoldPoint = true)
        {
            var root = new GameObject("Test holder");
            objects.Add(root);
            KitchenObjectHolder holder = root.AddComponent<KitchenObjectHolder>();
            if (hasHoldPoint)
            {
                var point = new GameObject("Hold point");
                point.transform.SetParent(root.transform);
                SetField(holder, "holdPoint", point.transform);
            }
            return holder;
        }

        private KitchenObject CreateItem()
        {
            var root = new GameObject("Test item");
            objects.Add(root);
            return root.AddComponent<KitchenObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in objects)
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        [Test]
        public void TransferUpdatesBothOwnersAndParentInBothDirections()
        {
            KitchenObjectHolder first = CreateHolder();
            KitchenObjectHolder second = CreateHolder();
            KitchenObject item = CreateItem();
            first.transform.localScale = Vector3.one * 2f;
            second.transform.localScale = Vector3.one * 3f;
            Assert.IsTrue(first.TryAddKitchenObject(item));
            Vector3 originalWorldScale = item.transform.lossyScale;
            Assert.IsTrue(KitchenObjectHolder.TryTransferKitchenObject(first, second));
            Assert.IsNull(first.GetKitchenObject());
            Assert.AreSame(item, second.GetKitchenObject());
            Assert.AreSame(second.GetHoldPoint(), item.transform.parent);
            Assert.AreEqual(Vector3.zero, item.transform.localPosition);
            Assert.That(Vector3.Distance(originalWorldScale, item.transform.lossyScale), Is.LessThan(0.0001f));
            Assert.IsTrue(KitchenObjectHolder.TryTransferKitchenObject(second, first));
            Assert.IsNull(second.GetKitchenObject());
            Assert.AreSame(item, first.GetKitchenObject());
            Assert.That(Vector3.Distance(originalWorldScale, item.transform.lossyScale), Is.LessThan(0.0001f));
        }

        [Test]
        public void OccupiedTargetDoesNotLoseEitherItem()
        {
            KitchenObjectHolder first = CreateHolder();
            KitchenObjectHolder second = CreateHolder();
            KitchenObject firstItem = CreateItem();
            KitchenObject secondItem = CreateItem();
            first.TryAddKitchenObject(firstItem);
            second.TryAddKitchenObject(secondItem);
            Assert.IsFalse(KitchenObjectHolder.TryTransferKitchenObject(first, second));
            Assert.AreSame(firstItem, first.GetKitchenObject());
            Assert.AreSame(secondItem, second.GetKitchenObject());
        }

        [Test]
        public void MissingHoldPointNullAndSelfTransfersKeepSourceIntact()
        {
            KitchenObjectHolder first = CreateHolder();
            KitchenObjectHolder invalid = CreateHolder(false);
            KitchenObject item = CreateItem();
            first.TryAddKitchenObject(item);
            Assert.IsFalse(KitchenObjectHolder.TryTransferKitchenObject(first, invalid));
            Assert.IsFalse(KitchenObjectHolder.TryTransferKitchenObject(first, first));
            Assert.IsFalse(KitchenObjectHolder.TryTransferKitchenObject(null, first));
            Assert.IsFalse(KitchenObjectHolder.TryTransferKitchenObject(first, null));
            Assert.AreSame(item, first.GetKitchenObject());
            Assert.AreSame(first.GetHoldPoint(), item.transform.parent);
        }

        public static void SetField(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }
            Assert.NotNull(field, name);
            field.SetValue(target, value);
        }
    }

    public class JsonDataStoreTests
    {
        [Serializable]
        public class TestData
        {
            public int value;
        }
        private string fileName;
        private string savedPath;
        private string defaultPath;
        private bool createdStreamingDirectory;

        [SetUp]
        public void SetUp()
        {
            fileName = "fox_regression_" + Guid.NewGuid().ToString("N");
            savedPath = Path.Combine(Application.persistentDataPath, fileName + ".json");
            defaultPath = Path.Combine(Application.streamingAssetsPath, fileName + ".json");
            createdStreamingDirectory = !Directory.Exists(Application.streamingAssetsPath);
            Directory.CreateDirectory(Application.streamingAssetsPath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string path in new[] { savedPath, defaultPath, defaultPath + ".meta" })
                if (File.Exists(path))
                    File.Delete(path);
            if (createdStreamingDirectory && Directory.Exists(Application.streamingAssetsPath) &&
                Directory.GetFileSystemEntries(Application.streamingAssetsPath).Length == 0)
            {
                Directory.Delete(Application.streamingAssetsPath);
                if (File.Exists(Application.streamingAssetsPath + ".meta"))
                    File.Delete(Application.streamingAssetsPath + ".meta");
            }
        }

        [TestCase(JsonType.LitJson)]
        [TestCase(JsonType.JsonUtility)]
        public void SavedDataTakesPriorityOverDefaultData(JsonType type)
        {
            File.WriteAllText(defaultPath, "{\"value\":1}");
            JsonDataStore.Instance.SaveData(new TestData { value = 42 }, fileName, type);
            Assert.AreEqual(42, JsonDataStore.Instance.LoadData<TestData>(fileName, type).value);
        }

        [TestCase(JsonType.LitJson)]
        [TestCase(JsonType.JsonUtility)]
        public void CorruptSaveFallsBackToDefaults(JsonType type)
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(savedPath, "{broken");
            File.WriteAllText(defaultPath, "{\"value\":8}");
            Assert.AreEqual(8, JsonDataStore.Instance.LoadData<TestData>(fileName, type).value);
        }

        [Test]
        public void MissingFilesReturnNewData()
        {
            Assert.AreEqual(0, JsonDataStore.Instance.LoadData<TestData>(fileName).value);
        }

        [TestCase("../outside")]
        [TestCase("sub/file")]
        [TestCase("sub\\file")]
        public void DirectoryNamesAreRejected(string name)
        {
            Assert.Throws<ArgumentException>(() => JsonDataStore.Instance.LoadData<TestData>(name));
        }
    }

    public class SceneFlowSmokeTests
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator MenuSettingsAllLevelsCompletionAndRepeatedTransitions()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/StartScene.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (UIManager.Instance.GetPanel<StartPanel>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.NotNull(UIManager.Instance.GetPanel<StartPanel>());
            var invalidGoalObject = new GameObject("Invalid goal regression");
            FoxPlacementGoal invalidGoal = invalidGoalObject.AddComponent<FoxPlacementGoal>();
            LogAssert.Expect(LogType.Error, "狐狸摆放目标配置不完整。");
            yield return null;
            Assert.IsFalse(invalidGoal.enabled, "Unconfigured goals must not complete.");
            Assert.IsNull(UIManager.Instance.GetPanel<LevelSelectPanel>());
            UnityEngine.Object.Destroy(invalidGoalObject);
            Click(UIManager.Instance.GetPanel<StartPanel>(), "settingBtn");
            yield return null;
            Assert.NotNull(UIManager.Instance.GetPanel<SettingsPanel>());
            Click(UIManager.Instance.GetPanel<SettingsPanel>(), "btnExit");
            yield return null;
            Assert.NotNull(UIManager.Instance.GetPanel<StartPanel>());
            Click(UIManager.Instance.GetPanel<StartPanel>(), "levelSelectBtn");
            yield return null;
            Assert.NotNull(UIManager.Instance.GetPanel<LevelSelectPanel>());
            for (int level = 1; level <= 3; level++)
            {
                LevelSelectPanel select = UIManager.Instance.GetPanel<LevelSelectPanel>();
                Click(select, "btnCard" + level);
                Assert.IsFalse(SceneLoader.Load(SceneLoader.Scene.StartScene), "Concurrent requests must be rejected.");
                yield return WaitForScene("GameScene" + level);
                UnityEngine.Object.FindObjectOfType<StoryCinematic>()?.Skip();
                float storyDeadline = Time.realtimeSinceStartup + 1.1f;
                while (Time.realtimeSinceStartup < storyDeadline)
                    yield return null;
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<SceneUIRoot>().Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length);
                Player player = Player.Instance;
                Assert.NotNull(player);
                AssertInputLifecycle(player);
                MethodInfo interact = typeof(Player).GetMethod("HandleInteractAction", BindingFlags.Instance | BindingFlags.NonPublic);
                player.SetSelectedCounter(null);
                Assert.DoesNotThrow(() => interact.Invoke(player, new object[] { null, EventArgs.Empty }));
                Assert.IsTrue(HasOrderPanel(level));
                if (level == 1)
                {
                    FoxEscortQuest quest = UnityEngine.Object.FindObjectOfType<FoxEscortQuest>();
                    foreach (FoxCompanion fox in quest.foxes)
                    {
                        Assert.IsTrue(fox.BeginApology());
                        fox.CompleteApology();
                        fox.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(fox.arrivalPoint.position);
                        Assert.IsTrue(fox.TryArrive());
                    }
                    quest.cinematic.Skip();
                    float endDeadline = Time.realtimeSinceStartup + 1.1f;
                    while (Time.realtimeSinceStartup < endDeadline)
                        yield return null;
                }
                if (level == 2)
                    CompleteGoal<IngredientPlacementGoal>(new[] { "tomatoCounter", "cheeseCounter", "bread" }, new[] { "tomatoDefinition", "cheeseDefinition", "breadDefinition" });
                if (level == 3)
                    CompleteGoal<FinalPlacementGoal>(new[] { "counter" }, new[] { "requiredItem" });
                yield return null;
                yield return null;
                if (level < 3)
                    Assert.NotNull(UIManager.Instance.GetPanel<LevelSelectPanel>());
                else
                {
                    Assert.NotNull(UIManager.Instance.GetPanel<CompletionPanel>());
                    Click(UIManager.Instance.GetPanel<CompletionPanel>(), "btn");
                    yield return WaitForScene("StartScene");
                    Assert.NotNull(UIManager.Instance.GetPanel<StartPanel>());
                }
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.IsTrue(SceneLoader.Load(SceneLoader.Scene.GameScene1, () => UIManager.Instance.ShowPanel<OrderPanel1>()));
                yield return WaitForScene("GameScene1");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<SceneUIRoot>().Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length);
                Assert.IsTrue(SceneLoader.Load(SceneLoader.Scene.StartScene, () => UIManager.Instance.ShowPanel<StartPanel>()));
                yield return WaitForScene("StartScene");
                Assert.IsNull(Player.Instance);
                Assert.IsNull(UIManager.Instance.GetPanel<OrderPanel1>());
            }
            yield return new ExitPlayMode();
        }

        private static bool HasOrderPanel(int level)
        {
            return level == 1 ? UnityEngine.Object.FindObjectOfType<FoxEscortQuest>() != null :
                level == 2 ? UIManager.Instance.GetPanel<OrderPanel2>() != null : UIManager.Instance.GetPanel<OrderPanel3>() != null;
        }

        private static void AssertInputLifecycle(Player player)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            GameInput input = (GameInput)typeof(Player).GetField("gameInput", flags).GetValue(player);
            FieldInfo subscribers = typeof(GameInput).GetField("OnInteractAction", flags);
            int CountSubscriptions()
            {
                EventHandler handlers = (EventHandler)subscribers.GetValue(input);
                int count = 0;
                if (handlers != null)
                    foreach (Delegate handler in handlers.GetInvocationList())
                        if (ReferenceEquals(handler.Target, player))
                            count++;
                return count;
            }
            Assert.AreEqual(1, CountSubscriptions());
            player.enabled = false;
            Assert.AreEqual(0, CountSubscriptions());
            player.enabled = true;
            Assert.AreEqual(1, CountSubscriptions());
            GameControl controls = (GameControl)typeof(GameInput).GetField("gameControl", flags).GetValue(input);
            input.enabled = false;
            Assert.IsFalse(controls.Player.enabled);
            input.enabled = true;
            Assert.IsTrue(controls.Player.enabled);
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            bool sawLoadingScene = false;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline)
            {
                sawLoadingScene |= SceneManager.GetActiveScene().name == "LoadScene";
                yield return null;
            }
            Assert.IsFalse(SceneLoader.IsLoading, "Scene load timed out.");
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            Assert.IsTrue(sawLoadingScene, "Loading scene must be visible to the update loop.");
            yield return null;
        }

        private static void Click(object panel, string field)
        {
            Assert.NotNull(panel);
            Button button = (Button)panel.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            Assert.NotNull(button);
            button.onClick.Invoke();
        }

        private static void CompleteGoal<T>(string[] counters, string[] items) where T : MonoBehaviour
        {
            T goal = UnityEngine.Object.FindObjectOfType<T>();
            Assert.NotNull(goal);
            for (int index = 0; index < counters.Length; index++)
            {
                ClearCounter counter = (ClearCounter)typeof(T).GetField(counters[index], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(goal);
                KitchenObjectSO definition = (KitchenObjectSO)typeof(T).GetField(items[index], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(goal);
                GameObject instance = UnityEngine.Object.Instantiate(definition.Prefab);
                Assert.IsTrue(counter.TryAddKitchenObject(instance.GetComponent<KitchenObject>()));
            }
        }
    }
}
