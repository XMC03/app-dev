using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    // The included scene needs no hand-wired inspector references or downloaded assets.
    public sealed class NightfallGame : MonoBehaviour
    {
        private Campaign run;
        private Camera view;
        private GameObject world;
        private SpriteRenderer player, gate;
        private readonly List<SpriteRenderer> spirits = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> crystals = new List<SpriteRenderer>();
        private readonly List<Point> crystalPositions = new List<Point>();
        private readonly List<UnityEngine.Object> generated = new List<UnityEngine.Object>();
        private readonly List<Spark> sparks = new List<Spark>();
        private Sprite playerArt, spiritArt, treeArt, crystalArt, closedGateArt, openGateArt, sparkArt;
        private Texture2D white;
        private AudioSource audioSource;
        private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        private GUIStyle titleStyle, headingStyle, labelStyle, smallStyle, buttonStyle, centerStyle;
        private bool muted;
        private double best;
        private int builtLevel = -1;
        private int lastCrystals;
        private string notice = "Gather eight crystals, then reach the northeast gate.";
        private float noticeTime;
        private static readonly Color Gold = new Color32(232, 204, 137, 255);
        private static readonly Color Ink = new Color32(231, 237, 220, 255);
        private static readonly Color Muted = new Color32(157, 176, 161, 255);
        private class Spark { public GameObject Object; public Vector3 Velocity; public float Life; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (UnityEngine.Object.FindFirstObjectByType<NightfallGame>() == null)
                new GameObject("Nightfall — Game").AddComponent<NightfallGame>();
        }
        private void Awake()
        {
            run = new Campaign(); run.Effect += HandleEffect;
            muted = PlayerPrefs.GetInt("Nightfall.Muted", 1) == 1;
            best = PlayerPrefs.GetFloat("Nightfall.Best", 0);
            Application.targetFrameRate = 60;
            view = new GameObject("Forest Camera").AddComponent<Camera>(); view.tag = "MainCamera";
            view.orthographic = true; view.orthographicSize = 7.5f;
            view.transform.position = new Vector3(12, -7.5f, -10);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color32(20, 37, 39, 255);
            view.gameObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0;
            CreateArtwork(); CreateSounds(); BuildWorld();
        }
        private static Vector3 Position(Point point) { return new Vector3((float)point.X / 40, -(float)point.Y / 40, 0); }
        private static int Order(Point point) { return 100 + (int)point.Y; }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) run.Pause();
            if (Input.GetKeyDown(KeyCode.Return) && run.State != RunState.Playing) run.PrimaryAction();
            if (Input.GetKeyDown(KeyCode.R)) run.RestartChapter();
            int dx = (Held(KeyCode.D, KeyCode.RightArrow) ? 1 : 0) - (Held(KeyCode.A, KeyCode.LeftArrow) ? 1 : 0);
            int dy = (Held(KeyCode.S, KeyCode.DownArrow) ? 1 : 0) - (Held(KeyCode.W, KeyCode.UpArrow) ? 1 : 0);
            run.Step(Time.deltaTime, dx, dy, Held(KeyCode.LeftShift, KeyCode.RightShift));
            if (builtLevel != run.Level || (run.Collected == 0 && lastCrystals > 0)) BuildWorld();
            lastCrystals = run.Collected;
            player.transform.position = Position(run.Player); player.sortingOrder = Order(run.Player); player.flipX = run.Facing < 0;
            player.enabled = run.Protection <= 0 || ((int)(run.Elapsed * 12) % 2 == 0);
            for (int i = 0; i < spirits.Count; i++)
            {
                var point = run.Spirits[i].Position;
                spirits[i].transform.position = Position(point) + Vector3.up * (float)Math.Sin(run.Elapsed * 3 + i * 2) * .06f;
                spirits[i].sortingOrder = Order(point);
            }
            for (int i = 0; i < crystals.Count; i++)
            {
                bool present = false;
                foreach (var point in run.Crystals) if (Point.Distance(point, crystalPositions[i]) < 1) { present = true; break; }
                crystals[i].gameObject.SetActive(present);
                if (present) crystals[i].transform.position = Position(crystalPositions[i]) + Vector3.up * (float)Math.Sin(run.Elapsed * 3 + i) * .07f;
            }
            gate.sprite = run.GateOpen ? openGateArt : closedGateArt;
            if (run.State == RunState.Playing)
            {
                noticeTime = Mathf.Max(0, noticeTime - Time.deltaTime);
                for (int i = sparks.Count - 1; i >= 0; i--)
                {
                    sparks[i].Life -= Time.deltaTime; sparks[i].Object.transform.position += sparks[i].Velocity * Time.deltaTime;
                    if (sparks[i].Life <= 0) { Destroy(sparks[i].Object); sparks.RemoveAt(i); }
                }
            }
            FitCamera();
        }
        private static bool Held(KeyCode a, KeyCode b) { return Input.GetKey(a) || Input.GetKey(b); }
        private void OnApplicationFocus(bool focused) { if (!focused && run != null && run.State == RunState.Playing) run.Pause(); }
        private void OnApplicationPause(bool paused) { if (paused && run != null && run.State == RunState.Playing) run.Pause(); }
        private float UiScale { get { return Mathf.Min(Screen.width / 1040f, Screen.height / 820f); } }
        private void FitCamera()
        {
            float s = UiScale, ox = (Screen.width - 1040 * s) / 2, oy = (Screen.height - 820 * s) / 2;
            view.rect = new Rect((ox + 40 * s) / Screen.width, (Screen.height - oy - 728 * s) / Screen.height, 960 * s / Screen.width, 600 * s / Screen.height);
        }
        private SpriteRenderer MakeSprite(string name, Sprite art, Point position, int order)
        {
            var obj = new GameObject(name); obj.transform.SetParent(world.transform);
            obj.transform.position = Position(position);
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = art; renderer.sortingOrder = order;
            return renderer;
        }
        private void BuildWorld()
        {
            if (world != null) { world.SetActive(false); Destroy(world); }
            world = new GameObject("Glade " + (run.Level + 1)); spirits.Clear(); crystals.Clear(); crystalPositions.Clear(); sparks.Clear();
            var ground = MakeSprite("Forest floor", floorArt, new Point(480, 300), -10);
            ground.transform.localScale = Vector3.one * 2;
            for (int i = 0; i < run.Trees.Length; i++) MakeSprite("Tree " + i, treeArt, run.Trees[i], Order(run.Trees[i]));
            foreach (var point in Campaign.Chapters[run.Level].Crystals)
            { crystals.Add(MakeSprite("Light crystal", crystalArt, point, 20)); crystalPositions.Add(point); }
            gate = MakeSprite("Northeast gate", closedGateArt, run.Gate, 50);
            player = MakeSprite("Traveler", playerArt, run.Player, Order(run.Player));
            for (int i = 0; i < run.Spirits.Count; i++) spirits.Add(MakeSprite("Spirit " + i, spiritArt, run.Spirits[i].Position, Order(run.Spirits[i].Position)));
            builtLevel = run.Level; lastCrystals = run.Collected;
        }
        private void HandleEffect(string effect, Point point)
        {
            if (!muted && sounds.ContainsKey(effect)) audioSource.PlayOneShot(sounds[effect]);
            if (effect == "crystal" || effect == "hurt" || effect == "gate")
            {
                Color color = effect == "hurt" ? new Color32(232, 155, 145, 255) : Gold;
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2 / 12;
                    var renderer = MakeSprite("Light spark", sparkArt, point, 1000); renderer.color = color;
                    sparks.Add(new Spark { Object = renderer.gameObject, Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 1.5f, Life = .6f });
                }
            }
            if (effect == "gate") { notice = "The gate is open! Head to the northeast corner."; noticeTime = 5; }
            if (effect == "hurt") { notice = "A spirit found you. Dash to create some space!"; noticeTime = 3; }
            if (effect == "win" && run.State == RunState.Won && (best <= 0 || run.CampaignTime < best))
            { best = run.CampaignTime; PlayerPrefs.SetFloat("Nightfall.Best", (float)best); PlayerPrefs.Save(); }
        }
        private void OnGUI()
        {
            if (run == null) return; if (titleStyle == null) CreateStyles();
            var oldMatrix = GUI.matrix; var oldColor = GUI.color;
            float s = UiScale;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1040 * s) / 2, (Screen.height - 820 * s) / 2, 0), Quaternion.identity, Vector3.one * s);
            Label(40, 18, 700, 44, "✦  N I G H T F A L L", headingStyle);
            Label(40, 61, 700, 22, "GATHER THE LIGHT. FIND YOUR WAY HOME.", smallStyle);
            if (GUI.Button(new Rect(860, 30, 140, 36), muted ? "Sound off" : "Sound on", buttonStyle))
            { muted = !muted; PlayerPrefs.SetInt("Nightfall.Muted", muted ? 1 : 0); PlayerPrefs.Save(); }
            Box(new Rect(40, 90, 960, 38), new Color32(29, 49, 49, 255));
            Label(56, 94, 230, 27, "HEARTS  " + new string('♥', run.Hearts) + new string('♡', 3 - run.Hearts), labelStyle);
            Label(280, 94, 220, 27, "LIGHT  " + run.Collected + " / 8", labelStyle);
            Label(520, 94, 210, 27, "TIME  " + TimeText(run.Elapsed), labelStyle);
            if (GUI.Button(new Rect(880, 94, 106, 28), run.State == RunState.Paused ? "Resume [P]" : "Pause [P]", buttonStyle)) run.Pause();
            Box(new Rect(40, 728, 960, 43), new Color32(29, 49, 49, 255));
            Label(56, 736, 65, 25, "DASH", smallStyle); Box(new Rect(120, 746, 90, 5), new Color32(53, 74, 69, 255));
            Box(new Rect(120, 746, (float)run.Stamina * 90, 5), new Color32(145, 193, 168, 255));
            string hint = noticeTime > 0 ? notice : run.GateOpen ? "Gate open — head northeast." : "Collect all eight crystals. Keep moving.";
            Label(230, 736, 530, 27, hint, smallStyle);
            if (GUI.Button(new Rect(840, 735, 146, 28), "Restart chapter [R]", buttonStyle)) run.RestartChapter();
            Label(40, 783, 700, 25, "WASD / ARROWS  Move    •    SHIFT  Dash    •    P / ESC  Pause", smallStyle);
            Label(790, 783, 210, 25, "GLADE 0" + (run.Level + 1) + " / 03", smallStyle);
            if (run.GateOpen && run.State == RunState.Playing)
            {
                Vector3 p = view.WorldToScreenPoint(Position(run.Gate));
                float x = (p.x - (Screen.width - 1040 * s) / 2) / s;
                float y = (Screen.height - p.y - (Screen.height - 820 * s) / 2) / s;
                Label(x - 50, y - 68, 100, 26, "GATE OPEN", centerStyle);
            }
            if (run.State != RunState.Playing) DrawOverlay();
            GUI.matrix = oldMatrix; GUI.color = oldColor;
        }
        private void DrawOverlay()
        {
            Box(new Rect(40, 128, 960, 600), new Color(0.04f, .10f, .10f, .82f));
            Box(new Rect(235, 233, 570, 375), new Color32(25, 46, 44, 250));
            string title, copy, action;
            switch (run.State)
            {
                case RunState.Paused: title = "The forest can wait."; copy = "Take a breath. Your adventure is paused."; action = "Continue adventure"; break;
                case RunState.Lost: title = "A light worth saving."; copy = "The spirits found you. Try this glade again.\nCompleted chapters are kept. Use your dash!"; action = "Try this chapter again"; break;
                case RunState.Cleared: title = "One step closer."; copy = Campaign.Chapters[run.Level].Name + " crossed in " + TimeText(run.Elapsed) + ".\nYour hearts and dash are restored in the next glade."; action = "Enter the next glade"; break;
                case RunState.Won: title = "You found your way home."; copy = "All 24 crystals gathered. All three gates crossed.\nYour adventure took " + TimeText(run.CampaignTime) + "."; action = "Play a new adventure"; break;
                default: title = "A light in the dark."; copy = "Gather eight crystals to open the forest gate.\nEvade the spirits and cross three glades to get home."; action = "Enter the forest"; break;
            }
            Label(270, 257, 500, 35, "✦  A THREE CHAPTER ADVENTURE  ✦", centerStyle);
            var centerTitle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter };
            Label(250, 307, 540, 55, title, centerTitle);
            Label(270, 381, 500, 80, copy, centerStyle);
            if (GUI.Button(new Rect(365, 484, 310, 48), action + "  →", buttonStyle)) run.PrimaryAction();
            Label(270, 552, 500, 30, best > 0 ? "BEST ADVENTURE  " + TimeText(best) : "Press Enter to begin. A little courage goes a long way.", centerStyle);
        }
        private static string TimeText(double time) { return ((int)time / 60).ToString("00") + ":" + ((int)time % 60).ToString("00"); }
        private void Box(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, white); GUI.color = Color.white; }
        private static void Label(float x, float y, float w, float h, string text, GUIStyle style) { GUI.Label(new Rect(x, y, w, h), text, style); }
        private GUIStyle Style(int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        { var style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = alignment, wordWrap = true }; style.normal.textColor = color; return style; }
        private void CreateStyles()
        {
            titleStyle = Style(34, Ink); headingStyle = Style(24, Gold); labelStyle = Style(16, Ink); smallStyle = Style(13, Muted); centerStyle = Style(16, Muted, TextAnchor.MiddleCenter);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = Solid(Gold); buttonStyle.normal.textColor = new Color32(29, 48, 43, 255);
            buttonStyle.hover.background = Solid(new Color32(249, 223, 161, 255)); buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.background = buttonStyle.hover.background; buttonStyle.active.textColor = buttonStyle.normal.textColor;
        }
        private Texture2D Solid(Color color)
        { var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); generated.Add(texture); return texture; }
        private Sprite floorArt;
        private Sprite Art(int width, int height, Func<int, int, Color32> pixel, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
            texture.SetPixels32(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 40);
            generated.Add(texture); generated.Add(sprite); return sprite;
        }
        private static bool In(int x, int y, int l, int b, int w, int h) { return x >= l && y >= b && x < l + w && y < b + h; }
        private static Color32 Clear { get { return new Color32(0, 0, 0, 0); } }
        private void CreateArtwork()
        {
            white = Solid(Color.white);
            playerArt = Art(32, 36, (x, y) => {
                if (In(x,y,23,7,5,9)) return new Color32(244,209,135,255);
                if (In(x,y,10,0,5,10) || In(x,y,19,0,5,10)) return new Color32(39,50,53,255);
                if (In(x,y,7,9,20,14)) return new Color32(119,174,150,255);
                if (In(x,y,10,23,14,8)) return new Color32(224,193,151,255);
                if (In(x,y,6,29,22,4) || In(x,y,10,32,14,4)) return new Color32(85,127,102,255);
                return Clear;
            }, new Vector2(.5f, .35f));
            spiritArt = Art(28, 32, (x,y) => {
                if (In(x,y,8,17,3,4) || In(x,y,18,17,3,4)) return new Color32(253,222,174,255);
                if (In(x,y,5,5,19,20) || In(x,y,8,25,13,5) || In(x,y,5,0,5,7) || In(x,y,19,0,5,7)) return new Color32(174,128,153,255);
                if (In(x,y,1,9,5,12) || In(x,y,23,9,5,12)) return new Color32(133,92,123,255);
                return Clear;
            }, new Vector2(.5f, .35f));
            treeArt = Art(76, 86, (x,y) => {
                if (In(x,y,33,0,10,28)) return new Color32(97,83,57,255);
                double a = Math.Pow((x-38)/31.0,2)+Math.Pow((y-51)/30.0,2), b = Math.Pow((x-18)/17.0,2)+Math.Pow((y-38)/21.0,2), c = Math.Pow((x-59)/17.0,2)+Math.Pow((y-40)/23.0,2);
                if (a<1 || b<1 || c<1) {
                    if ((x*7+y*11)%47<5 && y>45) return new Color32(81,118,74,255);
                    return y>49?new Color32(57,97,66,255):new Color32(36,76,53,255);
                }
                return Clear;
            }, new Vector2(.5f, .25f));
            crystalArt = Art(20, 28, (x,y) => Math.Abs(x-10)*1.7+Math.Abs(y-14)<14 ? (x<10?new Color32(251,225,155,255):new Color32(208,174,97,255)) : Clear, new Vector2(.5f,.5f));
            Func<bool, Sprite> gateArt = open => Art(56, 64, (x,y) => {
                if(In(x,y,3,0,10,54)||In(x,y,43,0,10,54)||In(x,y,3,52,50,10))return new Color32(125,145,111,255);
                if(In(x,y,13,0,30,52)){
                    if(open)return new Color32(170,231,185,(byte)(x%5==0?170:105));
                    if(x%10<3||In(x,y,13,25,30,4))return new Color32(87,112,91,255);
                }return Clear;
            }, new Vector2(.5f,.25f));
            closedGateArt=gateArt(false); openGateArt=gateArt(true);
            sparkArt = Art(3,3,(x,y)=>new Color32(255,255,255,255),new Vector2(.5f,.5f));
            floorArt = Art(480, 300, (x,y) => {
                int tx=x/20,ty=y/20,seed=(tx*73+ty*137)%97;
                if(tx==0||ty==0||tx==23||ty==14)return In(x%20,y%20,3,4,14,12)?new Color32(43,70,51,255):new Color32(25,48,40,255);
                if((x*31+y*17)%179==0)return new Color32(90,116,70,255);
                return new Color32((byte)(35+seed%7),(byte)(59+seed%9),(byte)(45+seed%6),255);
            },new Vector2(.5f,.5f));
        }
        private void CreateSounds()
        {
            Tone("crystal", new[] { 660f, 880f }); Tone("hurt", new[] { 170f, 110f }); Tone("gate", new[] { 440f, 660f, 880f });
            Tone("dash", new[] { 250f, 400f }); Tone("win", new[] { 440f, 554f, 660f, 880f });
        }
        private void Tone(string name, float[] notes)
        {
            const int rate = 22050; int noteSamples = (int)(rate * .13f); var samples = new float[noteSamples * notes.Length];
            for (int i = 0; i < notes.Length; i++) for (int n = 0; n < noteSamples; n++)
            { float t = n / (float)rate, envelope = Mathf.Sin(Mathf.PI*n/noteSamples); samples[i*noteSamples+n]=Mathf.Sin(2*Mathf.PI*notes[i]*t)*envelope*.16f; }
            var clip = AudioClip.Create("Nightfall " + name, samples.Length, 1, rate, false); clip.SetData(samples,0); sounds[name]=clip; generated.Add(clip);
        }
        private void OnDestroy()
        {
            if (run != null) run.Effect -= HandleEffect;
            if (world != null) Destroy(world); if(view!=null)Destroy(view.gameObject);
            foreach (var asset in generated) if(asset!=null)Destroy(asset);
        }
    }
}
