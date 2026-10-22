using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FoxProject.Tests
{
    public class GameplayModelTests
    {
        private GameplayCatalog catalog;
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private RunSession session;

        [SetUp]
        public void SetUp()
        {
            GameplayPause.ClearScene();
            RunSession.End();
            catalog = ScriptableObject.CreateInstance<GameplayCatalog>();
            assets.Add(catalog);
            catalog.startingItems = new[] { Definition(ItemEffectKind.SpeedBoost), Definition(ItemEffectKind.Health),
                Definition(ItemEffectKind.Shield), Definition(ItemEffectKind.Explosion) };
            session = RunSession.BeginNew(catalog);
        }

        private KitchenObjectSO Definition(ItemEffectKind effect)
        {
            KitchenObjectSO definition = ScriptableObject.CreateInstance<KitchenObjectSO>();
            definition.effect = effect;
            assets.Add(definition);
            return definition;
        }

        [TearDown]
        public void TearDown()
        {
            RunSession.End();
            GameplayPause.ClearScene();
            foreach (ScriptableObject asset in assets)
                Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void SeedIsTwelveIndependentItemsAndEnsureDoesNotReseed()
        {
            Assert.AreEqual(12, session.Inventory.Items.Count);
            Assert.AreEqual(12, session.Inventory.Items.Select(item => item.id).Distinct().Count());
            foreach (KitchenObjectSO definition in catalog.startingItems)
                Assert.AreEqual(3, session.Inventory.Items.Count(item => item.definition == definition));
            Assert.AreSame(session, RunSession.Ensure(catalog));
            Assert.AreEqual(12, session.Inventory.Items.Count);
        }

        [Test]
        public void EquipStowDoesNotDuplicateAndThreeUsesRemoveOnlyOneInstance()
        {
            ItemInstance first = session.Inventory.Items[0];
            ItemInstance other = session.Inventory.Items[1];
            Assert.IsTrue(session.Inventory.Equip(first.id));
            Assert.IsTrue(session.Inventory.ConsumeUse(first));
            session.Inventory.Stow();
            Assert.IsTrue(session.Inventory.Equip(first.id));
            Assert.AreEqual(2, first.remainingDurability);
            Assert.AreEqual(3, other.remainingDurability);
            Assert.IsFalse(session.Inventory.Add(first));
            session.Inventory.ConsumeUse(first);
            session.Inventory.ConsumeUse(first);
            Assert.IsNull(session.Inventory.Equipped);
            Assert.AreEqual(11, session.Inventory.Items.Count);
            Assert.AreEqual(3, catalog.startingItems[0].maxDurability);
        }

        [Test]
        public void ThrowRemovalConsumesEntireItemWithRemainingDurability()
        {
            ItemInstance item = session.Inventory.Items[0];
            session.Inventory.Equip(item.id);
            Assert.IsTrue(session.Inventory.Remove(item));
            Assert.IsNull(session.Inventory.Find(item.id));
            Assert.IsNull(session.Inventory.Equipped);
            Assert.AreEqual(3, item.remainingDurability);
        }

        [Test]
        public void TomatoGrowsCapacityAndHealthAndCapsAtTen()
        {
            Assert.AreEqual(5, session.CurrentHealth);
            session.TryTakeDamage(1);
            Assert.IsTrue(session.UseHealth(1));
            Assert.AreEqual(6, session.MaxHealth);
            Assert.AreEqual(5, session.CurrentHealth);
            while (session.MaxHealth < 10)
                session.UseHealth(1);
            Assert.AreEqual(10, session.MaxHealth);
            Assert.IsTrue(session.UseHealth(1));
            Assert.AreEqual(10, session.CurrentHealth);
            Assert.IsFalse(session.UseHealth(1));
            session.Tick(1f);
            session.TryTakeDamage(3);
            Assert.IsTrue(session.UseHealth(1));
            Assert.AreEqual(8, session.CurrentHealth);
            Assert.AreEqual(10, session.MaxHealth);
        }

        [Test]
        public void ShieldsBlockFullAttacksAndProtectionPreventsExtraChargeLoss()
        {
            KitchenObjectSO cheese = catalog.startingItems[2];
            for (int index = 0; index < 3; index++)
                Assert.IsTrue(session.UseShield(cheese));
            Assert.IsFalse(session.UseShield(cheese));
            Assert.IsTrue(session.TryTakeDamage(100));
            Assert.AreEqual(5, session.CurrentHealth);
            Assert.AreEqual(2, session.ShieldCharges);
            Assert.IsFalse(session.TryTakeDamage(100));
            Assert.AreEqual(2, session.ShieldCharges);
            session.Tick(0.75f);
            Assert.IsTrue(session.TryTakeDamage(1));
            Assert.AreEqual(1, session.ShieldCharges);
        }

        [Test]
        public void SpeedRefreshesWithoutStackingAndExpires()
        {
            KitchenObjectSO butterfly = catalog.startingItems[0];
            session.UseSpeed(butterfly);
            session.Tick(4f);
            Assert.AreEqual(6f, session.BoostRemaining, 0.001f);
            session.UseSpeed(butterfly);
            Assert.AreEqual(10f, session.BoostRemaining, 0.001f);
            Assert.AreEqual(1.5f, session.BoostMultiplier);
            session.Tick(10f);
            Assert.AreEqual(1f, session.BoostMultiplier);
        }

        [Test]
        public void DeathIsOnlyRaisedOnceAndRetryCreatesFreshState()
        {
            int deaths = 0;
            session.Died += () => deaths++;
            Assert.IsTrue(session.TryTakeDamage(20));
            Assert.IsFalse(session.TryTakeDamage(20));
            Assert.AreEqual(1, deaths);
            Assert.IsFalse(session.UseHealth(1));
            RunSession retry = RunSession.BeginNew(catalog);
            Assert.AreEqual(5, retry.CurrentHealth);
            Assert.AreEqual(5, retry.MaxHealth);
            Assert.AreEqual(12, retry.Inventory.Items.Count);
            Assert.IsNull(retry.Inventory.Equipped);
        }

        [Test]
        public void PauseOwnersDoNotUnpauseEachOther()
        {
            var bag = new object();
            var death = new object();
            GameplayPause.Set(bag, true);
            GameplayPause.Set(death, true);
            GameplayPause.Set(bag, false);
            Assert.AreEqual(0f, Time.timeScale);
            GameplayPause.Set(death, false);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [TestCase(5f)]
        [TestCase(12f)]
        public void SharedBallisticsLandAtClampedGroundTarget(float distance)
        {
            Vector3 origin = Vector3.up * 1.2f;
            Vector3 target = Vector3.forward * distance;
            Vector3 velocity = Ballistics.LaunchVelocity(origin, target, Vector3.forward, 12f, 12f);
            float time = distance / 12f;
            Vector3 end = origin + velocity * time + Physics.gravity * 0.5f * time * time;
            Assert.That(Vector3.Distance(end, target), Is.LessThan(0.001f));
        }
    }

    public class GameplayIntegrationTests
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator RealInputsInventoryEffectsPoolingCrossSceneAndRetry()
        {
            RunSession.End();
            GameplayPause.ClearScene();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            Keyboard testKeyboard = Keyboard.current == null ? InputSystem.AddDevice<Keyboard>() : null;
            Mouse testMouse = Mouse.current == null ? InputSystem.AddDevice<Mouse>() : null;
            InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSettings.EditorInputBehaviorInPlayMode previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(Keyboard.current);
            InputSystem.EnableDevice(Mouse.current);
            yield return null;
            yield return null;
            Object.FindObjectOfType<StoryCinematic>()?.Skip();
            yield return Wait(1f, true);
            RunSession session = RunSession.Current;
            Assert.NotNull(GameplayHUD.Current);
            Assert.AreEqual(12, session.Inventory.Items.Count);
            Player player = Player.Instance;
            PlayerItemController controller = player.GetComponent<PlayerItemController>();
            EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
            spawner.spawnInterval = 100f;
            Assert.NotNull(spawner.navigation);
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas));

            GameObject pickup = Object.Instantiate(session.Catalog.startingItems[0].Prefab, player.transform.position + Vector3.forward * 1.6f, Quaternion.identity);
            player.GetComponent<Rigidbody>().position = playerHit.position;
            player.transform.position = playerHit.position;
            player.transform.forward = Vector3.forward;
            yield return Wait(0.05f);
            yield return null;
            yield return Press(Key.E);
            Assert.AreEqual(13, session.Inventory.Items.Count, "E must collect a world item into inventory; fox NPCs are not inventory items.");
            Object.Destroy(pickup);
            // 结束拾取输入测试后回到可导航的起始区域，避免传送进树木间隙干扰刷怪验证。
            player.GetComponent<Rigidbody>().position = playerHit.position;
            player.transform.position = playerHit.position;
            yield return Wait(0.4f);

            yield return Press(Key.Tab);
            Assert.IsTrue(GameplayHUD.Current.InventoryOpen);
            Assert.AreEqual(0f, Time.timeScale);
            yield return Wait(.3f, true);
            ItemInstance butterfly = session.Inventory.Items.First(item => item.definition.effect == ItemEffectKind.SpeedBoost);
            Button butterflySlot = GameplayHUD.Current.GetComponentsInChildren<Button>().First(button =>
                button.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.Contains("蝴蝶")));
            UnityEngine.EventSystems.ExecuteEvents.Execute(butterflySlot.gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                {
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
                },
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.AreSame(butterfly, session.Inventory.Equipped);
            float boostBefore = session.BoostRemaining;
            int spawnedBefore = spawner.SpawnedCount;
            yield return null;
            Assert.AreEqual(boostBefore, session.BoostRemaining);
            Assert.AreEqual(spawnedBefore, spawner.SpawnedCount);
            Directory.CreateDirectory("Docs/Images");
            CaptureFrame("Docs/Images/inventory.png");
            yield return null;
            yield return Press(Key.Tab);
            Assert.IsFalse(GameplayHUD.Current.InventoryOpen);
            yield return Wait(.3f, true);
            Assert.AreEqual(1f, Time.timeScale);
            Vector2 aim = new Vector2(Screen.width * 0.55f, Screen.height * 0.55f);
            yield return MouseClick(aim, MouseButton.Left);
            Assert.AreEqual(2, butterfly.remainingDurability);
            Assert.Greater(session.BoostRemaining, 0f);
            yield return Press(Key.R);
            Assert.IsNull(session.Inventory.Equipped);
            Assert.IsFalse(player.HasKitchenObject());
            Assert.IsTrue(controller.Equip(butterfly.id));
            yield return Wait(0.3f);
            yield return MouseClick(aim, MouseButton.Right);
            Assert.IsNull(session.Inventory.Find(butterfly.id));
            Assert.IsNull(session.Inventory.Equipped);

            ItemProjectilePool.Current.Launch(butterfly.definition, player.transform.position + Vector3.up * 8f,
                player.transform.position + Vector3.up * 8f + Vector3.forward * 12f, CombatTeam.Player, player.gameObject, false);
            ItemProjectile flying = Object.FindObjectOfType<ItemProjectile>();
            Assert.NotNull(flying);
            GameplayHUD.Current.ToggleInventory();
            Vector3 pausedPosition = flying.transform.position;
            float pausedBoost = session.BoostRemaining;
            yield return Wait(0.15f, true);
            Assert.AreEqual(pausedPosition, flying.transform.position);
            Assert.AreEqual(pausedBoost, session.BoostRemaining);
            GameplayHUD.Current.CloseInventory();
            ItemProjectilePool.Current.Release(flying, butterfly.definition);

            // 真实台面往返保留同一个实例及耐久。
            ItemInstance tomato = session.Inventory.Items.First(item => item.definition.effect == ItemEffectKind.Health);
            session.Inventory.ConsumeUse(tomato);
            controller.Equip(tomato.id);
            ClearCounter counter = Object.FindObjectsOfType<ClearCounter>().First(item => !item.HasKitchenObject());
            counter.Interact(player);
            Assert.IsNull(session.Inventory.Find(tomato.id));
            Assert.AreEqual(tomato, counter.GetKitchenObject().Instance);
            counter.Interact(player);
            Assert.AreSame(tomato, session.Inventory.Find(tomato.id));
            Assert.AreEqual(2, tomato.remainingDurability);
            controller.Equip(tomato.id);
            yield return Wait(0.3f);
            Assert.IsTrue(controller.TryUse(player.transform.position));
            Assert.AreEqual(6, session.MaxHealth);
            Assert.AreEqual(6, session.CurrentHealth);

            spawner.spawnInterval = 100f;
            Assert.IsTrue(spawner.TrySpawn(), "Spawn failed: total=" + spawner.SpawnedCount + ", alive=" + spawner.AliveCount + ", player=" + player.transform.position);
            RockEnemy bombTarget = spawner.ActiveEnemies.First();
            bombTarget.attackInterval = 100f;
            bombTarget.GetComponent<NavMeshAgent>().speed = 0f;
            Vector3 nearby = bombTarget.transform.position + Vector3.back * 2f;
            player.GetComponent<Rigidbody>().position = nearby;
            player.transform.position = nearby;
            var extraCollider = new GameObject("Explosion duplicate collider regression");
            extraCollider.transform.SetParent(bombTarget.transform, false);
            BoxCollider extra = extraCollider.AddComponent<BoxCollider>();
            extra.isTrigger = true;
            extra.center = Vector3.up;
            extra.size = new Vector3(1, 2, 1);
            ItemInstance cabbage = session.Inventory.Items.First(item => item.definition.effect == ItemEffectKind.Explosion);
            controller.Equip(cabbage.id);
            yield return Wait(0.3f);
            Assert.IsTrue(controller.TryUse(bombTarget.transform.position));
            yield return Wait(0.6f);
            Assert.AreEqual(2, bombTarget.CurrentHealth, "One explosion must damage each enemy once, even with multiple colliders.");
            Assert.AreEqual(2, cabbage.remainingDurability);
            Object.Destroy(extraCollider);
            KitchenObjectSO normalShot = session.Catalog.startingItems.First(item => item.effect == ItemEffectKind.Health);
            for (int index = 0; index < 2; index++)
            {
                // 从目标上方发射，隔离附近树干遮挡；仍使用真实 Rigidbody 碰撞结算。
                ItemProjectilePool.Current.Launch(normalShot, bombTarget.transform.position + Vector3.up * 2.5f,
                    bombTarget.transform.position + Vector3.up, CombatTeam.Player, player.gameObject, false);
                yield return Wait(0.4f);
            }
            Assert.IsFalse(bombTarget.gameObject.activeSelf, "Lethal projectile damage must return the monster to its pool.");

            // 第一只出生后杀死并重用池实例，最终只累计生成十只。
            player.GetComponent<Rigidbody>().position = playerHit.position;
            player.transform.position = playerHit.position;
            spawner.spawnInterval = 0.02f;
            int firstInstance = 0;
            var observed = new HashSet<int>();
            float deadline = Time.realtimeSinceStartup + 25f;
            while ((spawner.SpawnedCount < 10 || spawner.AliveCount > 0) && Time.realtimeSinceStartup < deadline)
            {
                foreach (RockEnemy enemy in spawner.ActiveEnemies.ToArray())
                {
                    enemy.attackInterval = 100f;
                    if (firstInstance == 0)
                        firstInstance = enemy.GetInstanceID();
                    observed.Add(enemy.GetInstanceID());
                    Assert.AreEqual(3, enemy.CurrentHealth);
                    Assert.IsTrue(enemy.TryApplyDamage(new DamageInfo(3, CombatTeam.Player, player.gameObject)));
                }
                Assert.LessOrEqual(spawner.AliveCount, 3);
                yield return null;
            }
            Assert.AreEqual(10, spawner.SpawnedCount);
            Assert.AreEqual(0, spawner.AliveCount);
            Assert.LessOrEqual(observed.Count, 3);
            Assert.AreNotEqual(0, firstInstance);
            Assert.IsFalse(spawner.TrySpawn());
            CaptureFrame("Docs/Images/gameplay-hud.png");

            session.UseShield(session.Catalog.startingItems.First(item => item.effect == ItemEffectKind.Shield));
            float savedBoost = session.BoostRemaining;
            int savedCount = session.Inventory.Items.Count;
            Assert.IsTrue(SceneLoader.Load(SceneLoader.Scene.GameScene2));
            yield return WaitForScene("GameScene2");
            Assert.AreSame(session, RunSession.Current);
            Assert.AreEqual(savedCount, session.Inventory.Items.Count);
            Assert.AreEqual(6, session.MaxHealth);
            Assert.AreEqual(1, session.ShieldCharges);
            Assert.LessOrEqual(session.BoostRemaining, savedBoost);
            Assert.Greater(session.BoostRemaining, savedBoost - 1f);
            player = Player.Instance;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            Assert.IsFalse(health.TryApplyDamage(new DamageInfo(1, CombatTeam.Player, player.gameObject)));
            health.TryApplyDamage(new DamageInfo(1, CombatTeam.Enemy, null));
            Assert.AreEqual(0, session.ShieldCharges);
            session.Tick(1f);
            Assert.IsTrue(health.TryApplyDamage(new DamageInfo(100, CombatTeam.Enemy, null)));
            Assert.IsTrue(session.IsDead);
            Assert.AreEqual(0f, Time.timeScale);
            GameplayHUD.Current.Retry();
            yield return WaitForScene("GameScene2");
            Assert.AreEqual(5, RunSession.Current.CurrentHealth);
            Assert.AreEqual(12, RunSession.Current.Inventory.Items.Count);
            Assert.AreEqual(1f, Time.timeScale);
            GameplayHUD.Current.ReturnToMenu();
            yield return WaitForScene("StartScene");
            Assert.IsNull(RunSession.Current);
            Assert.AreEqual(1f, Time.timeScale);
            if (testKeyboard != null)
                InputSystem.RemoveDevice(testKeyboard);
            if (testMouse != null)
                InputSystem.RemoveDevice(testMouse);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EnemyNavigationActiveAttackContactAndAliveLimit()
        {
            RunSession.End();
            GameplayPause.ClearScene();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            Object.FindObjectOfType<StoryCinematic>()?.Skip();
            yield return Wait(1f, true);
            Player player = Player.Instance;
            EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
            spawner.spawnInterval = 100f;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (spawner.AliveCount < 3 && Time.realtimeSinceStartup < deadline)
            {
                spawner.TrySpawn();
                yield return null;
            }
            Assert.AreEqual(3, spawner.AliveCount);
            Assert.AreEqual(3, spawner.SpawnedCount);
            Assert.IsFalse(spawner.TrySpawn(), "An alive cap must reject a fourth lease.");
            foreach (RockEnemy enemy in spawner.ActiveEnemies)
            {
                enemy.attackInterval = 100f;
                enemy.detectionRange = 0f;
            }
            RockEnemy attacker = spawner.ActiveEnemies.First();
            attacker.detectionRange = 50f;
            attacker.attackRange = 1f;
            Vector3 before = attacker.transform.position;
            NavMeshAgent agent = attacker.GetComponent<NavMeshAgent>();
            yield return Wait(0.3f);
            float distanceBefore = NavigationDistance(attacker.transform.position, player.transform.position);
            yield return Wait(2f);
            Assert.Greater(Vector3.Distance(before, attacker.transform.position), 1f, "NavMeshAgent must move along the environment path.");
            Assert.Less(NavigationDistance(attacker.transform.position, player.transform.position), distanceBefore);
            Assert.IsTrue(attacker.GetComponentInChildren<Animator>().GetBool("isWalking"));
            Assert.IsTrue(agent.isOnNavMesh);
            File.WriteAllText("Docs/navigation-runtime-result.txt", "Agent moved " + Vector3.Distance(before, attacker.transform.position) +
                " metres; path status=" + agent.pathStatus + "; path corners=" + agent.path.corners.Length + "\n");

            Vector3 attackPosition = FindClearAttackPosition(player);
            Assert.IsTrue(agent.Warp(attackPosition));
            attacker.attackRange = 10f;
            attacker.attackInterval = 0.1f;
            RunSession session = RunSession.Current;
            KitchenObjectSO cheese = session.Catalog.startingItems.First(item => item.effect == ItemEffectKind.Shield);
            session.UseShield(cheese);
            session.UseShield(cheese);
            session.UseShield(cheese);
            yield return null;
            CaptureFrame("Docs/Images/combat.png");
            deadline = Time.realtimeSinceStartup + 8f;
            while (session.ShieldCharges == 3 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Less(session.ShieldCharges, 3, "A real AI projectile must hit the player and consume a shield.");
            Assert.AreEqual(5, session.CurrentHealth);
            attacker.attackInterval = 100f;
            yield return Wait(5.2f);
            session.Tick(1f);
            int previousCharges = session.ShieldCharges;
            int previousHealth = session.CurrentHealth;
            attacker.detectionRange = 0f;
            agent.enabled = false;
            attacker.transform.position = player.transform.position + Vector3.right * 0.8f;
            Physics.SyncTransforms();
            yield return Wait(0.12f);
            Assert.IsTrue(session.ShieldCharges < previousCharges || session.CurrentHealth < previousHealth,
                "A real monster collision must apply contact damage.");
            Assert.IsTrue(SceneLoader.Load(SceneLoader.Scene.StartScene));
            yield return WaitForScene("StartScene");
            Assert.IsNull(ItemProjectilePool.Current);
            Assert.AreEqual(0, NavMesh.CalculateTriangulation().indices.Length, "Leaving the level must remove navigation instances.");
            yield return new ExitPlayMode();
        }

        private static Vector3 FindClearAttackPosition(Player player)
        {
            int excluded = LayerMask.GetMask("PlayerCombat", "Enemy", "WorldItem", "ItemProjectile");
            for (int step = 0; step < 32; step++)
            {
                float angle = step * Mathf.PI / 16f;
                Vector3 point = player.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 4f;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    continue;
                if (!Physics.Linecast(hit.position + Vector3.up * 1.2f, player.transform.position + Vector3.up,
                    ~excluded, QueryTriggerInteraction.Ignore))
                    return hit.position;
            }
            Assert.Fail("No unobstructed combat position on navigation.");
            return player.transform.position;
        }

        private static float NavigationDistance(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            float distance = 0f;
            for (int index = 1; index < path.corners.Length; index++)
                distance += Vector3.Distance(path.corners[index - 1], path.corners[index]);
            return distance;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameplayPause.ClearScene();
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }

        private static void CaptureFrame(string path)
        {
            // 批处理省略屏幕 UI，图形验收由普通编辑器 Game View 完成。
            if (Application.isBatchMode)
                return;
            Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            GameplayVisualPreview.Capture(EditorWindow.GetWindow(gameView), path);
        }

        private static IEnumerator Wait(float seconds, bool realtime = false)
        {
            float deadline = (realtime ? Time.realtimeSinceStartup : Time.time) + seconds;
            while ((realtime ? Time.realtimeSinceStartup : Time.time) < deadline)
                yield return null;
        }

        private static IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator MouseClick(Vector2 position, MouseButton button)
        {
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position }.WithButton(button));
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position });
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(SceneLoader.IsLoading);
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
            yield return null;
        }
    }
}
