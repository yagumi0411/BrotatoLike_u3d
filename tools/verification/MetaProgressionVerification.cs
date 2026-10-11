using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Copy to the isolated verification project's Assets/Editor before running.
[InitializeOnLoad]
public static class MetaProgressionVerification
{
    private const string Key = "Codex.MetaProgressionVerification";
    private const string Root = "E:/u3dProject/BrotatoLike_u3d/";
    private static readonly List<string> Results = new List<string>();
    private static int _stage;
    private static double _next;
    private static double _started = EditorApplication.timeSinceStartup;
    private static GameManager Manager => GameManager.Instance;
    private static string Id(string suffix) => "core.mage." + suffix;

    static MetaProgressionVerification()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.dataPath.Contains("GameLoopVerificationProject"))
            throw new Exception("Run only in the isolated project with a separate company/product save identity.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Results.Add("PASS " + name);
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, values);
    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    private static void ClearKnownTalents(MetaProgression profile) => profile.ResetTalents();

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - _started > 120) { Finish(new Exception("Timeout")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _next) return;
        try
        {
            if (Manager == null) return;
            switch (_stage)
            {
                case 0:
                    _next = EditorApplication.timeSinceStartup + 0.8; _stage++; break;
                case 1:
                    VerifyProfile();
                    VerifyRunSettlement();
                    VerifyUIAndStats();
                    _next = EditorApplication.timeSinceStartup + 0.7; _stage++; break;
                case 2:
                    CaptureTalentGraph();
                    _next = EditorApplication.timeSinceStartup + 0.5; _stage++; break;
                default: Finish(null); break;
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static TalentGraphDefinition Definition() => JsonUtility.FromJson<TalentGraphDefinition>(Resources.Load<TextAsset>("Progression/MageTalentGraph").text);

    private static void VerifyProfile()
    {
        string path = Path.Combine(Root, "Temp", "TalentProfile_" + Guid.NewGuid().ToString("N") + ".json");
        var profile = new MetaProgression(Definition(), path);
        Check(profile.Graph.Nodes.Count == 19 && profile.Graph.TotalRanks == 33 && profile.AvailablePoints == 0, "Mage region contains 18 allocatable nodes and 33 ranks without free points");
        Check(!profile.TryAllocate(Id("damage.notable")), "Remote talent cannot be allocated before connecting a path");
        var invalid = profile.SettleRun("empty", 0, 0, 0);
        Check(invalid.XP == 0 && profile.TotalXP == 0, "Immediate exit gives no reward");
        var first = profile.SettleRun("first", 1, 0, 0);
        Check(first.XP == 10 && first.Points == 1 && profile.AvailablePoints == 1, "First valid short run earns an immediately usable point");
        Check(profile.SettleRun("first", 999, 999, 99) == null && profile.TotalXP == 10, "Same run cannot settle twice");
        var reloaded = new MetaProgression(Definition(), path);
        Check(reloaded.SettleRun("first", 999, 999, 99) == null && reloaded.TotalXP == 10, "Run settlement guard survives save reload");
        Check(profile.TryAllocate(Id("damage.entry")) && profile.IsConnected(Id("damage.upper")), "Rank one opens adjacent nodes before entry is maxed");
        var loadedRank = new MetaProgression(Definition(), path);
        Check(loadedRank.GetRank(Id("damage.entry")) == 1 && loadedRank.AvailablePoints == 0, "Allocation and spent points survive save reload");
        profile.SettleRun("large", 100000, 600, 20);
        long afterLarge = profile.TotalXP;
        Check(profile.SettleRun("first", 999, 999, 99) == null && profile.TotalXP == afterLarge, "Older settled run cannot be replayed after later runs");
        var historical = new MetaProgression(Definition(), path);
        Check(historical.SettleRun("first", 999, 999, 99) == null && historical.TotalXP == afterLarge, "Historical settlement guard persists across reload");
        Check(profile.EarnedPoints == 33 && profile.UnspentXP > 0, "Every rank can be earned and surplus XP remains for future regions");
        Check(profile.TryAllocate(Id("damage.upper")), "Connected branch can be allocated");
        Check(profile.TryAllocate(Id("bridge.damage_range")) && profile.TryAllocate(Id("range.left")), "Cross-route link opens a neighboring branch");
        Check(!profile.TryAllocate(Id("survival.notable")), "Disconnected notable remains locked despite abundant points");
        bool changed;
        do
        {
            changed = false;
            foreach (var node in profile.Graph.Nodes)
                while (profile.TryAllocate(node.Id)) changed = true;
        } while (changed);
        Check(profile.SpentPoints == 33 && profile.AvailablePoints == 0 && profile.Graph.Nodes.All(n => n.Id == profile.Graph.StartNodeId || profile.GetRank(n.Id) == n.MaxRank), "Entire region can be maxed without mutually exclusive branches");
        var full = profile.GetBonuses();
        Check(Mathf.Approximately(full.Damage, 0.31f) && Mathf.Approximately(full.WeaponRange, 0.29f) && Mathf.Approximately(full.MaxHP, 11) && Mathf.Approximately(full.DamageReduction, 0.055f), "Full region combines damage range and survival exactly once");
        Check(!profile.TryAllocate(Id("damage.entry")), "Max rank cannot be overspent");
        long total = profile.TotalXP;
        Check(profile.ResetTalents() && profile.AvailablePoints == 33 && profile.TotalXP == total && profile.GetBonuses().Damage == 0, "Free reset refunds all points without erasing experience");
        var resetReload = new MetaProgression(Definition(), path);
        Check(resetReload.SpentPoints == 0 && resetReload.EarnedPoints == 33 && resetReload.TotalXP == total, "Free reset persists");

        var extension = new TalentGraphDefinition { Version = 1, Nodes = new[] {
            new TalentNodeDefinition { Id = "example.expansion.mage", Name = "Extension", MaxRank = 2, Position = new Vector2(400, 0), Effects = new[] { new TalentEffectDefinition { Id = "core.damage", Value = 0.01f } } }
        }, Links = new[] { new TalentLinkDefinition { From = Id("damage.notable"), To = "example.expansion.mage" } } };
        Check(profile.TryRegisterContent(extension, out _) && profile.Graph.TotalRanks == 35 && profile.EarnedPoints == 35, "Registered content expands point capacity and converts retained XP");
        Check(!profile.TryRegisterContent(extension, out _) && profile.Graph.Nodes.Count == 20, "Duplicate content is rejected without partially mutating graph");
        Check(profile.TryAllocate(Id("damage.entry")) && profile.TryAllocate(Id("damage.upper")) && profile.TryAllocate(Id("damage.notable")) && profile.TryAllocate("example.expansion.mage"), "Extension node uses the same connectivity and allocation rules");
        var withoutMod = new MetaProgression(Definition(), path);
        Check(withoutMod.GetRank("example.expansion.mage") == 1 && withoutMod.SpentPoints == 4 && Mathf.Approximately(withoutMod.GetBonuses().Damage, 0.12f), "Missing content retains reserved ID and points without applying unknown effects");
        Check(withoutMod.TryRegisterContent(extension, out _) && Mathf.Approximately(withoutMod.GetBonuses().Damage, 0.13f), "Returning content restores its saved effect");
        var badContent = new TalentGraphDefinition { Version = 1, Nodes = new[] { new TalentNodeDefinition { Id = "bad.unreachable", MaxRank = 1 } }, Links = new TalentLinkDefinition[0] };
        int countBefore = profile.Graph.Nodes.Count;
        Check(!profile.TryRegisterContent(badContent, out _) && profile.Graph.Nodes.Count == countBefore, "Unreachable content is rejected transactionally");
        var unknown = new TalentGraphDefinition { Version = 1, Nodes = new[] { new TalentNodeDefinition { Id = "bad.effect", MaxRank = 1, Effects = new[] { new TalentEffectDefinition { Id = "unknown.handler", Value = 1 } } } }, Links = new TalentLinkDefinition[0] };
        Check(!profile.TryRegisterContent(unknown, out _), "Unregistered effect ID is rejected");
        string futurePath = path + ".future";
        const string future = "{\"Version\":99,\"TotalXP\":777}";
        File.WriteAllText(futurePath, future);
        var readOnly = new MetaProgression(Definition(), futurePath);
        Check(readOnly.IsReadOnly && !readOnly.Persist() && File.ReadAllText(futurePath) == future, "Unsupported save version is preserved and never overwritten");
        Check(File.Exists(path + ".bak"), "Atomic save keeps a previous-file backup");
    }

    private static void VerifyRunSettlement()
    {
        var manager = Manager;
        Check(Application.companyName == "CodexVerification" && manager.Progression.SavePath.Contains("MageTalents_"), "Verification uses an isolated profile, not the user's progress");
        manager.ReturnToMainMenu();
        ClearKnownTalents(manager.Progression);
        long before = manager.Progression.TotalXP;
        Click(manager.MainMenuUI.StartButton);
        manager.TotalKills = 5; manager.GamePlayTime = 45;
        manager.WaveManager.EndWave();
        manager.ReturnToMainMenu();
        Check(manager.Progression.TotalXP == before + 17 && manager.Progression.LastReward.XP == 17, "Return from shop settles kills combat time and completed wave before resetting run");
        manager.ReturnToMainMenu();
        Check(manager.Progression.TotalXP == before + 17, "Repeated return to menu cannot settle again");
        Click(manager.MainMenuUI.StartButton);
        manager.ReturnToMainMenu();
        Check(manager.Progression.TotalXP == before + 17 && manager.Progression.LastReward.XP == 0, "Starting then immediately returning cannot farm guaranteed rewards");
        Click(manager.MainMenuUI.StartButton);
        manager.TotalKills = 1;
        manager.Player.StatsComponent.TakeDamage(999);
        Check(manager.CurrentState == GameManager.EGameState.GameOver && manager.Progression.TotalXP == before + 27 && manager.GameOverUI.TimeResultText.text.Contains("天赋经验 +10"), "Death saves reward and presents it in game-over results");
        manager.RestartGame();
        Check(manager.Progression.TotalXP == before + 27, "Restart after death does not duplicate death reward");
        manager.TotalKills = 5; manager.GamePlayTime = 15;
        manager.PauseGame();
        manager.RestartGame();
        Check(manager.Progression.TotalXP == before + 39 && manager.CurrentState == GameManager.EGameState.Playing && manager.TotalKills == 0, "Restart from pause settles current run and begins a fresh one");
        manager.ReturnToMainMenu();
        Click(manager.MainMenuUI.StartButton);
        manager.TotalKills = 0; manager.GamePlayTime = 15;
        manager.ReturnToMainMenu();
        Check(manager.Progression.LastReward.XP == 11, "Combat-time threshold grants progress even without a kill");
        var loaded = new MetaProgression(Definition(), manager.Progression.SavePath);
        Check(loaded.TotalXP == manager.Progression.TotalXP && loaded.EarnedPoints == manager.Progression.EarnedPoints, "Run rewards are immediately recoverable from disk");
    }

    private static void VerifyUIAndStats()
    {
        var manager = Manager;
        var profile = manager.Progression;
        profile.SettleRun(Guid.NewGuid().ToString("N"), 200, 60, 2);
        ClearKnownTalents(profile);
        var entry = manager.MainMenuUI.transform.Find("OpenTalents").GetComponent<Button>();
        Click(entry);
        var root = (GameObject)Field(manager.TalentUI, "_root");
        Check(root.activeSelf && Time.timeScale == 0 && manager.CurrentState == GameManager.EGameState.MainMenu, "Main-menu button opens talent graph while gameplay stays paused");
        var content = (RectTransform)Field(manager.TalentUI, "_content");
        var pointer = new PointerEventData(EventSystem.current);
        var remote = content.Find(Id("damage.notable")).gameObject;
        ExecuteEvents.Execute(remote, pointer, ExecuteEvents.pointerEnterHandler);
        var details = (TextMeshProUGUI)Field(manager.TalentUI, "_detail");
        Check(details.text.Contains("奥术掌握") && details.text.Contains("需要先点亮相邻节点"), "Hover previews node effects and connection requirement");
        ExecuteEvents.Execute(remote, pointer, ExecuteEvents.pointerExitHandler);
        Click(content.Find(Id("damage.entry")).GetComponent<Button>());
        Click((Button)Field(manager.TalentUI, "_allocate"));
        Check(profile.GetRank(Id("damage.entry")) == 1 && profile.GetRank(Id("damage.upper")) == 0, "UI allocates the selected node for one point");
        Check(profile.CanAllocate(Id("damage.upper")), "Adjacent node opens after one rank without needing a full entry");
        var viewport = (TalentGraphViewport)Field(manager.TalentUI, "_viewport");
        Vector2 oldPosition = content.anchoredPosition;
        pointer.position = new Vector2(200, 200); viewport.OnBeginDrag(pointer);
        pointer.position = new Vector2(300, 230); viewport.OnDrag(pointer);
        Check(content.anchoredPosition != oldPosition, "Dragging pans graph");
        pointer.scrollDelta = Vector2.up; viewport.OnScroll(pointer);
        Check(content.localScale.x > 0.9f, "Mouse wheel zooms graph");
        viewport.ResetView(new Vector2(0, -15));
        Check(Mathf.Approximately(content.localScale.x, 0.9f) && content.anchoredPosition == new Vector2(0, -15), "View reset restores starting framing");
        profile.TryAllocate(Id("range.entry")); profile.TryAllocate(Id("survival.entry"));
        manager.TalentUI.SetVisible(false);
        Click(manager.MainMenuUI.StartButton);
        var stats = manager.Player.StatsComponent;
        Check(Mathf.Approximately(stats.GetEffectiveMaxHP(), 15) && Mathf.Approximately(stats.CurrentHP, 15) && Mathf.Approximately(stats.GetEffectiveDamageMultiplier(), 1.03f) && Mathf.Approximately(stats.GetEffectiveWeaponRangeMultiplier(), 1.03f), "Next run receives allocated HP damage and range bonuses");
        stats.ApplyUpgrade(AssetDatabase.LoadAssetAtPath<UpgradeOption>("Assets/Data/Upgrades/Upgrade_Damage_All_Mul.asset"));
        Check(Mathf.Approximately(stats.GetEffectiveDamageMultiplier(), 1.13f), "Run upgrades stack with persistent damage without overwriting it");
        var weapon = manager.Player.EquippedWeapons[0];
        float effectiveRange = (float)typeof(Weapon).GetMethod("GetEffectiveRange", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(weapon, null);
        Check(Mathf.Approximately(effectiveRange, weapon.WeaponDef.Range * 1.03f), "Weapon target search uses actual talent range");
        var slime = manager.MonsterSpawner.EnemyPrefabs.Find(e => e.Type == EEnemyType.Slime);
        var target = new GameObject("TalentRangeProbe").AddComponent<SlimeEnemy>();
        target.transform.position = manager.Player.transform.position + Vector3.right * 10; target.Initialize(slime, 1);
        Call(weapon, "Fire", target);
        var bullet = Object.FindObjectsByType<MagicBulletProjectile>().First();
        Check(Mathf.Approximately(bullet.Lifetime, weapon.WeaponDef.ProjectileLifetime * 1.03f), "Magic bullet lifetime expands with target range");
        manager.TalentUI.SetVisible(true);
        Check(!root.activeSelf, "Talent editing UI cannot open during a run");
        manager.ReturnToMainMenu();
        profile.ResetTalents();
        profile.TryAllocate(Id("survival.entry")); profile.TryAllocate(Id("survival.lower"));
        Click(manager.MainMenuUI.StartButton);
        float hpBefore = stats.CurrentHP;
        stats.TakeDamage(10);
        Check(Mathf.Approximately(hpBefore - stats.CurrentHP, 9.85f), "Survival reduction affects incoming damage");
        manager.ReturnToMainMenu();
        profile.ResetTalents();
        manager.TalentUI.SetVisible(true);
        Check(((TextMeshProUGUI)Field(manager.TalentUI, "_bonuses")).text.Contains("生命 +0"), "Free reset updates summary and returns all bonuses to zero");
        manager.TalentUI.SetVisible(false);
        Click(manager.MainMenuUI.StartButton);
        Check(Mathf.Approximately(stats.GetEffectiveMaxHP(), 14) && Mathf.Approximately(stats.GetEffectiveDamageMultiplier(), 1) && Mathf.Approximately(stats.GetEffectiveWeaponRangeMultiplier(), 1), "Fresh run after reset has no residual talent bonuses");
        manager.ReturnToMainMenu();
        profile.TryAllocate(Id("damage.entry")); profile.TryAllocate(Id("damage.upper")); profile.TryAllocate(Id("range.entry")); profile.TryAllocate(Id("survival.entry"));
        manager.TalentUI.SetVisible(true);
        ExecuteEvents.Execute(content.Find(Id("damage.notable")).gameObject, pointer, ExecuteEvents.pointerEnterHandler);
    }

    private static void CaptureTalentGraph()
    {
        var camera = Camera.main;
        var canvas = ((GameObject)Field(Manager.TalentUI, "_root")).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previous = RenderTexture.active; camera.targetTexture = target;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes(Root + "Screenshots/mage_talent_graph_preview.png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(image);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Check(File.GetLastWriteTimeUtc(Root + "Screenshots/mage_talent_graph_preview.png") > DateTime.UtcNow.AddSeconds(-5), "Talent preview renders the actual game UI");
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        if (error != null) Results.Add("FAIL " + error);
        Directory.CreateDirectory(Root + "Logs"); File.WriteAllLines(Root + "Logs/MetaProgressionVerification.txt", Results);
        Debug.Log(string.Join("\n", Results)); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
