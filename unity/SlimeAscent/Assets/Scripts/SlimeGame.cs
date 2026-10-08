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
        private Sprite[,,] slimeArt=new Sprite[3,4,2],enemyArt=new Sprite[7,4,2];
        private Sprite wallArt,oilArt,gasArt,barrelArt,crackArt,plateArt,rockArt,flameArt,riftArt,coreArt,visionArt,sparkArt,houseArt;
        private Sprite[] floorArt=new Sprite[4];
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
                for(int i=0;i<villagers.Count;i++)villagers[i].transform.position=new Vector3(7+i*2+(i%2==0?-1:1)*escapeTime*1.4f,-8.3f,0);
                return;
            }
            float halfWidth=10,halfHeight=5.625f;
            Vector3 target=new Vector3(Mathf.Clamp((float)run.Player.X,halfWidth,Rules.Width-halfWidth),-Mathf.Clamp((float)run.Player.Y,halfHeight,Rules.Height-halfHeight),-10);
            view.transform.position=Vector3.Lerp(view.transform.position,target,1-Mathf.Exp(-Time.deltaTime*9));
            if(slime==null)return;
            int frame=(int)(run.Clock*6)%2,dir=Direction(run.Facing),rank=Math.Min(2,run.Stats.FireRank);
            slime.sprite=slimeArt[rank,dir,frame];slime.transform.position=Position(run.Player);slime.sortingOrder=Order(run.Player);
            slime.transform.localScale=Vector3.one*(1+Mathf.Min(.6f,(run.Stats.Level-1)*.04f));
            slime.color=run.Hidden?new Color(1,1,1,.3f):run.HardenTime>0?new Color(.65f,.8f,1):Color.white;
            slime.enabled=run.Protection<=0||(int)(run.Clock*15)%2==0;
            foreach(var enemy in run.Enemies)
            {
                var r=enemies[enemy.Id];r.gameObject.SetActive(enemy.State!=Mind.Devoured);r.transform.position=Position(enemy.Position);r.sortingOrder=Order(enemy.Position);
                r.sprite=enemyArt[(int)enemy.Kind,Direction(enemy.Facing),frame];r.color=enemy.State==Mind.Defeated?new Color(.5f,.5f,.5f,.6f):Color.white;
                if(enemy.State==Mind.Defeated)r.transform.localScale=new Vector3(1,.45f,1);
                var cone=cones[enemy.Id];cone.enabled=enemy.State!=Mind.Defeated&&enemy.State!=Mind.Devoured;cone.transform.position=Position(enemy.Position);
                cone.transform.rotation=Quaternion.Euler(0,0,(float)(Math.Atan2(-enemy.Facing.Y,enemy.Facing.X)*180/Math.PI));
                cone.color=enemy.Alert>.55?new Color(1,.25f,.22f,.13f):new Color(1,.8f,.5f,.07f);
            }
            for(int i=0;i<run.Hazards.Count;i++)
            {
                var h=run.Hazards[i];var r=hazardViews[i];
                r.sprite=h.Active?flameArt:h.Kind==HazardType.Oil?h.Barrel&&!h.Triggered?barrelArt:oilArt:h.Kind==HazardType.Gas?h.Barrel&&!h.Triggered?barrelArt:gasArt:h.Kind==HazardType.Plate?plateArt:rockArt;
                r.color=h.Kind==HazardType.Gas&&h.Active?new Color(.4f,1,.5f,.8f):h.RockTimer>=0?new Color(1,.35f,.3f):Color.white;
                if(h.Kind==HazardType.Rock)r.enabled=h.RockTimer>=0||h.Triggered;
            }
            while(shotViews.Count<run.Shots.Count)shotViews.Add(Sprite("Projectile",coreArt,run.Player,400));
            for(int i=0;i<shotViews.Count;i++){shotViews[i].enabled=i<run.Shots.Count;if(i<run.Shots.Count){shotViews[i].transform.position=Position(run.Shots[i].Position);shotViews[i].color=run.Shots[i].Friendly?new Color(1,.5f,.2f):new Color(.6f,.4f,1);shotViews[i].transform.localScale=Vector3.one*.4f;}}
            gate.color=run.GateOpen?Mint:new Color(.45f,.3f,.55f);core.enabled=run.CoreAvailable&&!run.CoreDevoured;core.transform.position=Position(run.CorePosition);
            if(run.State==Phase.Playing)for(int i=particles.Count-1;i>=0;i--){var p=particles[i];p.Life-=Time.deltaTime;p.Object.transform.position+=p.Velocity*Time.deltaTime;if(p.Life<=0){Destroy(p.Object);particles.RemoveAt(i);}}
        }
        private static int Direction(Vec v){return Math.Abs(v.X)>Math.Abs(v.Y)?v.X>=0?1:3:v.Y>=0?2:0;}
        private static int Order(Vec p){return 100+(int)(p.Y*10);}
        private SpriteRenderer Sprite(string name,Sprite art,Vec point,int order)
        {var o=new GameObject(name);o.transform.SetParent(world.transform);o.transform.position=Position(point);var r=o.AddComponent<SpriteRenderer>();r.sprite=art;r.sortingOrder=order;return r;}
        private void BuildWorld(bool ending)
        {
            if(world!=null){world.SetActive(false);Destroy(world);}world=new GameObject(ending?"Human world":"Underworld floor "+(run.Stats.Floor+1));
            enemies.Clear();cones.Clear();hazardViews.Clear();shotViews.Clear();particles.Clear();villagers.Clear();
            if(ending)
            {
                Sprite("Human-world tiles",floorArt[3],new Vec(10,5.625),-100).transform.localScale=new Vector3(20f/48,11.25f/16,1);
                Sprite("Cottage",houseArt,new Vec(5,5),50).transform.localScale=Vector3.one*2;
                Sprite("Cottage",houseArt,new Vec(15,4),50).transform.localScale=Vector3.one*2;
                Sprite("Evolved villain slime",slimeArt[2,2,0],new Vec(10,8),200).transform.localScale=Vector3.one*3;
                for(int i=0;i<4;i++)villagers.Add(Sprite("Fleeing human",enemyArt[4,3,0],new Vec(7+i*2,8.3),190));
                view.transform.position=new Vector3(10,-5.625f,-10);PlayTrack(3);return;
            }
            Sprite("Dungeon tiles",floorArt[run.Stats.Floor],new Vec(24,8),-100);
            for(int y=0;y<Rules.Height;y++)for(int x=0;x<Rules.Width;x++)if(run.Solid(x,y))Sprite("Wall",wallArt,new Vec(x+.5,y+.5),Order(new Vec(x,y)));
            foreach(var spot in run.HideSpots)Sprite("Marked hide crack",crackArt,spot,-20);
            foreach(var h in run.Hazards)hazardViews.Add(Sprite(h.Kind.ToString(),h.Kind==HazardType.Plate?plateArt:h.Kind==HazardType.Rock?rockArt:h.Barrel?barrelArt:h.Kind==HazardType.Oil?oilArt:gasArt,h.Position,-15));
            gate=Sprite("Rift gate — F",riftArt,run.Gate,100);core=Sprite("Dungeon Core — hold E",coreArt,run.Gate,350);
            slime=Sprite("Villain slime",slimeArt[0,1,0],run.Player,Order(run.Player));
            foreach(var enemy in run.Enemies)
            {
                enemies[enemy.Id]=Sprite(Rules.SpeciesName(enemy.Kind),enemyArt[(int)enemy.Kind,1,0],enemy.Position,Order(enemy.Position));
                var cone=Sprite("Vision cone",visionArt,enemy.Position,-25);float range=enemy.Kind==Species.Guardian?7:enemy.Kind==Species.HeroMage?5.5f:4.5f;
                cone.transform.localScale=Vector3.one*range/2;cones[enemy.Id]=cone;
            }
            view.transform.position=new Vector3(10,-8,-10);PlayTrack(run.Stats.Floor);
        }
        private void Effect(string name,Vec point)
        {
            if(!muted&&sounds.ContainsKey(name))fx.PlayOneShot(sounds[name]);
            if(world==null || name=="bossWarn")return;
            int count=name=="devour"||name=="level"||name=="hazard"||name=="rock"?12:4;
            Color c=name=="hurt"?new Color(1,.3f,.3f):name=="devour"?Mint:Gold;
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;var r=Sprite("Essence spark",sparkArt,point,1000);r.color=c;particles.Add(new Particle{Object=r.gameObject,Velocity=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*1.5f,Life=.5f});}
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
                var p=UiPoint(e.Position);if(p.x>0&&p.x<1280&&p.y>100&&p.y<610){Bar(p.x-22,p.y-27,44,4,e.HP/e.MaxHP,e.Hero?new Color(.9f,.5f,.4f):Mint);if(e.Telegraph>0){var warn=UiPoint(e.SlamPoint);Box(new Rect(warn.x-55,warn.y-55,110,110),new Color(1,.2f,.1f,.23f));Text(warn.x-60,warn.y-75,150,22,"GUARDIAN SLAM · DODGE",small);}}
                else if(run.EchoTime>0||(run.ManaTime>0&&(e.Kind==Species.HeroMage||e.Kind==Species.Guardian))){float x=Mathf.Clamp(p.x,35,1245),y=Mathf.Clamp(p.y,150,590);Text(x-30,y-10,90,25,Rules.SpeciesName(e.Kind),small);}
            }
        }
        private Vector2 UiPoint(Vec point)
        {var p=view.WorldToScreenPoint(Position(point));float s=Scale;return new Vector2((p.x-(Screen.width-1280*s)/2)/s,(Screen.height-p.y-(Screen.height-720*s)/2)/s);}
        private void Overlay(){Box(new Rect(0,0,1280,720),new Color(.025f,.04f,.07f,.86f));}
        private void Menu()
        {
            Overlay();Text(170,95,940,60,"SLIME ASCENT",title);Text(170,158,940,35,"DEVOUR THE DUNGEON",heading);
            Text(170,218,940,105,"You are the villain of someone else’s story.\nWake as a fragile slime. Steal power from your prey.\nClimb the Underworld and release its monster into the human world.",label);
            if(GUI.Button(new Rect(170,358,320,48),"Begin a new ascent",button))NewGame();
            if(saved!=null&&GUI.Button(new Rect(170,424,320,42),"Continue · Floor "+(saved.Floor+1),button))run.Continue(saved);
            Text(170,500,900,95,"Hunt small creatures first. Hold E over weakened or defeated prey.\nGreen cracks are hide spots: hold Shift while nearby.\nSlam barrels or use Flame Spit to spread hazards.",small);
            if(GUI.Button(new Rect(970,640,180,35),muted?"Sound off":"Sound on",button))ToggleSound();
            Text(170,635,730,40,"OFFLINE · THREE FLOORS · ONE FLOOR CHECKPOINT",small);
        }
        private void Intro()
        {
            Overlay();Text(180,62,960,60,"Beneath the human world…",title);
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
            for(int i=0;i<5;i++)Text(150,203+i*45,960,35,(run.HasTrait(kinds[i])?"UNLOCKED  ":"LOCKED  ")+traitNames[i]+" · Devoured "+run.Stats.Eaten[(int)kinds[i]],label);
            Text(150,450,950,40,"FIRE CHAIN: Fire Resistance → Heat Immunity → Flame Body",heading);
            string next=run.Stats.FireRank<2?"Heat Immunity · 2 Lizard Devours + 8 Essence":run.Stats.FireRank==2?"Flame Body · 3 Lizard Devours + 14 Essence":"Flame Body unlocked";
            if(run.Stats.FireRank<3&&GUI.Button(new Rect(150,503,650,42),"Evolve: "+next,button))run.UpgradeFire();
            Text(150,557,1000,40,run.Message,small);
            if(GUI.Button(new Rect(150,631,300,42),"Return to the hunt · Tab",button))run.EvolveMenu();
        }
        private void DeathPanel()
        {Overlay();Text(210,200,900,70,"A body lost. A hunger remains.",title);Text(210,300,900,60,"The current floor restarts from its entry checkpoint in "+Mathf.CeilToInt((float)(3-run.DeathTime))+" seconds.\nEarlier floors and their evolution progress are kept.",label);if(GUI.Button(new Rect(420,420,440,48),"Restart floor now",button))run.RetryFloor();}
        private void Ending()
        {
            Box(new Rect(0,0,1280,270),new Color(.03f,.05f,.05f,.85f));Text(110,28,1060,64,"THE VILLAIN HAS CROSSED OVER.",title);
            Text(110,111,1060,95,"The Dungeon Core is silent. The Rift opens.\nHeroes flee from the creature they once hunted.\nThe question is no longer whether you can escape — but whether this world is ready.",label);
            if(GUI.Button(new Rect(970,635,260,45),"Begin another ascent",button))NewGame();
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
        private static bool In(int x,int y,int a,int b,int w,int h){return x>=a&&y>=b&&x<a+w&&y<b+h;}
        private void CreateArt()
        {
            white=Solid(Color.white);
            for(int rank=0;rank<3;rank++)for(int dir=0;dir<4;dir++)for(int frame=0;frame<2;frame++)
            {
                int r=rank,d=dir,f=frame;
                slimeArt[r,d,f]=Art(32,28,(x,y)=>{
                    double ellipse=Math.Pow((x-16)/(13.0+f),2)+Math.Pow((y-12)/(9.0-f),2);
                    if(r==2&&((x==7||x==24)&&y>20&&y<27))return new Color32(239,205,140,255);
                    if(ellipse>1)return Clear;
                    if((d==1&&(In(x,y,22,13,3,3)||In(x,y,26,13,2,3))) || (d==2&&(In(x,y,11,8,3,3)||In(x,y,20,8,3,3))) || (d==3&&(In(x,y,5,13,2,3)||In(x,y,9,13,3,3))))return new Color32(17,46,31,255);
                    if(In(x,y,10,17,6,2))return new Color32(191,245,187,255);
                    return r==0?new Color32(106,199,129,255):r==1?new Color32(173,192,116,255):new Color32(227,156,87,255);
                });
            }
            Color32[] palette={new Color32(154,137,113,255),new Color32(144,109,175,255),new Color32(201,113,75,255),new Color32(103,150,152,255),new Color32(170,185,198,255),new Color32(151,118,211,255),new Color32(130,143,179,255)};
            for(int kind=0;kind<7;kind++)for(int dir=0;dir<4;dir++)for(int frame=0;frame<2;frame++)
            {int k=kind,d=dir,f=frame;enemyArt[k,d,f]=Art(k==6?44:28,k==6?44:28,(x,y)=>{
                int size=k==6?44:28,c=size/2;
                if(k==1&&In(x,y,1,10+f,26,6))return palette[k];
                if(!In(x,y,c-8,c-9,16,19)&&!(k==6&&In(x,y,4,9,36,24)))return Clear;
                if((d==1&&In(x,y,c+4,c+2,3,3)) || (d==3&&In(x,y,c-7,c+2,3,3)) || (d==2&&(In(x,y,c-5,c-5,3,3)||In(x,y,c+3,c-5,3,3))))return new Color32(242,214,143,255);
                if(y<c-4&&(x+y+f)%5<2)return new Color32(45,43,56,255);
                return palette[k];});}
            wallArt=Art(32,32,(x,y)=>(y%10==0||x%16==0)?new Color32(28,35,48,255):new Color32((byte)(62+(x+y)%7),65,78,255));
            oilArt=Art(32,32,(x,y)=>Math.Pow((x-16)/15.0,2)+Math.Pow((y-16)/10.0,2)<1?new Color32(26,27,35,230):Clear);
            gasArt=Art(32,32,(x,y)=>Math.Pow((x-16)/14.0,2)+Math.Pow((y-16)/11.0,2)<1?new Color32(101,159,82,155):Clear);
            barrelArt=Art(26,30,(x,y)=>In(x,y,4,2,18,25)?(y%9<2?new Color32(148,142,134,255):new Color32(139,97,58,255)):Clear);
            crackArt=Art(32,32,(x,y)=>In(x,y,2,8,28,16)?(Math.Abs(x-16)<2?new Color32(138,219,151,255):new Color32(23,54,44,255)):Clear);
            plateArt=Art(32,32,(x,y)=>In(x,y,4,4,24,24)?(x<8||y<8?new Color32(226,179,78,255):new Color32(112,93,65,255)):Clear);
            rockArt=Art(32,32,(x,y)=>(x+y)%5==0?new Color32(182,91,87,160):new Color32(122,111,110,110));
            flameArt=Art(32,38,(x,y)=>y<30&&Math.Abs(x-16)<(30-y)*.45?(y<14?new Color32(248,205,89,230):new Color32(232,111,62,210)):Clear);
            riftArt=Art(48,64,(x,y)=>Math.Abs(x-24)<20&&y>3&&y<61?(x<10||x>38||y<9||y>54?new Color32(135,140,160,255):new Color32(167,127,211,155)):Clear);
            coreArt=Art(20,24,(x,y)=>Math.Abs(x-10)+Math.Abs(y-12)*.6<9?new Color32(225,199,248,255):Clear);
            visionArt=Art(128,128,(x,y)=>x>=64&&Math.Pow(x-64,2)+Math.Pow(y-64,2)<4096&&Math.Abs(y-64)<(x-64)*1.73?new Color32(255,255,255,255):Clear);
            sparkArt=Art(3,3,(x,y)=>new Color32(255,255,255,255));
            houseArt=Art(80,90,(x,y)=>In(x,y,10,0,60,45)?new Color32(185,160,122,255):y>=45&&y<84&&Math.Abs(x-40)<(84-y)?new Color32(171,93,78,255):Clear);
            for(int floor=0;floor<4;floor++){int n=floor;floorArt[n]=Art(768,256,(x,y)=>{
                if(n==3)return(x+y)%31==0?new Color32(126,165,107,255):new Color32(92,137,98,255);
                int tileX=x/16,tileY=y/16;bool border=x%16==0||y%16==0;int seed=(tileX*31+tileY*17)%11;
                return border?new Color32(29,32,43,255):new Color32((byte)(44+n*7+seed),(byte)(46+n*3+seed),(byte)(56+n*8+seed),255);
            },16);}
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
            if(world!=null)Destroy(world);foreach(var asset in generated)if(asset!=null)Destroy(asset);
        }
    }
}
