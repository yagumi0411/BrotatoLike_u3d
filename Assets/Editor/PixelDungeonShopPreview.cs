using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Preview the real shop and validate its existing actions without changing scene assets.
public static class PixelDungeonShopPreview
{
    private const string Pending = "PixelDungeonShopPreview.Pending";
    private static int _frames;
    private static int _stage;
    private static bool _batch;
    private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Outputs/PixelUI"));

    [MenuItem("Tools/Pixel Dungeon UI/Preview Shop")]
    public static void Preview()
    {
        if (EditorApplication.isPlaying) { ShowShop(); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetBool(Pending, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        Preview();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        _batch = Application.isBatchMode;
        _frames = 0;
        _stage = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void ShowShop()
    {
        var manager = GameManager.Instance;
        manager.StartGame();
        manager.OpenWaveShop();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || GameManager.Instance == null || ++_frames < 60) return;
        try
        {
            var manager = GameManager.Instance;
            if (_stage == 0)
            {
                SessionState.SetBool(Pending, false);
                ShowShop();
                if (!_batch) { EditorApplication.update -= Tick; return; }
                Require(!FindButton("PurchaseHealing").interactable, "Full-health item must be disabled");
                manager.ContinueFromShop();
                manager.Player.StatsComponent.TakeDamage(3f);
                manager.OpenWaveShop();
                Require(!FindButton("PurchaseHealing").interactable, "Unaffordable item must be disabled");
                manager.AddGold(manager.HealingCost + 5);
                manager.ShopUI.SetVisible(true);
                Require(FindButton("PurchaseHealing").interactable, "Healing item must be purchasable");
                _stage = 1;
            }
            else if (_stage == 1)
            {
                Capture("shop.png");
                _stage = 2;
            }
            else if (_stage == 2)
            {
                ExecuteEvents.Execute(FindButton("PurchaseHealing").gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                _stage = 3;
            }
            else if (_stage == 3)
            {
                Capture("tooltip.png");
                _stage = 4;
            }
            else if (_stage == 4)
            {
                int gold = manager.Gold;
                float hp = manager.Player.StatsComponent.CurrentHP;
                FindButton("PurchaseHealing").onClick.Invoke();
                Require(manager.HealingPurchased && manager.Gold == gold - manager.HealingCost,
                    "Purchase must deduct gold once");
                Require(manager.Player.StatsComponent.CurrentHP > hp && !FindButton("PurchaseHealing").interactable,
                    "Purchase must heal and disable item");
                Require(!manager.BuyHealing(), "Second purchase must be rejected");
                _stage = 5;
            }
            else if (_stage == 5)
            {
                Capture("sold.png");
                _stage = 6;
            }
            else
            {
                FindButton("ContinueWave").onClick.Invoke();
                Require(manager.CurrentState == GameManager.EGameState.Playing, "Continue must resume playing");
                Debug.Log("[PixelDungeonUI] PASS: full health, insufficient gold, purchase, tooltip, sold state, repeat purchase, continue.");
                File.WriteAllText(Path.Combine(Output, "result.txt"), "PASS");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            _frames = 0;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (_batch) EditorApplication.Exit(1);
        }
    }

    private static Button FindButton(string name)
    {
        foreach (var button in UnityEngine.Object.FindObjectsByType<Button>())
            if (button.name == name) return button;
        throw new InvalidOperationException("Missing button: " + name);
    }

    private static void Capture(string name)
    {
        var camera = Camera.main;
        var canvas = GameObject.Find("WaveShopCanvas").GetComponent<Canvas>();
        var mode = canvas.renderMode;
        var worldCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 1f;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Output, name), texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = distance;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
