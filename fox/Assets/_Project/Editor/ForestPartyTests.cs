using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FoxProject.Tests
{
    public class FoxEscortStateTests
    {
        private GameObject root, arrival;
        private FoxCompanion fox;
        [SetUp]
        public void Setup()
        {
            GameplayPause.ClearScene();
            root = new GameObject("Fox under test");
            root.AddComponent<NavMeshAgent>().enabled = false;
            fox = root.AddComponent<FoxCompanion>();
            arrival = new GameObject("Party arrival");
            arrival.transform.position = Vector3.forward * 20;
            fox.arrivalPoint = arrival.transform;
            fox.ResetToSearch(Vector3.zero);
            CombatTargets.Register(fox);
        }
        [TearDown]
        public void Cleanup()
        {
            CombatTargets.Unregister(fox);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(arrival);
            GameplayPause.ClearScene();
        }
        [Test]
        public void UnfoundAndArrivedFoxesAreProtectedAndNeverValidEnemyTargets()
        {
            Assert.IsFalse(fox.CanBeTargeted);
            Assert.IsFalse(fox.TryApplyDamage(new DamageInfo(99, CombatTeam.Enemy, null)));
            fox.BeginApology();
            fox.CompleteApology();
            root.transform.position = arrival.transform.position;
            Assert.IsTrue(fox.TryArrive());
            Assert.IsFalse(fox.CanBeTargeted);
            Assert.IsFalse(fox.TryApplyDamage(new DamageInfo(99, CombatTeam.Enemy, null)));
        }
        [Test]
        public void ApologyOnlyCompletesOnceAndSupportsWaitingThenFollowing()
        {
            Assert.IsTrue(fox.BeginApology());
            Assert.IsFalse(fox.BeginApology());
            Assert.IsFalse(fox.CanBeTargeted);
            fox.CompleteApology();
            Assert.AreEqual(FoxEscortState.Following, fox.State);
            Assert.IsTrue(fox.CanBeTargeted);
            fox.ToggleFollow();
            Assert.AreEqual(FoxEscortState.Waiting, fox.State);
            fox.ToggleFollow();
            Assert.AreEqual(FoxEscortState.Following, fox.State);
        }
        [Test]
        public void DownedFoxCannotArriveAndRescueRestoresPriorEscortState()
        {
            fox.BeginApology();
            fox.CompleteApology();
            fox.ToggleFollow();
            Assert.IsTrue(fox.TryApplyDamage(new DamageInfo(10, CombatTeam.Enemy, null)));
            Assert.AreEqual(FoxEscortState.Downed, fox.State);
            Assert.IsFalse(fox.CanBeTargeted);
            root.transform.position = arrival.transform.position;
            Assert.IsFalse(fox.TryArrive());
            Assert.IsTrue(fox.Rescue());
            Assert.AreEqual(FoxEscortState.Waiting, fox.State);
            Assert.AreEqual(5, fox.CurrentHealth);
            Assert.IsFalse(fox.TryApplyDamage(new DamageInfo(1, CombatTeam.Enemy, null)), "Rescue protection blocks immediate re-damage.");
        }
        [Test]
        public void ArrivalIsDistanceCheckedAndSubmittedOnlyOnce()
        {
            fox.BeginApology();
            fox.CompleteApology();
            Assert.IsFalse(fox.TryArrive());
            int changes = 0;
            fox.StateChanged += () => changes++;
            root.transform.position = arrival.transform.position;
            Assert.IsTrue(fox.TryArrive());
            Assert.IsFalse(fox.TryArrive());
            Assert.AreEqual(1, changes);
        }
        [Test]
        public void FriendlyAttacksAndPausedDamageCannotHurtEscort()
        {
            fox.BeginApology();
            fox.CompleteApology();
            Assert.IsFalse(fox.TryApplyDamage(new DamageInfo(2, CombatTeam.Player, null)));
            GameplayPause.Set(this, true);
            Assert.IsFalse(fox.TryApplyDamage(new DamageInfo(2, CombatTeam.Enemy, null)));
            Assert.AreEqual(5, fox.CurrentHealth);
        }
        [Test]
        public void NearestTargetDropsDownedAndArrivedCompanions()
        {
            fox.BeginApology();
            fox.CompleteApology();
            var other = new GameObject("Another escort");
            other.AddComponent<NavMeshAgent>().enabled = false;
            var second = other.AddComponent<FoxCompanion>();
            second.ResetToSearch(Vector3.forward * 3);
            CombatTargets.Register(second);
            second.BeginApology();
            second.CompleteApology();
            try
            {
                Assert.AreSame(fox, CombatTargets.Nearest(Vector3.zero, 20));
                fox.TryApplyDamage(new DamageInfo(20, CombatTeam.Enemy, null));
                Assert.AreSame(second, CombatTargets.Nearest(Vector3.zero, 20));
                second.arrivalPoint = arrival.transform;
                other.transform.position = arrival.transform.position;
                second.TryArrive();
                Assert.IsNull(CombatTargets.Nearest(Vector3.zero, 50));
            }
            finally { CombatTargets.Unregister(second); Object.DestroyImmediate(other); }
        }
    }

    public class ForestPartyIntegrationTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator TitleMainButtonMixerMinimapAndGhostAnimationWorkTogether()
        {
            RunSession.End();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/StartScene.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            float start = Time.realtimeSinceStartup;
            while (UIManager.Instance.GetPanel<StartPanel>() == null && Time.realtimeSinceStartup - start < 2)
                yield return null;
            Assert.NotNull(UIManager.Instance.GetPanel<StartPanel>());
            Assert.AreEqual(0, Object.FindObjectsOfType<UnityEngine.Video.VideoPlayer>().Length);
            var title = UIManager.Instance.GetPanel<StartPanel>();
            var button = (UnityEngine.UI.Button)typeof(StartPanel).GetField("startBtn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(title);
            button.onClick.Invoke();
            yield return WaitScene();
            Assert.AreEqual("GameScene1", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            Assert.IsTrue(StoryCinematic.IsPlaying);
            FoxEscortQuest.Current.cinematic.Skip();
            yield return Wait(1.1f);
            var map = Object.FindObjectOfType<CircularMinimap>();
            Assert.NotNull(map.MapCamera);
            Assert.IsTrue(map.MapCamera.orthographic);
            Assert.AreEqual(512, map.MapCamera.targetTexture.width);
            foreach (string name in new[] { "PlayerCombat", "Enemy", "Companion", "WorldItem", "ItemProjectile", "DynamicFX" })
                Assert.AreEqual(0, map.MapCamera.cullingMask & (1 << LayerMask.NameToLayer(name)));
            var symbols = Object.FindObjectOfType<MinimapSymbols>();
            Assert.AreEqual(Vector2.one * .5f, symbols.rectTransform.pivot);
            Assert.NotNull(symbols.PlayerMarker.sprite);
            Assert.AreEqual(4, symbols.FrustumLineCount);
            Animator animator = Player.Instance.GetComponentInChildren<Animator>();
            Assert.IsFalse(animator.applyRootMotion);
            Transform body = animator.GetComponentsInChildren<Transform>().First(node => node.name == "Body");
            Vector3 before = body.position;
            yield return Wait(.55f);
            Assert.Greater(Vector3.Distance(before, body.position), .01f, "Imported FBX bones must actually move in world units during idle.");
            float oldMusic = AudioManager.Instance.GetBgVolume(), oldEffects = AudioManager.Instance.GetBkVolume();
            try
            {
                AudioManager.Instance.SaveBgVolume(.2f);
                AudioManager.Instance.SaveBkVolume(.7f);
                var mixer = Resources.Load<PartyAudioLibrary>("ForestParty/AudioLibrary").mixer;
                Assert.IsTrue(mixer.GetFloat("MusicVolume", out float music));
                Assert.That(music, Is.EqualTo(Mathf.Log10(.2f) * 20).Within(.02f));
                Assert.IsTrue(mixer.GetFloat("EffectsVolume", out float effects));
                Assert.That(effects, Is.EqualTo(Mathf.Log10(.7f) * 20).Within(.02f));
            }
            finally { AudioManager.Instance.SaveBgVolume(oldMusic); AudioManager.Instance.SaveBkVolume(oldEffects); }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator DialogueGroupEscortRescueInterruptAndCinematicLifecycle()
        {
            RunSession.End();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            FoxEscortQuest quest = FoxEscortQuest.Current;
            StoryCinematic story = quest.cinematic;
            Object.FindObjectOfType<EnemySpawner>().spawnInterval = 1000;
            Assert.IsTrue(StoryCinematic.IsPlaying);
            Assert.IsTrue(GameplayPause.IsPaused);
            story.Skip();
            yield return Wait(1.1f);
            Assert.IsFalse(StoryCinematic.IsPlaying);
            Assert.IsFalse(GameplayPause.IsPaused);
            Assert.IsTrue(RunSession.Current.IntroSeen);
            Assert.IsTrue(quest.foxes.All(fox => fox.State == FoxEscortState.WaitingToFind));
            Keyboard added = Keyboard.current == null ? InputSystem.AddDevice<Keyboard>() : null;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(Keyboard.current);
            Player player = Player.Instance;
            foreach (FoxCompanion fox in quest.foxes)
            {
                PlacePlayer(fox.transform.position + Vector3.back * 1.4f);
                yield return null;
                Assert.IsTrue(quest.TryInteract());
                Assert.IsTrue(GameplayPause.IsPaused);
                Assert.AreEqual(FoxEscortState.Dialogue, fox.State);
                DialogueView.Current.Advance();
                DialogueView.Current.Advance();
                DialogueView.Current.Advance();
                DialogueView.Current.Advance();
                DialogueView.Current.Advance();
                DialogueView.Current.Advance();
                yield return Wait(.3f);
                Assert.AreEqual(FoxEscortState.Following, fox.State);
                Assert.IsFalse(GameplayPause.IsPaused);
                fox.ToggleFollow();
            }
            Assert.AreEqual(3, quest.BefriendedCount);
            FoxCompanion wounded = quest.foxes[0];
            PlacePlayer(wounded.transform.position + Vector3.back * 7);
            yield return null;
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            Assert.IsTrue(spawner.TrySpawn());
            RockEnemy attacker = spawner.ActiveEnemies.First();
            attacker.attackInterval = 100;
            attacker.detectionRange = 100;
            attacker.contactDamage = 0;
            Assert.IsTrue(NavMesh.SamplePosition(wounded.transform.position + Vector3.right * 1.5f, out NavMeshHit attackPoint, 3, NavMesh.AllAreas));
            attacker.GetComponent<NavMeshAgent>().Warp(attackPoint.position);
            yield return Wait(.1f);
            Assert.AreSame(wounded, attacker.CurrentTarget, "An escort closer than the player must become the real AI target.");
            wounded.TryApplyDamage(new DamageInfo(99, CombatTeam.Enemy, null));
            Assert.AreEqual(FoxEscortState.Downed, wounded.State);
            yield return Wait(.1f);
            Assert.AreNotSame(wounded, attacker.CurrentTarget, "AI must drop a downed target.");
            attacker.TryApplyDamage(new DamageInfo(99, CombatTeam.Player, player.gameObject));
            PlacePlayer(wounded.transform.position + Vector3.back);
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
            InputSystem.Update();
            yield return Wait(.8f);
            Assert.Greater(quest.RescueProgress, .1f);
            PlacePlayer(wounded.transform.position + Vector3.back * 5);
            yield return Wait(.1f);
            Assert.AreEqual(0, quest.RescueProgress);
            PlacePlayer(wounded.transform.position + Vector3.back);
            yield return Wait(.8f);
            player.GetComponent<PlayerHealth>().TryApplyDamage(new DamageInfo(1, CombatTeam.Enemy, null));
            Assert.AreEqual(0, quest.RescueProgress);
            yield return Wait(2.2f);
            Assert.AreEqual(5, wounded.CurrentHealth);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();
            foreach (FoxCompanion fox in quest.foxes)
            {
                if (fox.State == FoxEscortState.Waiting)
                    fox.ToggleFollow();
                fox.GetComponent<NavMeshAgent>().Warp(fox.arrivalPoint.position);
                Assert.IsTrue(fox.TryArrive());
                Assert.IsFalse(fox.TryArrive());
            }
            Assert.AreEqual(3, quest.ArrivedCount);
            Assert.IsTrue(quest.IsComplete);
            Assert.IsTrue(StoryCinematic.IsPlaying);
            var bagOwner = new object();
            GameplayPause.Set(bagOwner, true);
            story.reunion.time = 12;
            story.reunion.Evaluate();
            story.Skip();
            yield return Wait(1.1f);
            Assert.IsFalse(StoryCinematic.IsPlaying);
            Assert.IsTrue(GameplayPause.IsPaused, "Finishing a cinematic must not release another pause owner.");
            GameplayPause.Set(bagOwner, false);
            UIManager.Instance.HidePanel<LevelSelectPanel>(false);
            yield return null;
            Assert.IsFalse(GameplayPause.IsPaused);
            Assert.IsTrue(SceneLoader.Load(SceneLoader.Scene.GameScene2));
            yield return WaitScene();
            Assert.IsNull(FoxEscortQuest.Current);
            Assert.IsFalse(StoryCinematic.IsPlaying);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsNull(Object.FindObjectOfType<EnemySpawner>());
            if (added != null)
                InputSystem.RemoveDevice(added);
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator NaturalIntroEndAndSceneExitMidReunionRestoreState()
        {
            RunSession.End();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            var story = FoxEscortQuest.Current.cinematic;
            story.intro.time = 39.8;
            story.intro.Evaluate();
            yield return Wait(1.5f);
            Assert.IsFalse(StoryCinematic.IsPlaying);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsTrue(RunSession.Current.IntroSeen);
            story.PlayReunion();
            yield return null;
            Assert.IsTrue(StoryCinematic.IsPlaying);
            Assert.IsTrue(SceneLoader.Load(SceneLoader.Scene.StartScene));
            yield return WaitScene();
            Assert.IsFalse(StoryCinematic.IsPlaying);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsNull(RunSession.Current);
            Assert.IsNull(UIManager.Instance.GetPanel<LevelSelectPanel>());
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator AllThreeEscortsUseNavigationAndWaitWhenPlayerRunsTooFar()
        {
            RunSession.End();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            var quest = FoxEscortQuest.Current;
            quest.cinematic.Skip();
            yield return Wait(1.1f);
            Object.FindObjectOfType<EnemySpawner>().spawnInterval = 1000;
            Vector3 basePoint = quest.foxes[1].transform.position;
            Vector3[] starts = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                var fox = quest.foxes[i];
                Assert.IsTrue(NavMesh.SamplePosition(basePoint + Vector3.right * (i - 1) * 1.2f, out NavMeshHit hit, 3, NavMesh.AllAreas));
                fox.GetComponent<NavMeshAgent>().Warp(hit.position);
                starts[i] = hit.position;
                fox.BeginApology();
                fox.CompleteApology();
            }
            Assert.IsTrue(NavMesh.SamplePosition(basePoint + Vector3.back * 6, out NavMeshHit destination, 3, NavMesh.AllAreas));
            PlacePlayer(destination.position);
            yield return Wait(2);
            for (int i = 0; i < 3; i++)
                Assert.Greater(Vector3.Distance(starts[i], quest.foxes[i].transform.position), .3f, "Each escort must actually walk its navigation route.");
            PlacePlayer(basePoint + Vector3.back * 30);
            yield return Wait(.2f);
            Assert.IsTrue(quest.foxes.All(fox => fox.State == FoxEscortState.Waiting));
            foreach (var fox in quest.foxes)
                Assert.Greater(Vector3.Distance(fox.transform.position, Player.Instance.transform.position), 12, "Waiting foxes must not teleport to catch up.");
            yield return new ExitPlayMode();
        }
        private static void PlacePlayer(Vector3 point)
        {
            Player.Instance.transform.position = point;
            Player.Instance.GetComponent<Rigidbody>().position = point;
            Physics.SyncTransforms();
        }
        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }
        private static IEnumerator WaitScene()
        {
            float end = Time.realtimeSinceStartup + 60;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.IsFalse(SceneLoader.IsLoading);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameplayPause.ClearScene();
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }
    }
}
