using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GorillaLocomotion;

// One main menu, worn on your LEFT HAND and turned to face you (ModMenuV2 look: purple rounded panel, Daydream font).
// Left X opens/closes it. A small purple ball floats in front of your RIGHT hand: touch a button with the ball
// and pull the right trigger to select / deselect it. Desktop: Tab opens it, aim + left click.
// Home lists the sections; every section has "< Prev / Next >" when it has more than one page.
// Nothing is locked anymore - Mods is open to everybody.
public class MainMenu : MonoBehaviour
{
    class Lbl
    {
        public Text legacy;
        public TMP_Text tmp;
        public string text { set { if (tmp != null) tmp.text = value; else legacy.text = value; } }
        public Color color { set { if (tmp != null) tmp.color = value; else legacy.color = value; } }
    }

    class Btn
    {
        public GameObject go;
        public Image border, bg;
        public Lbl label;
        public Func<string> text;
        public Func<bool> isOn;
        public Action click;
    }

    class Item
    {
        public Func<string> text;
        public Func<bool> isOn;
        public Action click;
    }

    class Cat
    {
        public string title;
        public readonly List<Item> items = new List<Item>();
    }

    const int PerPage = 7;

    GtagBootstrap boot;
    XRRigInput rig;
    Player player;
    Rigidbody body;

    GameObject panel;
    Lbl titleText, statsText, footerText;
    readonly Dictionary<string, Cat> cats = new Dictionary<string, Cat>();
    readonly Dictionary<Collider, Btn> byCollider = new Dictionary<Collider, Btn>();
    readonly Btn[] slots = new Btn[PerPage];
    Btn navBack, navPrev, navNext;
    Cat current;
    string currentId = "home";
    int pageIdx;
    Btn hover;
    bool open;

    // pointer ball
    Transform ball;
    const float BallRadius = 0.018f;

    // fps
    float fpsTimer, fpsFrames, fps;

    // sprite / font
    static Sprite roundSprite;
    static TMP_FontAsset daydream;
    static bool fontTried;

    // moderator (kept for the host-side commands; not shown on the menu)
    int targetIdx;

    // mod state
    int speedIdx, gravIdx, armIdx;
    bool fly, noclip, platforms, flashlight, darkness, freeze, god, rainbow, ghost;
    bool hasSaved;
    Vector3 savedRig;
    readonly GameObject[] plat = new GameObject[2];
    Light torch;
    float rainbowNext;

    static readonly string[] SpeedNames = { "Normal", "Fast", "Super" };
    static readonly float[] JumpMult = { 1.1f, 1.3f, 1.5f };
    static readonly float[] MaxJump = { 6.5f, 9f, 13f };
    static readonly float[] GravScale = { 1f, 0.5f, 0.25f };
    static readonly float[] ArmScales = { 1f, 1.5f, 2f, 3f };
    const float BaseArmLength = 1.5f;
    const float FlySpeed = 6f;
    const float PanelScale = 0.0006f;   // 1 canvas px = 0.6 mm -> the panel is about 22 x 31 cm, like a watch screen

    // ModMenuV2 colors
    Color cPanel = new Color(0.10f, 0.04f, 0.17f, 0.97f);
    Color cEdge = new Color(0.55f, 0.22f, 0.92f, 1f);
    Color cNormal = new Color(0.17f, 0.08f, 0.29f, 1f);
    Color cOn = new Color(0.36f, 0.14f, 0.62f, 1f);
    Color cHover = new Color(0.52f, 0.28f, 0.85f, 1f);
    Color cText = new Color(0.25f, 0.78f, 0.95f, 1f);
    Color cTextOn = new Color(0.75f, 1f, 0.85f, 1f);

    void Start()
    {
        boot = GtagBootstrap.Instance;
        rig = boot.Rig;
        player = boot.Player;
        body = boot.Body;

        BuildPanel();
        BuildBall();
        BuildTorch();
        PlayFabLite.Create();
        ShowCat("home");
        panel.SetActive(false);
    }

    // ---------------- pages ----------------

    void BuildPanel()
    {
        panel = new GameObject("MainMenu");
        panel.layer = GtagBootstrap.MenuLayer;
        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 520f);
        rt.localScale = Vector3.one * PanelScale;

        Image edge = MakeRect("Edge", panel.transform, Vector2.zero, new Vector2(360f, 520f)).gameObject.AddComponent<Image>();
        Rounded(edge, cEdge);
        Image fill = MakeRect("Fill", panel.transform, Vector2.zero, new Vector2(346f, 506f)).gameObject.AddComponent<Image>();
        Rounded(fill, cPanel);

        titleText = MakeLabel("Title", panel.transform, new Vector2(0f, 224f), new Vector2(330f, 38f), 28, cText);
        statsText = MakeLabel("Stats", panel.transform, new Vector2(0f, 196f), new Vector2(330f, 22f), 15, Color.white);
        footerText = MakeLabel("Footer", panel.transform, new Vector2(0f, -240f), new Vector2(330f, 22f), 12, new Color(0.7f, 0.65f, 0.9f));

        // top row: Back / Prev / Next
        navBack = NewButton(new Vector2(-112f, 156f), new Vector2(104f, 38f), 18);
        navPrev = NewButton(new Vector2(0f, 156f), new Vector2(104f, 38f), 18);
        navNext = NewButton(new Vector2(112f, 156f), new Vector2(104f, 38f), 18);
        navBack.text = () => currentId == "home" ? "Close" : "Back";
        navBack.click = () => { if (currentId == "home") SetOpen(false); else ShowCat("home"); };
        navPrev.text = () => "< Prev";
        navPrev.click = () => { int n = PageCount(); if (n > 1) pageIdx = (pageIdx + n - 1) % n; };
        navNext.text = () => "Next >";
        navNext.click = () => { int n = PageCount(); if (n > 1) pageIdx = (pageIdx + 1) % n; };

        // item slots
        for (int i = 0; i < PerPage; i++)
            slots[i] = NewButton(new Vector2(0f, 108f - i * 50f), new Vector2(320f, 42f), 22);

        // ---- Home ----
        Cat home = NewCat("home", "Menu");
        AddItem(home, () => "Mods", null, () => ShowCat("mods"));
        AddItem(home, () => "Player", null, () => ShowCat("player"));
        AddItem(home, () => "Settings", null, () => ShowCat("settings"));
        AddItem(home, () => "PlayFab", null, () => ShowCat("playfab"));
        AddItem(home, () => "Go to Stump", null, () => boot.TeleportToSpawn());

        // ---- Mods (page 1: the ModMenuV2 set, then your own mods) ----
        Cat mods = NewCat("mods", "Mods");
        AddItem(mods, () => "Fly " + (fly ? "ON" : "OFF"), () => fly, () => { fly = !fly; });
        AddItem(mods, () => "Noclip " + (noclip ? "ON" : "OFF"), () => noclip, () => SetNoclip(!noclip));
        AddItem(mods, () => "Platforms " + (platforms ? "ON" : "OFF"), () => platforms, () => { platforms = !platforms; if (!platforms) ClearPlatforms(); });
        AddItem(mods, () => "Rainbow " + (rainbow ? "ON" : "OFF"), () => rainbow, () => SetRainbow(!rainbow));
        AddItem(mods, () => "Ghost Monke " + (ghost ? "ON" : "OFF"), () => ghost, () => SetGhost(!ghost));
        AddItem(mods, () => "Long Arms " + ArmScales[armIdx] + "x", () => armIdx > 0, () => { armIdx = (armIdx + 1) % ArmScales.Length; ApplyArms(); });
        AddItem(mods, () => "Speed " + SpeedNames[speedIdx], () => speedIdx > 0, () => { speedIdx = (speedIdx + 1) % 3; ApplySpeed(); });
        // page 2
        AddItem(mods, () => "Gravity " + GravScale[gravIdx] + "x", () => gravIdx > 0, () => { gravIdx = (gravIdx + 1) % 3; ApplyGravity(); });
        AddItem(mods, () => "Invincible " + (god ? "ON" : "OFF"), () => god, () => { god = !god; boot.Invincible = god; });
        AddItem(mods, () => "Freeze Monster " + (freeze ? "ON" : "OFF"), () => freeze, () => { freeze = !freeze; ApplyFreeze(); });
        AddItem(mods, () => "Flashlight " + (flashlight ? "ON" : "OFF"), () => flashlight, () => { flashlight = !flashlight; torch.enabled = flashlight; });
        AddItem(mods, () => "Darkness " + (darkness ? "ON" : "OFF"), () => darkness, () => SetDarkness(!darkness));
        AddItem(mods, () => "Random Spot", null, () => boot.TeleportToGround(boot.FindForestFloor()));
        AddItem(mods, () => hasSaved ? "Save Pos (set)" : "Save Pos", null, () => { savedRig = player.transform.position; hasSaved = true; });
        // page 3
        AddItem(mods, () => "Load Pos", null, () => { if (hasSaved) boot.MoveRig(savedRig); });
        AddItem(mods, () => "Reset Mods", null, ResetMods);

        // ---- Settings ----
        Cat set = NewCat("settings", "Settings");
        AddItem(set, () => "Turn " + PlayerProfile.TurnNames[PlayerProfile.TurnIdx], null, () => PlayerProfile.TurnIdx = PlayerProfile.TurnIdx + 1);
        AddItem(set, () => "Monster " + (PlayerProfile.MonsterOn ? "ON" : "OFF"), () => PlayerProfile.MonsterOn, ToggleMonster);
        AddItem(set, () => "Monster " + PlayerProfile.MonsterNames[PlayerProfile.MonsterSpeedIdx], null, () => PlayerProfile.MonsterSpeedIdx = PlayerProfile.MonsterSpeedIdx + 1);
        AddItem(set, () => "Mirror " + (PlayerProfile.MirrorOn ? "ON" : "OFF"), () => PlayerProfile.MirrorOn, () => PlayerProfile.MirrorOn = !PlayerProfile.MirrorOn);
        AddItem(set, () => OnlineManager.Instance != null && OnlineManager.Instance.MicMuted ? "Mic MUTED" : "Mic ON", () => OnlineManager.Instance != null && !OnlineManager.Instance.MicMuted, () => { if (OnlineManager.Instance != null) OnlineManager.Instance.ToggleMic(); });
        AddItem(set, () => "Reconnect", null, () => { if (OnlineManager.Instance != null) OnlineManager.Instance.Reconnect(); });
        AddItem(set, () => "Reset Board", null, () => Scores.Clear());

        // ---- Player ----
        Cat pl = NewCat("player", "Player");
        AddItem(pl, () => "Name " + PlayerProfile.Name, null, () => { PlayerProfile.NameIdx = PlayerProfile.NameIdx + 1; if (PlayFabLite.Instance != null) PlayFabLite.Instance.SyncName(); });
        AddItem(pl, () => "Color " + PlayerProfile.ColorNames[PlayerProfile.ColorIdx], null, () => PlayerProfile.ColorIdx = PlayerProfile.ColorIdx + 1);

        // ---- PlayFab ----
        Cat pf = NewCat("playfab", "PlayFab");
        AddItem(pf, () => PlayFabLite.Instance != null ? PlayFabLite.Instance.Status : "PlayFab off", () => PlayFabLite.Instance != null && PlayFabLite.Instance.LoggedIn, null);
        AddItem(pf, () => "Sign In", null, () => { if (PlayFabLite.Instance != null) PlayFabLite.Instance.Login(); });
        AddItem(pf, () => "Send Best Time", null, () => { if (PlayFabLite.Instance != null) PlayFabLite.Instance.SubmitBestTime(); });
        AddItem(pf, () => "Send Name", null, () => { if (PlayFabLite.Instance != null) PlayFabLite.Instance.SyncName(); });
    }

    Cat NewCat(string id, string title)
    {
        var c = new Cat { title = title };
        cats[id] = c;
        return c;
    }

    void AddItem(Cat c, Func<string> text, Func<bool> isOn, Action click)
    {
        c.items.Add(new Item { text = text, isOn = isOn, click = click });
    }

    int PageCount()
    {
        return Mathf.Max(1, Mathf.CeilToInt(current.items.Count / (float)PerPage));
    }

    void ShowCat(string id)
    {
        currentId = id;
        current = cats[id];
        pageIdx = 0;
        hover = null;
    }

    Btn NewButton(Vector2 pos, Vector2 size, int fontSize)
    {
        RectTransform r = MakeRect("Button", panel.transform, pos, size);
        var b = new Btn { go = r.gameObject };
        b.border = r.gameObject.AddComponent<Image>();
        Rounded(b.border, cEdge);
        Image fill = MakeRect("Fill", r, Vector2.zero, size - new Vector2(5f, 5f)).gameObject.AddComponent<Image>();
        Rounded(fill, cNormal);
        b.bg = fill;
        b.label = MakeLabel("Label", r, Vector2.zero, size - new Vector2(14f, 6f), fontSize, cText);

        var box = r.gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(size.x, size.y, 120f);
        box.isTrigger = true;
        byCollider[box] = b;
        return b;
    }

    // ---------------- look ----------------

    static void Rounded(Image img, Color c)
    {
        if (roundSprite == null) roundSprite = MakeRoundSprite();
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = c;
        img.raycastTarget = false;
    }

    static Sprite MakeRoundSprite()
    {
        const int n = 64;
        const float radius = 18f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (n - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (n - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
    }

    static TMP_FontAsset LoadDaydream()
    {
        if (fontTried) return daydream;
        fontTried = true;
        try
        {
            if (TMP_Settings.instance == null) return null;
            daydream = Resources.Load<TMP_FontAsset>("ModMenuV2/Daydream DEMO SDF");
            if (daydream != null)
            {
                // the demo font only has letters, digits and a few symbols: borrow ":" "|" "<" ">" "-" etc. from TMP's default font
                TMP_FontAsset fb = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (fb != null && !daydream.fallbackFontAssetTable.Contains(fb)) daydream.fallbackFontAssetTable.Add(fb);
            }
        }
        catch (Exception e) { Debug.LogWarning("Daydream font not available, using the default font: " + e.Message); daydream = null; }
        return daydream;
    }

    static Lbl MakeLabel(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color)
    {
        RectTransform r = MakeRect(name, parent, pos, size);
        var l = new Lbl();
        TMP_FontAsset f = LoadDaydream();
        if (f != null)
        {
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = f;
            t.fontSize = fontSize;
            t.enableAutoSizing = true;
            t.fontSizeMin = 8;
            t.fontSizeMax = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = false;
            t.color = color;
            t.raycastTarget = false;
            l.tmp = t;
        }
        else
        {
            var t = r.gameObject.AddComponent<Text>();
            t.font = GetFont();
            t.fontSize = fontSize;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 8;
            t.resizeTextMaxSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            l.legacy = t;
        }
        return l;
    }

    static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 24);
        return f;
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = GtagBootstrap.MenuLayer;
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    // The small ball in front of your right hand: same purple look as the menu. Touch a button with it and pull the trigger.
    void BuildBall()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MenuBall";
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * BallRadius * 2f;

        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool ring = x < 3 || y < 3 || x >= n - 3 || y >= n - 3;
                tex.SetPixel(x, y, ring ? cEdge : cNormal * 1.6f);
            }
        tex.Apply();
        Material m = MonkeModel.NewMaterial(Color.white);
        m.mainTexture = tex;
        go.GetComponent<Renderer>().sharedMaterial = m;
        ball = go.transform;
        go.SetActive(false);
    }

    void BuildTorch()
    {
        var go = new GameObject("Flashlight");
        go.transform.SetParent(boot.Head, false);
        torch = go.AddComponent<Light>();
        torch.type = LightType.Spot;
        torch.range = 35f;
        torch.spotAngle = 60f;
        torch.intensity = 3f;
        torch.shadows = LightShadows.None;
        torch.enabled = false;
    }

    // ---------------- update ----------------

    void Update()
    {
        if (boot == null || !player.enabled) return;

        if (rig.MenuTogglePressed) SetOpen(!open);
        if (boot.IsDying && open) SetOpen(false);

        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f) { fps = fpsFrames / fpsTimer; fpsFrames = 0f; fpsTimer = 0f; }

        UpdateMenu();
        UpdateFly();
        UpdatePlatforms();
        UpdateRainbow();
    }

    void SetOpen(bool value)
    {
        open = value;
        panel.SetActive(open);
        if (ball != null) ball.gameObject.SetActive(open && rig.XRActive);
        if (open) ShowCat("home");
        hover = null;
    }

    int ActiveMods()
    {
        int n = 0;
        if (fly) n++; if (noclip) n++; if (platforms) n++; if (rainbow) n++; if (ghost) n++;
        if (armIdx > 0) n++; if (speedIdx > 0) n++; if (gravIdx > 0) n++; if (god) n++; if (freeze) n++;
        if (flashlight) n++; if (darkness) n++;
        return n;
    }

    void UpdateMenu()
    {
        if (!open) return;

        // worn on the left hand, turned to face your head
        Vector3 headPos = boot.Head.position;
        Vector3 pos;
        if (rig.XRActive)
        {
            Vector3 lp = boot.LeftHand.position;
            pos = lp + boot.LeftHand.up * 0.06f + (headPos - lp).normalized * 0.04f;
        }
        else
        {
            pos = headPos + boot.Head.forward * 0.55f + boot.Head.right * -0.12f + boot.Head.up * -0.05f;
        }
        panel.transform.position = pos;
        panel.transform.rotation = Quaternion.LookRotation(pos - headPos, boot.Head.up);

        // the ball floats a hand's length in front of the right controller
        Vector3 pointer = Vector3.zero;
        if (rig.XRActive)
        {
            pointer = boot.RightHand.position + boot.RightHand.forward * 0.09f;
            ball.position = pointer;
            if (!ball.gameObject.activeSelf) ball.gameObject.SetActive(true);
        }
        else if (ball.gameObject.activeSelf) ball.gameObject.SetActive(false);

        // what is being touched / aimed at?
        hover = null;
        Collider hit = null;
        if (rig.XRActive)
        {
            Collider[] hits = Physics.OverlapSphere(pointer, BallRadius, 1 << GtagBootstrap.MenuLayer, QueryTriggerInteraction.Collide);
            if (hits.Length > 0) hit = hits[0];
        }
        else
        {
            RaycastHit rh;
            Ray ray = new Ray(boot.Head.position, boot.Head.forward);
            if (Physics.Raycast(ray, out rh, 3f, 1 << GtagBootstrap.MenuLayer, QueryTriggerInteraction.Collide))
                hit = rh.collider;
        }
        if (hit != null) byCollider.TryGetValue(hit, out hover);

        if (hover != null && hover.go.activeInHierarchy && hover.click != null && rig.ClickPressed)
        {
            Action click = hover.click;
            rig.Pulse();
            click();   // may switch pages, so it is called last
            hover = null;
        }

        Refresh();
    }

    void Refresh()
    {
        int pages = PageCount();
        titleText.text = current.title + (pages > 1 ? " " + (pageIdx + 1) + "/" + pages : "");
        statsText.text = Mathf.RoundToInt(fps) + " FPS  |  " + ActiveMods() + " active";

        StyleButton(navBack, false);
        StyleButton(navPrev, false);
        StyleButton(navNext, false);
        navPrev.go.SetActive(pages > 1);
        navNext.go.SetActive(pages > 1);

        for (int i = 0; i < PerPage; i++)
        {
            Btn b = slots[i];
            int idx = pageIdx * PerPage + i;
            bool has = idx < current.items.Count;
            if (b.go.activeSelf != has) b.go.SetActive(has);
            if (!has) { b.click = null; continue; }
            Item it = current.items[idx];
            b.text = it.text;
            b.isOn = it.isOn;
            b.click = it.click;
            StyleButton(b, true);
        }

        OnlineManager om = OnlineManager.Instance;
        footerText.text = om != null ? om.Status + "  |  " + om.VoiceStatus : "";
    }

    void StyleButton(Btn b, bool allowOn)
    {
        b.label.text = b.text();
        bool on = allowOn && b.isOn != null && b.isOn();
        Color fill = cNormal;
        if (on) fill = cOn;
        if (b == hover && b.click != null) fill = cHover;
        b.bg.color = fill;
        b.label.color = on ? cTextOn : cText;
    }

    void UpdateFly()
    {
        bool flying = fly || noclip;
        body.useGravity = !flying;
        if (!flying) return;

        if (rig.FlyHeld && !open && !boot.IsDying)
        {
            Vector3 dir = rig.XRActive ? boot.RightHand.forward : boot.Head.forward;
            body.velocity = dir * FlySpeed;
        }
        else
        {
            body.velocity = Vector3.zero;
        }
    }

    void UpdatePlatforms()
    {
        if (!platforms) return;
        HandPlatform(0, rig.LeftGrip, boot.LeftHand);
        HandPlatform(1, rig.RightGrip, boot.RightHand);
    }

    void HandPlatform(int i, bool grip, Transform hand)
    {
        if (grip && plat[i] == null)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "Platform";
            p.transform.localScale = new Vector3(0.5f, 0.04f, 0.5f);
            p.transform.position = hand.position + Vector3.down * 0.05f;
            p.transform.rotation = Quaternion.Euler(0f, hand.eulerAngles.y, 0f);
            p.GetComponent<Renderer>().sharedMaterial = MonkeModel.NewMaterial(Color.white);   // primitives' default shader is stripped from builds -> magenta
            p.GetComponent<Renderer>().material.color = new Color(0.2f, 0.75f, 0.3f);
            plat[i] = p;
        }
        else if (!grip && plat[i] != null)
        {
            Destroy(plat[i]);
            plat[i] = null;
        }
    }

    void ClearPlatforms()
    {
        for (int i = 0; i < plat.Length; i++)
        {
            if (plat[i] != null) Destroy(plat[i]);
            plat[i] = null;
        }
    }

    // ---------------- mods / settings ----------------

    void ApplySpeed()
    {
        player.jumpMultiplier = JumpMult[speedIdx];
        player.maxJumpSpeed = MaxJump[speedIdx];
    }

    void ApplyGravity()
    {
        Physics.gravity = Vector3.down * 9.81f * GravScale[gravIdx];
    }

    // Longer reach, but the push you get from each swing stays the same as normal arms.
    void ApplyArms()
    {
        rig.ArmScale = ArmScales[armIdx];
        player.maxArmLength = BaseArmLength * ArmScales[armIdx];
        player.handMoveScale = 1f / ArmScales[armIdx];
    }

    void SetRainbow(bool on)
    {
        rainbow = on;
        PlayerProfile.Rainbow = on;
        PlayerProfile.Refresh();
    }

    void UpdateRainbow()
    {
        if (!rainbow || Time.time < rainbowNext) return;
        rainbowNext = Time.time + 0.07f;
        PlayerProfile.Refresh();
    }

    void SetGhost(bool on)
    {
        ghost = on;
        if (OnlineManager.Instance != null) OnlineManager.Instance.Ghost = on;
    }

    void ApplyFreeze()
    {
        boot.SetMonstersFrozen(freeze);
    }

    void ToggleMonster()
    {
        PlayerProfile.MonsterOn = !PlayerProfile.MonsterOn;
        boot.SetMonsterEnabled(PlayerProfile.MonsterOn);
        ApplyFreeze();
    }

    void SetNoclip(bool on)
    {
        noclip = on;
        player.locomotionEnabledLayers = on ? (LayerMask)0 : (LayerMask)1;
        boot.HeadCollider.isTrigger = on;
        boot.BodyCollider.isTrigger = on;
        body.velocity = Vector3.zero;
        boot.ResyncPlayer();

        // turning noclip off while inside a wall would trap you - go back to the stump instead
        if (!on && Physics.CheckSphere(boot.Head.position, 0.25f, 1 << 0, QueryTriggerInteraction.Ignore))
            boot.TeleportToSpawn();
    }

    void SetDarkness(bool on)
    {
        darkness = on;
        boot.Sun.intensity = on ? 0.04f : 1f;
        RenderSettings.ambientLight = on ? new Color(0.02f, 0.02f, 0.03f) : new Color(0.55f, 0.58f, 0.62f);
        RenderSettings.fog = on;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.035f;
        RenderSettings.fogColor = Color.black;
        boot.Cam.backgroundColor = on ? Color.black : new Color(0.45f, 0.62f, 0.85f);
        if (on && !flashlight) { flashlight = true; torch.enabled = true; }
    }

    void ResetMods()
    {
        speedIdx = 0; gravIdx = 0; armIdx = 0;
        fly = false; platforms = false; flashlight = false; freeze = false; god = false;
        if (rainbow) SetRainbow(false);
        if (ghost) SetGhost(false);
        torch.enabled = false;
        ClearPlatforms();
        if (noclip) SetNoclip(false);
        if (darkness) SetDarkness(false);
        boot.Invincible = false;
        ApplySpeed(); ApplyGravity(); ApplyArms(); ApplyFreeze();
        body.useGravity = true;
    }

    // ---------------- moderator ----------------

    ulong CurrentTarget(out bool ok)
    {
        ok = false;
        OnlineManager om = OnlineManager.Instance;
        if (om == null) return 0;
        List<ulong> ids = om.RemoteIds();
        if (ids.Count == 0) return 0;
        targetIdx = Mathf.Clamp(targetIdx, 0, ids.Count - 1);
        ok = true;
        return ids[targetIdx];
    }

    string TargetName()
    {
        bool ok;
        ulong id = CurrentTarget(out ok);
        return ok ? OnlineManager.Instance.NameOf(id) : "none online";
    }

    void CycleTarget()
    {
        OnlineManager om = OnlineManager.Instance;
        if (om == null) return;
        int n = om.RemoteIds().Count;
        if (n > 0) targetIdx = (targetIdx + 1) % n;
    }

    void ModAction(byte action)
    {
        bool ok;
        ulong id = CurrentTarget(out ok);
        if (ok) OnlineManager.Instance.SendMod(action, id, player.transform.position);
    }

    void GoToTarget()
    {
        bool ok;
        ulong id = CurrentTarget(out ok);
        if (ok) OnlineManager.Instance.TeleportToPlayer(id);
    }

    void OnDestroy()
    {
        Physics.gravity = Vector3.down * 9.81f;
    }
}
