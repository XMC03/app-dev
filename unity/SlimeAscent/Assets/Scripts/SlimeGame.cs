using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeAscent
{
    public sealed class SlimeGame : MonoBehaviour
    {
        private static SlimeGame instance;
        private readonly string[] scenes={"Lower","Middle","Upper"};
        private Rules run;
        private Progress saved;
        private Camera view;
        private GameObject world;
        private SpriteRenderer slime,gate,core;
        private readonly Dictionary<int,SpriteRenderer> enemies=new Dictionary<int,SpriteRenderer>();
        private readonly Dictionary<int,SpriteRenderer> cones=new Dictionary<int,SpriteRenderer>();
        private readonly List<SpriteRenderer> hazardViews=new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> shotViews=new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> villagers=new List<SpriteRenderer>();
        private readonly List<UnityEngine.Object> generated=new List<UnityEngine.Object>();
        private SlimeArt art;
        private SpriteRenderer slimeShadow,flameAura,hardenAura,senseAura,bossWarning,riftAura;
        private readonly Dictionary<int,SpriteRenderer> enemyShadows=new Dictionary<int,SpriteRenderer>();
        private readonly Dictionary<int,double> lastHP=new Dictionary<int,double>();
        private readonly Dictionary<int,float> hitFlashes=new Dictionary<int,float>();
        private readonly List<VisualEffect> visualEffects=new List<VisualEffect>();
        private readonly List<DamageText> damageTexts=new List<DamageText>();
        private class VisualEffect {public SpriteRenderer Renderer;public int Kind;public float Time,Duration;public Vec Start;public bool Inward;}
        private class DamageText {public Vec Position;public float Time;public int Value;}
        private int slimeAction=SlimeArt.Idle;
        private double actionStarted;
        private float actionDuration;
        private Sprite oilArt,gasArt,barrelArt,crackArt,plateArt,rockArt,riftArt,coreArt,visionArt,sparkArt;

        private Texture2D white;
        private readonly Dictionary<string,AudioClip> sounds=new Dictionary<string,AudioClip>();
        private AudioSource fx,music;
        private AudioClip[] tracks=new AudioClip[4];
        private bool muted;
        private string shownScene;
        private bool loading;
        private GUIStyle title,heading,label,small,center,button;
        private float escapeTime;
        private readonly List<Particle> particles=new List<Particle>();
        private class Particle {public GameObject Object;public Vector3 Velocity;public float Life;}
        private static readonly Color Gold=new Color32(232,204,137,255),Pale=new Color32(224,235,225,255),Mint=new Color32(142,217,160,255);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() {if(UnityEngine.Object.FindAnyObjectByType<SlimeGame>()==null)new GameObject("Slime Ascent — Game").AddComponent<SlimeGame>();}
        private void Awake()
        {
            if(instance!=null && instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);
            run=new Rules();run.Effect+=Effect;run.FloorEntered+=EnterFloor;run.Escaped+=Escape;
            try{string json=PlayerPrefs.GetString("SlimeAscent.Checkpoint.v1","");if(json.Length>0){var p=JsonUtility.FromJson<Progress>(json);if(p!=null&&p.Valid())saved=p;}}catch{saved=null;}
            muted=PlayerPrefs.GetInt("SlimeAscent.Muted",0)==1;
            view=new GameObject("Slime Ascent Camera").AddComponent<Camera>();view.transform.SetParent(transform);view.tag="MainCamera";
            view.orthographic=true;view.orthographicSize=5.625f;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color32(15,21,30,255);
            view.gameObject.AddComponent<AudioListener>();
            fx=gameObject.AddComponent<AudioSource>();fx.spatialBlend=0;music=gameObject.AddComponent<AudioSource>();music.spatialBlend=0;music.loop=true;music.volume=.23f;
            Application.targetFrameRate=60;CreateArt();CreateAudio();SceneManager.sceneLoaded+=SceneLoaded;
            shownScene=SceneManager.GetActiveScene().name;
            int direct=Array.IndexOf(scenes,shownScene);
            if(direct>=0){var p=new Progress{Floor=direct,Level=direct*2+1,HP=36+direct*24};run.Continue(p);}
            else BuildWorld(false);
        }
        private static Vector3 Position(Vec p){return new Vector3((float)p.X,-(float)p.Y,0);}
        private float Scale {get{return Mathf.Min(Screen.width/1280f,Screen.height/720f);}}
        private void EnterFloor(int floor)
        {
            saved=run.Checkpoint.Copy();PlayerPrefs.SetString("SlimeAscent.Checkpoint.v1",JsonUtility.ToJson(saved));PlayerPrefs.Save();
            loading=true;SceneManager.LoadScene(scenes[floor]);
        }
        private void Escape(){escapeTime=0;loading=true;SceneManager.LoadScene("HumanWorld");}
        private void SceneLoaded(Scene scene,LoadSceneMode mode){shownScene=scene.name;BuildWorld(scene.name=="HumanWorld");loading=false;}
        private void NewGame(){run.NewRun();if(shownScene!="Opening"){loading=true;SceneManager.LoadScene("Opening");}}
        private static bool Held(KeyCode a,KeyCode b){return Input.GetKey(a)||Input.GetKey(b);}
        private void Update()
        {
            if(run==null||loading)return;
            if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.P))run.Pause();
            if(Input.GetKeyDown(KeyCode.Tab))run.EvolveMenu();
            if(Input.GetKeyDown(KeyCode.Return)){if(run.State==Phase.Menu)NewGame();else if(run.State==Phase.Intro)run.SkipIntro();else if(run.State==Phase.Paused)run.Pause();else if(run.State==Phase.Dead)run.RetryFloor();}
            float s=Scale,ox=(Screen.width-1280*s)/2,oy=(Screen.height-720*s)/2;
            view.rect=new Rect(ox/Screen.width,oy/Screen.height,1280*s/Screen.width,720*s/Screen.height);
            Vector3 aim=view.ScreenToWorldPoint(Input.mousePosition);double uiY=(Screen.height-Input.mousePosition.y-oy)/s;
            bool mouseInWorld=uiY>100&&uiY<610;
            var controls=new Controls{
                X=(Held(KeyCode.D,KeyCode.RightArrow)?1:0)-(Held(KeyCode.A,KeyCode.LeftArrow)?1:0),
                Y=(Held(KeyCode.S,KeyCode.DownArrow)?1:0)-(Held(KeyCode.W,KeyCode.UpArrow)?1:0),
                Aim=new Vec(aim.x,-aim.y)-run.Player,Bite=Input.GetMouseButton(0)&&mouseInWorld,Slam=Input.GetMouseButtonDown(1)&&mouseInWorld,
                Tackle=Input.GetKeyDown(KeyCode.Space),Devour=Input.GetKey(KeyCode.E),Hide=Input.GetKey(KeyCode.LeftShift),Gate=Input.GetKeyDown(KeyCode.F),
                Skill=Input.GetKeyDown(KeyCode.Alpha1)?1:Input.GetKeyDown(KeyCode.Alpha2)?2:Input.GetKeyDown(KeyCode.Alpha3)?3:Input.GetKeyDown(KeyCode.Alpha4)?4:0};
            run.Step(Time.deltaTime,controls);
            if(loading)return;
            if(shownScene=="HumanWorld")
            {
                escapeTime+=Time.deltaTime;
                for(int i=0;i<villagers.Count;i++){villagers[i].sprite=art.Enemies[4,i%2==0?3:1,(int)(escapeTime*8)%4];villagers[i].transform.position=new Vector3(7+i*2+(i%2==0?-1:1)*escapeTime*1.4f,-8.3f,0);}
                return;
            }
            float halfWidth=10,halfHeight=5.625f;
            Vector3 target=new Vector3(Mathf.Clamp((float)run.Player.X,halfWidth,Rules.Width-halfWidth),-Mathf.Clamp((float)run.Player.Y,halfHeight,Rules.Height-halfHeight),-10);
            view.transform.position=Vector3.Lerp(view.transform.position,target,1-Mathf.Exp(-Time.deltaTime*9));
            if(slime==null)return;
            int frame=(int)(run.Clock*7)%4,dir=Direction(run.Facing),rank=Math.Min(3,run.Stats.FireRank);
            int action=run.DevourTarget>=0?SlimeArt.Devour:run.Clock-actionStarted<actionDuration?slimeAction:controls.X!=0||controls.Y!=0?SlimeArt.Walk:SlimeArt.Idle;
            int animationFrame=action==SlimeArt.Idle?((int)(run.Clock*3)%4):action==SlimeArt.Walk?frame:action==SlimeArt.Devour?(int)(run.DevourTime*10)%4:Math.Min(3,(int)((run.Clock-actionStarted)/Math.Max(.01,actionDuration)*4));
            slime.sprite=art.Slimes[rank,dir,action,animationFrame];slime.transform.position=Position(run.Player);slime.sortingOrder=Order(run.Player);
            slime.transform.localScale=Vector3.one*(1+Mathf.Min(.6f,(run.Stats.Level-1)*.04f));
            slimeShadow.transform.position=Position(run.Player)+Vector3.down*.18f;slimeShadow.sortingOrder=Order(run.Player)-1;
            slimeShadow.transform.localScale=slime.transform.localScale*.7f;slimeShadow.color=new Color(0,0,0,run.Hidden?.13f:.32f);
            slime.color=run.Hidden?new Color(1,1,1,.3f):Color.white;
            slime.enabled=run.Protection<=0||(int)(run.Clock*15)%2==0;
            foreach(var enemy in run.Enemies)
            {
                var r=enemies[enemy.Id];r.gameObject.SetActive(enemy.State!=Mind.Devoured);r.transform.position=Position(enemy.Position);r.sortingOrder=Order(enemy.Position);
                r.sprite=art.Enemies[(int)enemy.Kind,Direction(enemy.Facing),frame];
                if(enemy.HP<lastHP[enemy.Id]){hitFlashes[enemy.Id]=.12f;damageTexts.Add(new DamageText{Position=enemy.Position,Time=.7f,Value=(int)Math.Ceiling(lastHP[enemy.Id]-enemy.HP)});}
                lastHP[enemy.Id]=enemy.HP;hitFlashes[enemy.Id]=Mathf.Max(0,hitFlashes[enemy.Id]-Time.deltaTime);
                r.color=enemy.State==Mind.Defeated?new Color(.55f,.55f,.6f,.68f):hitFlashes[enemy.Id]>0?new Color(1,.55f,.55f):Color.white;
                if(enemy.State==Mind.Defeated)r.transform.localScale=new Vector3(1,.45f,1);
                var shadow=enemyShadows[enemy.Id];shadow.enabled=enemy.State!=Mind.Devoured;shadow.transform.position=Position(enemy.Position)+Vector3.down*.22f;shadow.sortingOrder=Order(enemy.Position)-1;
                var cone=cones[enemy.Id];cone.enabled=enemy.State!=Mind.Defeated&&enemy.State!=Mind.Devoured;cone.transform.position=Position(enemy.Position);
                cone.transform.rotation=Quaternion.Euler(0,0,(float)(Math.Atan2(-enemy.Facing.Y,enemy.Facing.X)*180/Math.PI));
                cone.color=enemy.Alert>.55?new Color(1,.25f,.22f,.13f):new Color(1,.8f,.5f,.07f);
            }
            for(int i=0;i<run.Hazards.Count;i++)
            {
                var h=run.Hazards[i];var r=hazardViews[i];
                r.sprite=h.Active?art.Effects[h.Kind==HazardType.Gas?1:0,frame]:h.Kind==HazardType.Oil?h.Barrel&&!h.Triggered?barrelArt:oilArt:h.Kind==HazardType.Gas?h.Barrel&&!h.Triggered?art.Props[30]:gasArt:h.Kind==HazardType.Plate?plateArt:rockArt;
                r.sortingOrder=h.Active?Order(h.Position):-15;r.transform.localScale=Vector3.one*(h.Active?.85f:1);
                r.color=h.RockTimer>=0?new Color(1,.45f,.4f):Color.white;
                if(h.Kind==HazardType.Rock){r.enabled=h.RockTimer>=0||h.Triggered;if(h.RockTimer>=0){r.sprite=art.Effects[9,3];r.transform.localScale=Vector3.one*(.8f/.9375f);}}
            }
            while(shotViews.Count<run.Shots.Count)shotViews.Add(Sprite("Projectile",art.Effects[8,0],run.Player,400));
            for(int i=0;i<shotViews.Count;i++){shotViews[i].enabled=i<run.Shots.Count;if(i<run.Shots.Count){shotViews[i].transform.position=Position(run.Shots[i].Position);shotViews[i].color=run.Shots[i].Friendly?new Color(1,.5f,.2f):new Color(.6f,.4f,1);shotViews[i].sprite=art.Effects[8,frame];shotViews[i].transform.localScale=Vector3.one*.5f;}}
            gate.sprite=art.Props[run.GateOpen?6:7];gate.color=Color.white;core.enabled=run.CoreAvailable&&!run.CoreDevoured;core.transform.position=Position(run.CorePosition);
            riftAura.enabled=run.GateOpen;riftAura.sprite=art.Effects[5,frame];
            UpdateVisualEffects(frame);
            if(run.State==Phase.Playing)for(int i=particles.Count-1;i>=0;i--){var p=particles[i];p.Life-=Time.deltaTime;p.Object.transform.position+=p.Velocity*Time.deltaTime;if(p.Life<=0){Destroy(p.Object);particles.RemoveAt(i);}}
        }
        private static int Direction(Vec v){return Math.Abs(v.X)>Math.Abs(v.Y)?v.X>=0?1:3:v.Y>=0?2:0;}
        private static int Order(Vec p){return 100+(int)(p.Y*10);}
        private SpriteRenderer Sprite(string name,Sprite art,Vec point,int order)
        {var o=new GameObject(name);o.transform.SetParent(world.transform);o.transform.position=Position(point);var r=o.AddComponent<SpriteRenderer>();r.sprite=art;r.sortingOrder=order;return r;}
        private void BuildWorld(bool ending)
        {
            if(world!=null){world.SetActive(false);Destroy(world);}world=new GameObject(ending?"Human world":"Underworld floor "+(run.Stats.Floor+1));
            enemies.Clear();cones.Clear();hazardViews.Clear();shotViews.Clear();particles.Clear();villagers.Clear();enemyShadows.Clear();lastHP.Clear();hitFlashes.Clear();visualEffects.Clear();damageTexts.Clear();actionDuration=0;
            if(ending)
            {
                var backdrop=UnityEngine.Sprite.Create(art.Ending,new Rect(0,0,art.Ending.width,art.Ending.height),new Vector2(.5f,.5f),art.Ending.width/20f,0,SpriteMeshType.FullRect);
                generated.Add(backdrop);Sprite("Illustrated human world",backdrop,new Vec(10,5.625),-100);
                for(int i=0;i<4;i++)villagers.Add(Sprite("Fleeing human",art.Enemies[4,3,0],new Vec(7+i*2,8.3),190));
                view.transform.position=new Vector3(10,-5.625f,-10);PlayTrack(3);return;
            }
            int floor=run.Stats.Floor;
            for(int y=0;y<Rules.Height;y++)for(int x=0;x<Rules.Width;x++)
            {
                Sprite("Dungeon floor tile",art.Tiles[floor,(x*17+y*31)%4],new Vec(x+.5,y+.5),-100);
                if(run.Solid(x,y))Sprite("Collision-aligned stone wall",art.Tiles[floor,y>0&&!run.Solid(x,y-1)?5:y<Rules.Height-1&&!run.Solid(x,y+1)?4:8],new Vec(x+.5,y+.5),Order(new Vec(x,y)));
            }
            DecorateDungeon(floor);
            foreach(var spot in run.HideSpots)Sprite("Marked hide crack",crackArt,spot,-20);
            foreach(var h in run.Hazards)hazardViews.Add(Sprite(h.Kind.ToString(),h.Kind==HazardType.Plate?plateArt:h.Kind==HazardType.Rock?rockArt:h.Barrel?h.Kind==HazardType.Gas?art.Props[30]:barrelArt:h.Kind==HazardType.Oil?oilArt:gasArt,h.Position,-15));
            gate=Sprite("Rift gate — F",riftArt,run.Gate,100);core=Sprite("Dungeon Core — hold E",coreArt,run.Gate,350);
            riftAura=Sprite("Active Rift glow",art.Effects[5,0],run.Gate,95);
            slimeShadow=Sprite("Slime ground shadow",oilArt,run.Player,99);slimeShadow.color=new Color(0,0,0,.3f);
            slime=Sprite("Villain slime",art.Slimes[0,1,0,0],run.Player,Order(run.Player));
            flameAura=Sprite("Flame Body aura",art.Effects[7,0],run.Player,99);hardenAura=Sprite("Harden shield",art.Effects[10,0],run.Player,300);senseAura=Sprite("Sense pulse",art.Effects[11,0],run.Player,-5);bossWarning=Sprite("Guardian danger radius",art.Effects[9,3],run.Player,-10);bossWarning.transform.localScale=Vector3.one*(1.7f/.9375f);
            foreach(var enemy in run.Enemies)
            {
                enemies[enemy.Id]=Sprite(Rules.SpeciesName(enemy.Kind),art.Enemies[(int)enemy.Kind,1,0],enemy.Position,Order(enemy.Position));
                var shadow=Sprite("Enemy ground shadow",oilArt,enemy.Position,Order(enemy.Position)-1);shadow.color=new Color(0,0,0,.27f);shadow.transform.localScale=Vector3.one*(enemy.Kind==Species.Guardian?1.1f:.65f);enemyShadows[enemy.Id]=shadow;
                lastHP[enemy.Id]=enemy.HP;hitFlashes[enemy.Id]=0;
                var cone=Sprite("Vision cone",visionArt,enemy.Position,-25);float range=enemy.Kind==Species.Guardian?7:enemy.Kind==Species.HeroMage?5.5f:4.5f;
                cone.transform.localScale=Vector3.one*range/2;cones[enemy.Id]=cone;
            }
            view.transform.position=new Vector3(10,-8,-10);PlayTrack(floor);
        }
        private void DecorateDungeon(int floor)
        {
            // Decoration is visual only: it adds no blockers or changes to the hand-built map.
            int[] accents=floor==0?new[]{10,21,22,25}:floor==1?new[]{12,13,19,20}:new[]{15,17,23,27};
            for(int room=0;room<3;room++)for(int i=0;i<4;i++)
            {
                var point=new Vec(room*16+3.5+i*2.5,i%2==0?2.5:12.5);
                if(!run.Solid((int)point.X,(int)point.Y))Sprite("Floor-themed dungeon detail",art.Props[accents[i]],point,-35);
            }
            for(int room=0;room<3;room++)
            {
                Sprite(floor==0?"Moss patch":floor==1?"Worn carpet":"Sanctuary rune",art.Tiles[floor,10],new Vec(room*16+8,9.5),-80).transform.localScale=new Vector3(5,2.5f,1);
                Sprite("Wall brazier",art.Props[16],new Vec(room*16+13.5,3.5),Order(new Vec(room*16+13.5,3.5)));
                Sprite("Scattered rubble",art.Props[14],new Vec(room*16+11.5,13.5),-35);
            }
        }
        private void SetAction(int action,float duration){slimeAction=action;actionStarted=run.Clock;actionDuration=duration;}
        private void Effect(string name,Vec point)
        {
            if(!muted&&sounds.ContainsKey(name))fx.PlayOneShot(sounds[name]);
            if(name=="bite")SetAction(SlimeArt.Bite,.22f);if(name=="slam"||name=="tackle")SetAction(SlimeArt.Slam,.28f);if(name=="hurt")SetAction(SlimeArt.Hurt,.24f);
            if(world==null || name=="bossWarn")return;
            int effect=name=="bite"?3:name=="slam"||name=="tackle"?2:name=="bossSlam"||name=="rock"?6:name=="devour"||name=="level"||name=="evolve"?4:name=="skill"?11:name=="hazard"?0:-1;
            if(effect>=0)
            {
                var renderer=Sprite("Animated combat effect",art.Effects[effect,0],point,1000);
                if(name=="bite"){renderer.transform.position+=Position(run.Facing)*.45f;renderer.transform.rotation=Quaternion.Euler(0,0,(float)(Math.Atan2(-run.Facing.Y,run.Facing.X)*180/Math.PI));}
                renderer.transform.localScale=Vector3.one*(name=="bossSlam"?1.7f:1);
                visualEffects.Add(new VisualEffect{Renderer=renderer,Kind=effect,Duration=effect==4?.6f:.4f,Start=point,Inward=name=="devour"});
            }
            int count=name=="devour"||name=="level"||name=="hazard"||name=="rock"?12:4;
            Color c=name=="hurt"?new Color(1,.3f,.3f):name=="devour"?Mint:Gold;
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;var r=Sprite("Essence spark",sparkArt,point,1000);r.color=c;particles.Add(new Particle{Object=r.gameObject,Velocity=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*1.5f,Life=.5f});}
        }
        private void UpdateVisualEffects(int frame)
        {
            flameAura.enabled=run.Stats.FireRank==3&&!run.Hidden;flameAura.sprite=art.Effects[7,frame];flameAura.transform.position=Position(run.Player);flameAura.sortingOrder=Order(run.Player)-2;
            hardenAura.enabled=run.HardenTime>0;hardenAura.sprite=art.Effects[10,frame];hardenAura.transform.position=Position(run.Player);hardenAura.sortingOrder=Order(run.Player)+2;
            senseAura.enabled=run.EchoTime>0||run.ManaTime>0;senseAura.sprite=art.Effects[11,frame];senseAura.transform.position=Position(run.Player);
            var boss=run.Enemies.Find(enemy=>enemy.Kind==Species.Guardian);
            bossWarning.enabled=boss!=null&&boss.Telegraph>0;
            if(bossWarning.enabled){bossWarning.transform.position=Position(boss.SlamPoint);bossWarning.color=new Color(1,.6f,.55f,.65f+(float)Math.Sin(run.Clock*12)*.15f);}
            if(run.State!=Phase.Playing)return;
            for(int i=visualEffects.Count-1;i>=0;i--)
            {
                var fx=visualEffects[i];fx.Time+=Time.deltaTime;
                if(fx.Time>=fx.Duration){Destroy(fx.Renderer.gameObject);visualEffects.RemoveAt(i);continue;}
                fx.Renderer.sprite=art.Effects[fx.Kind,Math.Min(3,(int)(fx.Time/fx.Duration*4))];
                if(fx.Inward)fx.Renderer.transform.position=Vector3.Lerp(Position(fx.Start),Position(run.Player),fx.Time/fx.Duration);
            }
            for(int i=damageTexts.Count-1;i>=0;i--){damageTexts[i].Time-=Time.deltaTime;if(damageTexts[i].Time<=0)damageTexts.RemoveAt(i);}
        }
        private void OnApplicationFocus(bool focused){if(!focused&&run!=null&&run.State==Phase.Playing)run.Pause();}
        private void OnApplicationPause(bool paused){if(paused&&run!=null&&run.State==Phase.Playing)run.Pause();}
        private void OnGUI()
        {
            if(run==null)return;if(title==null)Styles();var oldMatrix=GUI.matrix;var oldColor=GUI.color;float s=Scale;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*s)/2,(Screen.height-720*s)/2,0),Quaternion.identity,Vector3.one*s);
            if(loading){Overlay();Text(300,290,800,70,"Crossing the Rift…",title);GUI.matrix=oldMatrix;GUI.color=oldColor;return;}
            if(run.State!=Phase.Menu&&run.State!=Phase.Intro&&run.State!=Phase.Escaped){Box(new Rect(0,0,1280,720),new Color(0,0,0,.09f));Hud();}
            if(run.State==Phase.Menu)Menu();else if(run.State==Phase.Intro)Intro();else if(run.State==Phase.Paused)PausePanel();else if(run.State==Phase.Evolution)Evolution();else if(run.State==Phase.Dead)DeathPanel();else if(run.State==Phase.Escaped)Ending();
            GUI.matrix=oldMatrix;GUI.color=oldColor;
        }
        private void Hud()
        {
            Box(new Rect(16,14,1248,80),new Color(.04f,.07f,.09f,.92f));
            Text(32,18,400,25,"SLIME ASCENT · FLOOR "+(run.Stats.Floor+1)+" / 3",heading);
            Text(460,18,650,25,Rules.FloorNames[run.Stats.Floor],small);
            Bar(32,52,180,18,run.Stats.HP/run.MaxHP,new Color(.7f,.25f,.3f));Text(34,50,180,23,"HP  "+(int)run.Stats.HP+" / "+(int)run.MaxHP,small);
            Bar(230,52,150,18,run.Stats.XP/(double)run.NextXP,Gold);Text(232,50,150,23,"XP · LEVEL "+run.Stats.Level,small);
            Bar(398,52,180,18,run.Stats.Essence/run.MaxEssence,new Color(.4f,.4f,.85f));Text(400,50,180,23,"ESSENCE  "+(int)run.Stats.Essence,small);
            Bar(596,52,120,18,run.Stats.Biomass/run.Capacity,new Color(.2f,.6f,.45f));Text(598,50,140,23,"BIOMASS "+run.Stats.Biomass.ToString("F1"),small);
            Bar(770,52,170,18,run.Detection,new Color(.9f,.4f,.2f));Text(772,50,170,23,run.Hidden?"HIDDEN":"DETECTION",small);
            if(GUI.Button(new Rect(1000,40,115,30),"Evolve [Tab]",button))run.EvolveMenu();
            if(GUI.Button(new Rect(1130,40,115,30),"Pause [P]",button))run.Pause();
            Box(new Rect(16,620,1248,86),new Color(.04f,.07f,.09f,.93f));
            string[] skillNames={"Flame Spit · Lizard","Harden · Beetle","Echo Sense · Bat","Mana Sense · Mage"};
            for(int i=0;i<4;i++){bool known=run.SkillKnown(i+1);Box(new Rect(30+i*205,631,194,36),known?new Color(.18f,.3f,.24f):new Color(.12f,.14f,.18f));Text(40+i*205,635,185,25,(i+1)+"  "+skillNames[i],small);}
            Text(860,628,385,30,run.GateOpen?"RIFT GATE · F":"GATE SEALED · DEVOUR THE CORE",small);
            Text(30,674,1200,25,"WASD Move · Mouse aim · LMB Bite · RMB Slam · Space Tackle · E Devour · Shift Hide · F Gate",small);
            Text(24,99,1000,30,run.Message,small);
            DrawEnemyUI();
            foreach(var damage in damageTexts){var pos=UiPoint(damage.Position);float rise=(.7f-damage.Time)*45;Text(pos.x-25,pos.y-40-rise,65,24,"−"+damage.Value,heading);}
            if(run.DevourTarget>=0){
                Vector2 ring=UiPoint(run.Player);for(int i=0;i<24;i++){double angle=i*Math.PI*2/24;Box(new Rect(ring.x+(float)Math.Cos(angle)*31-2,ring.y+(float)Math.Sin(angle)*31-2,4,4),i/24.0<run.DevourTime/1.25?Mint:new Color(.2f,.3f,.25f,.8f));}
                Vec p=run.Player;Vector2 screen=UiPoint(p);Bar(screen.x-50,screen.y+30,100,8,run.DevourTime/1.25,Mint);Text(screen.x-75,screen.y+42,150,25,"DEVOURING · HOLD E",small);}
            else foreach(var e in run.Enemies)if(e.Kind!=Species.Guardian&&e.State!=Mind.Devoured&&e.HP<=e.MaxHP*.25&&Vec.Distance(run.Player,e.Position)<1.05){var p=UiPoint(run.Player);Text(p.x-100,p.y+32,220,25,"HOLD E · "+Rules.SpeciesName(e.Kind),small);break;}
            if(run.CoreAvailable){var p=UiPoint(run.CorePosition);Text(p.x-70,p.y-45,180,25,"DUNGEON CORE · HOLD E",small);}
            if(Vec.Distance(run.Player,run.Gate)<1.2){var p=UiPoint(run.Gate);Text(p.x-70,p.y-55,180,25,run.GateOpen?"PRESS F TO CROSS":"CORE REQUIRED",small);}
        }
        private void DrawEnemyUI()
        {
            foreach(var e in run.Enemies)if(e.State!=Mind.Devoured&&e.State!=Mind.Defeated)
            {
                var p=UiPoint(e.Position);if(p.x>0&&p.x<1280&&p.y>100&&p.y<610){Bar(p.x-22,p.y-27,44,4,e.HP/e.MaxHP,e.Hero?new Color(.9f,.5f,.4f):Mint);if(e.Telegraph>0){var warn=UiPoint(e.SlamPoint);Text(warn.x-110,warn.y-95,220,26,"GUARDIAN SLAM · DODGE",center);}}
                else if(run.EchoTime>0||(run.ManaTime>0&&(e.Kind==Species.HeroMage||e.Kind==Species.Guardian))){float x=Mathf.Clamp(p.x,35,1245),y=Mathf.Clamp(p.y,150,590);Text(x-30,y-10,90,25,Rules.SpeciesName(e.Kind),small);}
            }
        }
        private Vector2 UiPoint(Vec point)
        {var p=view.WorldToScreenPoint(Position(point));float s=Scale;return new Vector2((p.x-(Screen.width-1280*s)/2)/s,(Screen.height-p.y-(Screen.height-720*s)/2)/s);}
        private void Overlay(){Box(new Rect(0,0,1280,720),new Color(.025f,.04f,.07f,.86f));}
        private void Illustration(Texture2D texture)
        {GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,1280,720),texture,ScaleMode.ScaleAndCrop);}
        private void PreviewSprite(Rect rect,UnityEngine.Sprite sprite)
        {var r=sprite.textureRect;var uv=new Rect(r.x/sprite.texture.width,r.y/sprite.texture.height,r.width/sprite.texture.width,r.height/sprite.texture.height);GUI.color=Color.white;GUI.DrawTextureWithTexCoords(rect,sprite.texture,uv);}
        private void Menu()
        {
            Illustration(art.Title);Box(new Rect(45,80,610,540),new Color(.025f,.05f,.055f,.7f));
            Text(80,105,520,30,"A MONSTER’S STORY · THE UNDERWORLD",heading);
            Text(80,151,560,60,"SLIME ASCENT",title);Text(80,215,560,35,"DEVOUR THE DUNGEON",heading);
            Text(80,277,520,85,"Born as prey. Destined to become the villain.\nSteal their power. Climb the dungeon.\nFind the Rift before the heroes find you.",label);
            if(GUI.Button(new Rect(80,385,340,46),"Begin a new ascent  →",button))NewGame();
            if(saved!=null&&GUI.Button(new Rect(80,446,340,42),"Continue · Floor "+(saved.Floor+1),button))run.Continue(saved);
            Text(80,513,520,68,"Hunt. Hide. Devour. Evolve.\nThree floors separate you from the human world.",small);
            if(GUI.Button(new Rect(1050,647,180,35),muted?"Sound off":"Sound on",button))ToggleSound();
            Text(80,650,830,35,"SLIME ASCENT · OFFLINE ADVENTURE · ONE FLOOR CHECKPOINT",small);
        }
        private void Intro()
        {
            Illustration(art.Title);Box(new Rect(0,0,1280,720),new Color(.025f,.04f,.07f,.65f));Text(180,62,960,60,"Beneath the human world…",title);
            string[] names={"THE HUMAN WORLD · THE FINAL RIFT","UPPER · DUNGEON CORE GUARDIAN","MIDDLE · THE HUNTING GROUNDS","LOWER · YOU WAKE ALONE"};
            for(int i=0;i<4;i++){Box(new Rect(300,160+i*88,680,65),new Color(.10f+i*.025f,.15f+i*.02f,.17f,.95f));Text(330,175+i*88,630,34,names[i],heading);}
            Text(200,550,900,70,run.IntroTime<4?"Heroes come below for treasure. To them, you are prey.":"Devour their power. Become what they fear. Reach the Rift Gate.",center);
            if(GUI.Button(new Rect(480,638,320,42),"Wake in the dungeon · Enter",button))run.SkipIntro();
        }
        private void PausePanel()
        {
            Overlay();Text(240,170,800,60,"The hunt can wait.",title);
            if(GUI.Button(new Rect(420,290,440,48),"Resume",button))run.Pause();
            if(GUI.Button(new Rect(420,355,440,44),"Restart this floor checkpoint",button))run.RetryFloor();
            if(GUI.Button(new Rect(420,418,440,44),muted?"Enable sound":"Mute sound",button))ToggleSound();
            Text(280,505,740,65,"Progress is saved at the start of each floor.\nYou can close the game and Continue from that checkpoint.",center);
        }
        private void Evolution()
        {
            Overlay();Text(140,70,1040,65,"Choose what the villain becomes.",title);
            Text(140,139,1040,35,"Level "+run.Stats.Level+" · HP "+run.MaxHP+" · Damage "+run.Damage+" · Speed "+run.Speed.ToString("F2")+" · Biomass capacity "+run.Capacity,label);
            string[] traitNames={"Enhanced Senses — Rat","Echo Sense — Cave Bat","Fire Resistance — Fire Lizard","Harden — Armored Beetle","Mana Sense — Hero Mage"};
            Species[] kinds={Species.Rat,Species.CaveBat,Species.FireLizard,Species.ArmoredBeetle,Species.HeroMage};
            for(int i=0;i<5;i++)Text(150,203+i*45,690,35,(run.HasTrait(kinds[i])?"UNLOCKED  ":"LOCKED  ")+traitNames[i]+" · Devoured "+run.Stats.Eaten[(int)kinds[i]],label);
            string[] forms={"AWAKENING","FIRE RESISTANCE","HEAT IMMUNITY","FLAME BODY"};
            for(int form=0;form<4;form++){PreviewSprite(new Rect(935,193+form*101,82,82),art.Slimes[form,2,0,0]);Text(1020,216+form*101,160,44,forms[form],small);}
            Text(150,450,720,40,"FIRE CHAIN: Fire Resistance → Heat Immunity → Flame Body",heading);
            string next=run.Stats.FireRank<2?"Heat Immunity · 2 Lizard Devours + 8 Essence":run.Stats.FireRank==2?"Flame Body · 3 Lizard Devours + 14 Essence":"Flame Body unlocked";
            if(run.Stats.FireRank<3&&GUI.Button(new Rect(150,503,650,42),"Evolve: "+next,button))run.UpgradeFire();
            Text(150,557,1000,40,run.Message,small);
            if(GUI.Button(new Rect(150,631,300,42),"Return to the hunt · Tab",button))run.EvolveMenu();
        }
        private void DeathPanel()
        {Overlay();Text(210,200,900,70,"A body lost. A hunger remains.",title);Text(210,300,900,60,"The current floor restarts from its entry checkpoint in "+Mathf.CeilToInt((float)(3-run.DeathTime))+" seconds.\nEarlier floors and their evolution progress are kept.",label);if(GUI.Button(new Rect(420,420,440,48),"Restart floor now",button))run.RetryFloor();}
        private void Ending()
        {
            Box(new Rect(40,35,630,330),new Color(.035f,.05f,.055f,.84f));
            Text(70,53,560,112,"THE VILLAIN\nHAS CROSSED OVER.",title);
            Text(70,178,550,157,"The Dungeon Core is silent. The Rift opens.\nThe heroes flee from the creature they once hunted.\nThe question is no longer whether you can escape — but whether this world is ready.",label);
            if(GUI.Button(new Rect(940,635,290,45),"Begin another ascent  →",button))NewGame();
        }
        private void ToggleSound(){muted=!muted;PlayerPrefs.SetInt("SlimeAscent.Muted",muted?1:0);PlayerPrefs.Save();music.mute=muted;}
        private void Bar(float x,float y,float w,float h,double value,Color color){Box(new Rect(x,y,w,h),new Color(.11f,.14f,.17f));Box(new Rect(x,y,w*(float)Math.Max(0,Math.Min(1,value)),h),color);}
        private void Box(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,white);GUI.color=Color.white;}
        private static void Text(float x,float y,float w,float h,string text,GUIStyle style){GUI.Label(new Rect(x,y,w,h),text,style);}
        private GUIStyle Style(int size,Color color,TextAnchor align=TextAnchor.MiddleLeft){var st=new GUIStyle(GUI.skin.label){fontSize=size,alignment=align,wordWrap=true};st.normal.textColor=color;return st;}
        private void Styles()
        {title=Style(42,Mint);heading=Style(19,Gold);label=Style(21,Pale);small=Style(14,Pale);center=Style(21,Pale,TextAnchor.MiddleCenter);button=new GUIStyle(GUI.skin.button){fontSize=17};button.normal.background=Solid(Mint);button.normal.textColor=new Color(.05f,.12f,.08f);button.hover.background=Solid(new Color(.75f,.95f,.8f));button.hover.textColor=button.normal.textColor;button.active.background=button.hover.background;button.active.textColor=button.normal.textColor;}
        private Texture2D Solid(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();generated.Add(t);return t;}
        private Sprite Art(int w,int h,Func<int,int,Color32> pixel,float ppu=32)
        {var t=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var colors=new Color32[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)colors[y*w+x]=pixel(x,y);t.SetPixels32(colors);t.Apply();var sp=UnityEngine.Sprite.Create(t,new Rect(0,0,w,h),new Vector2(.5f,.5f),ppu);generated.Add(t);generated.Add(sp);return sp;}
        private static Color32 Clear {get{return new Color32(0,0,0,0);}}
        private void CreateArt()
        {
            art=new SlimeArt();white=Solid(Color.white);
            barrelArt=art.Props[0];oilArt=art.Props[1];gasArt=art.Props[2];crackArt=art.Props[3];plateArt=art.Props[4];rockArt=art.Props[5];riftArt=art.Props[6];coreArt=art.Props[8];sparkArt=art.Props[31];
            visionArt=Art(128,128,(x,y)=>x>=64&&Math.Pow(x-64,2)+Math.Pow(y-64,2)<4096&&Math.Abs(y-64)<(x-64)*1.73?new Color32(255,255,255,255):Clear);
        }
        private void CreateAudio()
        {
            string[] names={"bite","slam","tackle","devour","level","skill","hurt","defeat","hazard","trap","rock","bossSlam","evolve","escape","death","battle"};
            for(int i=0;i<names.Length;i++)sounds[names[i]]=Tone(names[i],new[]{180f+i*31,260f+i*37},.09f,false);
            tracks[0]=Tone("Lower music",new[]{110f,164.8f,130.8f,146.8f,110f,196f,164.8f,130.8f},.9f,true);
            tracks[1]=Tone("Middle music",new[]{146.8f,220f,174.6f,164.8f,146.8f,261.6f,220f,164.8f},.7f,true);
            tracks[2]=Tone("Upper music",new[]{98f,146.8f,116.5f,130.8f,98f,174.6f,146.8f,116.5f},1.1f,true);
            tracks[3]=Tone("Human world music",new[]{261.6f,329.6f,392f,523.2f,392f,329.6f,293.6f,261.6f},.75f,true);
        }
        private AudioClip Tone(string name,float[] notes,float seconds,bool ambient)
        {
            const int rate=22050;int count=(int)(seconds*rate);var data=new float[count*notes.Length];
            for(int i=0;i<notes.Length;i++)for(int n=0;n<count;n++){float t=n/(float)rate,envelope=Mathf.Sin(Mathf.PI*n/count);data[i*count+n]=(Mathf.Sin(2*Mathf.PI*notes[i]*t)+(ambient?Mathf.Sin(Mathf.PI*notes[i]*t)*.35f:0))*envelope*(ambient?.12f:.15f);}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);generated.Add(clip);return clip;
        }
        private void PlayTrack(int floor){music.clip=tracks[floor];music.mute=muted;music.Play();}
        private void OnDestroy()
        {
            if(instance!=this)return;instance=null;SceneManager.sceneLoaded-=SceneLoaded;
            if(run!=null){run.Effect-=Effect;run.FloorEntered-=EnterFloor;run.Escaped-=Escape;}
            if(world!=null)Destroy(world);if(art!=null)art.Dispose();foreach(var asset in generated)if(asset!=null)Destroy(asset);
        }
    }
}
