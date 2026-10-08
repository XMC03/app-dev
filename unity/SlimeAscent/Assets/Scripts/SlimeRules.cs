using System;
using System.Collections.Generic;

namespace SlimeAscent
{
    public struct Vec
    {
        public double X, Y;
        public Vec(double x, double y) { X=x; Y=y; }
        public double Length { get { return Math.Sqrt(X*X+Y*Y); } }
        public Vec Unit { get { double n=Length; return n<.001?new Vec(1,0):new Vec(X/n,Y/n); } }
        public static Vec operator +(Vec a, Vec b) { return new Vec(a.X+b.X,a.Y+b.Y); }
        public static Vec operator -(Vec a, Vec b) { return new Vec(a.X-b.X,a.Y-b.Y); }
        public static Vec operator *(Vec a, double n) { return new Vec(a.X*n,a.Y*n); }
        public static double Distance(Vec a, Vec b) { return (a-b).Length; }
        public static double Dot(Vec a, Vec b) { return a.X*b.X+a.Y*b.Y; }
    }
    public enum Phase { Menu, Intro, Playing, Paused, Evolution, Dead, Escaped }
    public enum Species { Rat, CaveBat, FireLizard, ArmoredBeetle, HeroWarrior, HeroMage, Guardian }
    public enum Mind { Patrol, Notice, Chase, Flee, Defeated, Devoured }
    public enum HazardType { Oil, Gas, Plate, Rock }
    [Serializable]
    public sealed class Progress
    {
        public int Floor, Level=1, XP, FireRank;
        public double HP=36, Essence, Biomass;
        public int[] Eaten=new int[7];
        public Progress Copy() { return new Progress { Floor=Floor,Level=Level,XP=XP,FireRank=FireRank,HP=HP,Essence=Essence,Biomass=Biomass,Eaten=(int[])Eaten.Clone() }; }
        public bool Valid() { return Floor>=0 && Floor<3 && Level>=1 && Level<=40 && XP>=0 && XP<100000 && Eaten!=null && Eaten.Length==7 && FireRank>=0 && FireRank<=3 && HP>0 && HP<=36+(Level-1)*12 && !double.IsNaN(HP) && !double.IsInfinity(HP) && Essence>=0 && Essence<=100 && Biomass>=0 && Biomass<=100 && Array.TrueForAll(Eaten,n=>n>=0 && n<=10000); }
    }
    public sealed class Enemy
    {
        public int Id;
        public Species Kind;
        public Vec Position, Home, Facing=new Vec(1,0);
        public double HP, MaxHP, Alert, Cooldown, PatrolTime, PathTimer;
        public Mind State;
        public readonly Queue<Vec> Route=new Queue<Vec>();
        public double Telegraph;
        public Vec SlamPoint;
        public bool Hero { get { return Kind==Species.HeroWarrior || Kind==Species.HeroMage; } }
        public Enemy(int id,Species kind,Vec position) { Id=id;Kind=kind;Home=Position=position;HP=MaxHP=Rules.Health(kind); }
    }
    public sealed class Hazard
    {
        public HazardType Kind;
        public Vec Position;
        public bool Barrel, Triggered;
        public double Age, SpreadTimer, RockTimer=-1;
        public Hazard(HazardType kind,double x,double y,bool barrel=false) { Kind=kind;Position=new Vec(x,y);Barrel=barrel; }
        public bool Active { get { return Triggered && (Kind==HazardType.Oil||Kind==HazardType.Gas) && Age<5; } }
    }
    public sealed class Shot
    {
        public Vec Position, Direction;
        public double Life=2, Damage;
        public bool Friendly, Fire;
    }
    public struct Controls
    {
        public int X,Y,Skill;
        public Vec Aim;
        public bool Bite,Slam,Tackle,Devour,Hide,Gate;
    }
    public sealed class Rules
    {
        public const int Width=48, Height=16;
        public static readonly string[] FloorNames={"Lower — The forgotten nest","Middle — The hunting grounds","Upper — The Core sanctuary"};
        public Phase State { get; private set; }=Phase.Menu;
        public Progress Stats { get; private set; }=new Progress();
        public Progress Checkpoint { get; private set; }
        public Vec Player, Facing=new Vec(1,0), Gate=new Vec(44.5,7.5);
        public readonly List<Enemy> Enemies=new List<Enemy>();
        public readonly List<Hazard> Hazards=new List<Hazard>();
        public readonly List<Vec> HideSpots=new List<Vec>();
        public readonly List<Shot> Shots=new List<Shot>();
        public double Clock, IntroTime, DeathTime, BiteCooldown, SlamCooldown, TackleCooldown, Protection, HardenTime, EchoTime, ManaTime, DevourTime;
        public bool Hidden, CoreAvailable, CoreDevoured;
        public Vec CorePosition;
        public int DevourTarget { get; private set; }=-1;
        public int FloorDevours { get; private set; }
        public double Detection { get; private set; }
        public string Message { get; private set; }="A fragile slime wakes beneath the human world.";
        private double dashTime, fireBodyTick;
        private Vec dashDirection;
        private readonly double[] skillCooldown=new double[4];
        private readonly HashSet<int> walls=new HashSet<int>();
        public event Action<string,Vec> Effect;
        public event Action<int> FloorEntered;
        public event Action Escaped;
        public double MaxHP { get { return 36+(Stats.Level-1)*12; } }
        public double Damage { get { return 7+(Stats.Level-1)*2; } }
        public double Speed { get { return 3.2+Math.Min(2,(Stats.Level-1)*.12); } }
        public double Capacity { get { return 6+(Stats.Level-1)*2; } }
        public double MaxEssence { get { return 40+Math.Min(40,(Stats.Level-1)*4); } }
        public int NextXP { get { return 30+(Stats.Level-1)*20; } }
        public bool GateOpen { get { return Stats.Floor<2 || CoreDevoured; } }
        public bool HasTrait(Species kind) { return Stats.Eaten[(int)kind]>0; }
        public Rules() { Layout(); }
        public static double Health(Species kind) { return new double[]{15,20,32,46,70,46,240}[(int)kind]; }
        public static int Reward(Species kind) { return new[]{12,15,22,24,38,36,100}[(int)kind]; }
        public static int Mass(Species kind) { return new[]{1,1,2,3,4,3,4}[(int)kind]; }
        private static double Clamp(double x,double a,double b) { return Math.Max(a,Math.Min(b,x)); }
        private void Emit(string name,Vec p) { if(Effect!=null)Effect(name,p); }
        private void Say(string text) { Message=text; }
        public void NewRun() { Stats=new Progress();Checkpoint=null;State=Phase.Intro;IntroTime=0; }
        public void SkipIntro() { if(State==Phase.Intro)EnterFloor(0,true); }
        public bool Continue(Progress saved)
        {
            if(saved==null || !saved.Valid())return false;
            Stats=saved.Copy(); EnterFloor(Stats.Floor,true);return true;
        }
        private void EnterFloor(int floor,bool checkpoint)
        {
            Stats.Floor=floor;Stats.HP=MaxHP;Stats.Biomass=0;
            if(checkpoint)Checkpoint=Stats.Copy();
            Layout(); State=Phase.Playing;
            if(FloorEntered!=null)FloorEntered(floor);
        }
        public void RetryFloor() { if(Checkpoint==null)return;Stats=Checkpoint.Copy();EnterFloor(Stats.Floor,false); }
        public void Pause() { if(State==Phase.Playing) {State=Phase.Paused;CancelDevour();} else if(State==Phase.Paused)State=Phase.Playing; }
        public void EvolveMenu() { if(State==Phase.Playing) {State=Phase.Evolution;CancelDevour();} else if(State==Phase.Evolution)State=Phase.Playing; }
        public bool UpgradeFire()
        {
            int next=Stats.FireRank+1;
            if(next>3 || next<2) {Say("Devour a Fire Lizard to gain Fire Resistance first.");return false;}
            int cost=next==2?8:14;
            if(Stats.Eaten[(int)Species.FireLizard]<next || Stats.Essence<cost) {Say("Need "+next+" Fire Lizard Devours and "+cost+" Essence.");return false;}
            Stats.Essence-=cost;Stats.FireRank=next;Say(next==2?"Heat Immunity evolved. Fire cannot hurt you.":"Flame Body evolved. Your touch burns nearby prey.");Emit("evolve",Player);return true;
        }
        private void Layout()
        {
            walls.Clear();Enemies.Clear();Hazards.Clear();HideSpots.Clear();Shots.Clear();
            Player=new Vec(3.5,7.5);Facing=new Vec(1,0);Clock=DeathTime=0;Detection=0;Hidden=false;CoreAvailable=CoreDevoured=false;
            BiteCooldown=SlamCooldown=TackleCooldown=Protection=HardenTime=EchoTime=ManaTime=DevourTime=dashTime=fireBodyTick=0;DevourTarget=-1;FloorDevours=0;
            Array.Clear(skillCooldown,0,4);
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
                if(x==0||y==0||x==Width-1||y==Height-1||((x==15||x==16||x==31||x==32)&&(y<6||y>8)))walls.Add(y*Width+x);
            int[,] pillars={{6,4},{6,11},{12,3},{20,3},{20,11},{28,11},{36,3},{39,11},{43,3}};
            for(int i=0;i<pillars.GetLength(0);i++)walls.Add(pillars[i,1]*Width+pillars[i,0]);
            HideSpots.AddRange(new[]{new Vec(2.5,3.5),new Vec(13.5,12.5),new Vec(18.5,2.5),new Vec(29.5,12.5),new Vec(34.5,2.5),new Vec(45.5,12.5)});
            Add(Species.Rat,6.5,7.5);Add(Species.Rat,10.5,10.5);Add(Species.CaveBat,11.5,5.5);
            Add(Species.FireLizard,22.5,5.5);Add(Species.ArmoredBeetle,26.5,10.5);
            Add(Species.HeroWarrior,39.5,7.5);Add(Species.HeroMage,41.5,8.5);
            if(Stats.Floor>=1){Add(Species.FireLizard,27.5,4.5);Add(Species.HeroWarrior,23.5,8.5);Add(Species.CaveBat,37.5,5.5);}
            if(Stats.Floor==2){Add(Species.FireLizard,10.5,4.5);Add(Species.Guardian,44.5,7.5);}
            for(int i=0;i<3;i++) {Hazards.Add(new Hazard(HazardType.Oil,9.5+i,7.5,i==0||i==2));Hazards.Add(new Hazard(HazardType.Gas,24.5+i,7.5,i==0));}
            Hazards.Add(new Hazard(HazardType.Plate,37.5,9.5,true));Hazards.Add(new Hazard(HazardType.Rock,39.5,9.5));Hazards.Add(new Hazard(HazardType.Rock,40.5,9.5));
            Say("Hunt small prey first. Hold E to Devour. Rift gate: northeast room.");
        }
        private void Add(Species kind,double x,double y) {Enemies.Add(new Enemy(Enemies.Count,kind,new Vec(x,y)));}
        public bool Solid(int x,int y) {return x<0||y<0||x>=Width||y>=Height||walls.Contains(y*Width+x);}
        private bool Free(Vec p,double radius)
        {
            for(int y=(int)Math.Floor(p.Y-radius);y<=(int)Math.Floor(p.Y+radius);y++)for(int x=(int)Math.Floor(p.X-radius);x<=(int)Math.Floor(p.X+radius);x++)if(Solid(x,y))
            {double cx=Clamp(p.X,x,x+1),cy=Clamp(p.Y,y,y+1);if(Vec.Distance(p,new Vec(cx,cy))<radius)return false;}
            return true;
        }
        public Vec Move(Vec p,Vec delta,double radius=.26)
        {
            int steps=Math.Max(1,(int)Math.Ceiling(delta.Length/.12));Vec step=delta*(1.0/steps);
            for(int i=0;i<steps;i++){Vec n=new Vec(p.X+step.X,p.Y);if(Free(n,radius))p=n;n=new Vec(p.X,p.Y+step.Y);if(Free(n,radius))p=n;}
            return p;
        }
        private bool SafeSegment(Vec a,Vec b)
        {int steps=Math.Max(1,(int)Math.Ceiling(Vec.Distance(a,b)/.12));for(int i=1;i<=steps;i++)if(!Free(a+(b-a)*(i/(double)steps),.26))return false;return true;}
        public bool Sight(Vec a,Vec b)
        {
            double length=Vec.Distance(a,b);int steps=Math.Max(1,(int)Math.Ceiling(length/.15));
            for(int i=1;i<steps;i++){Vec p=a+(b-a)*(i/(double)steps);if(Solid((int)p.X,(int)p.Y))return false;}return true;
        }
        public List<Vec> FindPath(Vec from,Vec target)
        {
            int start=(int)from.Y*Width+(int)from.X,goal=(int)target.Y*Width+(int)target.X;
            var result=new List<Vec>();if(Solid(goal%Width,goal/Width))return result;
            if(start==goal){result.Add(target);return result;}
            var previous=new Dictionary<int,int>{{start,-1}};var queue=new Queue<int>();queue.Enqueue(start);
            while(queue.Count>0 && !previous.ContainsKey(goal))
            {int id=queue.Dequeue(),x=id%Width,y=id/Width;int[,] n={{x+1,y},{x-1,y},{x,y+1},{x,y-1}};
                for(int i=0;i<4;i++){int nx=n[i,0],ny=n[i,1],next=ny*Width+nx;if(!Solid(nx,ny)&&!previous.ContainsKey(next)){previous[next]=id;queue.Enqueue(next);}}}
            if(!previous.ContainsKey(goal))return result;
            for(int id=goal;id!=start;id=previous[id])result.Add(new Vec(id%Width+.5,id/Width+.5));result.Reverse();
            Vec center=new Vec(start%Width+.5,start/Width+.5);if(Vec.Distance(from,center)>.05)result.Insert(0,center);return result;
        }
        private void CancelDevour() {DevourTarget=-1;DevourTime=0;}
        private void HurtPlayer(double damage,bool fire=false)
        {
            if(State!=Phase.Playing||Protection>0||Hidden)return;
            if(fire && Stats.FireRank>=2)return;
            if(fire && Stats.FireRank==1)damage*=.65;
            if(HardenTime>0)damage*=.35;
            Stats.HP=Math.Max(0,Stats.HP-damage);Protection=.45;CancelDevour();Emit("hurt",Player);
            if(Stats.HP<=0){State=Phase.Dead;DeathTime=0;Hidden=false;Say("Your body fades. The current floor will restart from its checkpoint.");Emit("death",Player);}
        }
        public void Hit(Enemy enemy,double damage,bool alertPlayer=true)
        {
            if(enemy.State==Mind.Defeated||enemy.State==Mind.Devoured)return;
            enemy.HP=Math.Max(0,enemy.HP-damage);
            if(enemy.HP<=0){enemy.State=Mind.Defeated;enemy.Route.Clear();Emit("defeat",enemy.Position);if(enemy.Kind==Species.Guardian){CoreAvailable=true;CorePosition=enemy.Position;Say("The Guardian fell! Hold E near the Dungeon Core.");}}
            else {if(alertPlayer)enemy.Alert=1;enemy.State=Mind.Chase;}
        }
        private void Melee(double range,double multiplier,bool knock)
        {
            foreach(var enemy in Enemies)if(enemy.State!=Mind.Defeated && enemy.State!=Mind.Devoured && Vec.Distance(Player,enemy.Position)<=range && Vec.Dot((enemy.Position-Player).Unit,Facing)>.15 && Sight(Player,enemy.Position))
            {Hit(enemy,Damage*multiplier);if(knock&&enemy.Kind!=Species.Guardian)enemy.Position=Move(enemy.Position,Facing*.65);}
        }
        private void Trigger(Hazard hazard)
        {
            if(hazard.Triggered)return;hazard.Triggered=true;hazard.Age=0;hazard.SpreadTimer=.35;
            if(hazard.Kind==HazardType.Plate){foreach(var rock in Hazards)if(rock.Kind==HazardType.Rock)rock.RockTimer=.85;Emit("trap",hazard.Position);Say("Pressure plate triggered! Leave the marked falling-rock tiles.");}
            else if(hazard.Kind==HazardType.Oil||hazard.Kind==HazardType.Gas)Emit("hazard",hazard.Position);
        }
        private void Smash()
        {foreach(var hazard in Hazards)if(hazard.Barrel && Vec.Distance(Player,hazard.Position)<1.8 && Vec.Dot((hazard.Position-Player).Unit,Facing)>.1)Trigger(hazard);}
        private void Devour(bool holding,double dt)
        {
            if(!holding||Hidden){CancelDevour();return;}
            int target=-1;double nearest=1.05;
            if(CoreAvailable&&!CoreDevoured&&Vec.Distance(Player,CorePosition)<nearest){target=999;nearest=Vec.Distance(Player,CorePosition);}
            foreach(var enemy in Enemies)if(enemy.Kind!=Species.Guardian && enemy.State!=Mind.Devoured && enemy.HP<=enemy.MaxHP*.25)
            {double d=Vec.Distance(Player,enemy.Position);if(d<nearest){target=enemy.Id;nearest=d;}}
            if(target<0){CancelDevour();return;}
            int mass=target==999?4:Mass(Enemies[target].Kind);
            if(Stats.Biomass+mass>Capacity){CancelDevour();Say("Biomass capacity full. Give digestion a moment.");return;}
            if(target!=DevourTarget){DevourTime=0;DevourTarget=target;}
            DevourTime+=dt;
            if(DevourTime<1.25)return;
            Species kind=target==999?Species.Guardian:Enemies[target].Kind;
            if(target==999){CoreDevoured=true;CoreAvailable=false;Say("The Core is yours. Press F at the final Rift Gate to escape.");}
            else {Enemies[target].State=Mind.Devoured;Stats.Eaten[(int)kind]++;Say("Devoured "+SpeciesName(kind)+". Power becomes yours.");}
            Stats.Biomass+=mass;Stats.Essence=Math.Min(MaxEssence,Stats.Essence+10+mass*2);Stats.XP+=Reward(kind);FloorDevours++;
            if(kind==Species.FireLizard && Stats.FireRank==0)Stats.FireRank=1;
            while(Stats.XP>=NextXP){Stats.XP-=NextXP;Stats.Level++;Stats.HP=Math.Min(MaxHP,Stats.HP+12);Emit("level",Player);}
            Stats.HP=Math.Min(MaxHP,Stats.HP+5);Emit("devour",Player);CancelDevour();
        }
        public static string SpeciesName(Species kind) {return new[]{"Rat","Cave Bat","Fire Lizard","Armored Beetle","Hero Warrior","Hero Mage","Dungeon Guardian"}[(int)kind];}
        public bool SkillKnown(int slot) {return slot==1?HasTrait(Species.FireLizard):slot==2?HasTrait(Species.ArmoredBeetle):slot==3?HasTrait(Species.CaveBat):slot==4&&HasTrait(Species.HeroMage);}
        public double SkillCost(int slot) {return slot==1?6:slot==2?8:5;}
        private void Skill(int slot)
        {
            if(slot<1||slot>4)return;
            if(!SkillKnown(slot)){Say("Devour the matching species to learn this skill.");return;}
            double cost=SkillCost(slot);
            if(skillCooldown[slot-1]>0){Say("Skill is recovering.");return;}
            if(Stats.Essence<cost){Say("Not enough Essence. Hunt and Devour more prey.");return;}
            Stats.Essence-=cost;skillCooldown[slot-1]=slot==1?.6:slot==2?7:8;
            if(slot==1)Shots.Add(new Shot{Position=Player+Facing*.4,Direction=Facing,Friendly=true,Fire=true,Damage=Damage*2,Life=1.5});
            if(slot==2)HardenTime=4;
            if(slot==3)EchoTime=6;
            if(slot==4)ManaTime=6;
            Emit("skill",Player);
        }
        private void TickShots(double dt)
        {
            for(int i=Shots.Count-1;i>=0;i--)
            {
                var shot=Shots[i];shot.Life-=dt;shot.Position+=shot.Direction*(dt*(shot.Friendly?8:5));
                if(Solid((int)shot.Position.X,(int)shot.Position.Y))shot.Life=0;
                if(shot.Fire)foreach(var hazard in Hazards)if((hazard.Kind==HazardType.Oil||hazard.Kind==HazardType.Gas)&&Vec.Distance(shot.Position,hazard.Position)<.6){Trigger(hazard);shot.Life=0;}
                if(shot.Friendly){foreach(var enemy in Enemies)if(enemy.State!=Mind.Defeated&&enemy.State!=Mind.Devoured && Vec.Distance(shot.Position,enemy.Position)<(enemy.Kind==Species.Guardian?.75:.4)){Hit(enemy,shot.Damage);shot.Life=0;break;}}
                else if(Vec.Distance(shot.Position,Player)<.35){HurtPlayer(shot.Damage);shot.Life=0;}
                if(shot.Life<=0)Shots.RemoveAt(i);
            }
        }
        private void TickHazards(double dt)
        {
            foreach(var hazard in Hazards)
            {
                if(hazard.Kind==HazardType.Plate&&!hazard.Triggered)
                {if(Vec.Distance(Player,hazard.Position)<.4)Trigger(hazard);foreach(var enemy in Enemies)if(enemy.State!=Mind.Defeated&&enemy.State!=Mind.Devoured&&Vec.Distance(enemy.Position,hazard.Position)<.4)Trigger(hazard);}
                if(hazard.RockTimer>=0)
                {hazard.RockTimer-=dt;if(hazard.RockTimer<=0){hazard.RockTimer=-1;hazard.Triggered=true;Emit("rock",hazard.Position);if(Vec.Distance(Player,hazard.Position)<.8)HurtPlayer(24);foreach(var enemy in Enemies)if(Vec.Distance(enemy.Position,hazard.Position)<.8)Hit(enemy,36,false);}}
                if(!hazard.Active)continue;
                double before=hazard.Age;hazard.Age+=dt;hazard.SpreadTimer-=dt;
                if(hazard.SpreadTimer<=0){hazard.SpreadTimer=100;foreach(var other in Hazards)if(other.Kind==hazard.Kind && Vec.Distance(hazard.Position,other.Position)<1.1)Trigger(other);}
                if((int)(before/.4)!=(int)(hazard.Age/.4))
                {if(Vec.Distance(Player,hazard.Position)<.8)HurtPlayer(7,hazard.Kind==HazardType.Oil);foreach(var enemy in Enemies)if(Vec.Distance(enemy.Position,hazard.Position)<.8)Hit(enemy,enemy.Kind==Species.FireLizard&&hazard.Kind==HazardType.Oil?3:12,false);}
            }
        }
        private void EnemyMove(Enemy enemy,Vec target,double dt,double speed)
        {
            if(SafeSegment(enemy.Position,target)) {Vec direction=(target-enemy.Position).Unit;enemy.Position=Move(enemy.Position,direction*Math.Min(speed*dt,Vec.Distance(enemy.Position,target)),enemy.Kind==Species.Guardian?.45:.25);enemy.Facing=direction;enemy.Route.Clear();return;}
            enemy.PathTimer-=dt;
            if(enemy.Route.Count==0 && enemy.PathTimer<=0){var path=FindPath(enemy.Position,target);for(int i=0;i<Math.Min(2,path.Count);i++)enemy.Route.Enqueue(path[i]);enemy.PathTimer=.25;}
            if(enemy.Route.Count>0){Vec next=enemy.Route.Peek(),direction=(next-enemy.Position).Unit;enemy.Position=Move(enemy.Position,direction*Math.Min(speed*dt,Vec.Distance(enemy.Position,next)));enemy.Facing=direction;if(Vec.Distance(enemy.Position,next)<.05)enemy.Route.Dequeue();}
        }
        private void TickEnemy(Enemy enemy,double dt)
        {
            if(enemy.State==Mind.Defeated||enemy.State==Mind.Devoured)return;
            enemy.Cooldown=Math.Max(0,enemy.Cooldown-dt);enemy.PatrolTime+=dt;
            double range=enemy.Kind==Species.Guardian?7:enemy.Kind==Species.HeroMage?5.5:4.5;
            Vec toPlayer=Player-enemy.Position;
            bool sees=!Hidden&&toPlayer.Length<range&&Vec.Dot(toPlayer.Unit,enemy.Facing)>.5&&Sight(enemy.Position,Player);
            enemy.Alert=Clamp(enemy.Alert+(sees?dt*.9:-dt*.55),0,1);
            if(HasTrait(Species.Rat)&&!Hidden&&toPlayer.Length<3.5)Detection=Math.Max(Detection,.12);
            Detection=Math.Max(Detection,enemy.Alert);
            Enemy rival=null;double rivalDistance=3.5;
            foreach(var other in Enemies)if(other!=enemy && other.Hero!=enemy.Hero && other.Kind!=Species.Guardian && enemy.Kind!=Species.Guardian && other.State!=Mind.Defeated && other.State!=Mind.Devoured)
            {double d=Vec.Distance(enemy.Position,other.Position);if(d<rivalDistance&&Sight(enemy.Position,other.Position)){rival=other;rivalDistance=d;}}
            if(enemy.Kind==Species.Guardian && enemy.Telegraph>0)
            {enemy.Telegraph-=dt;if(enemy.Telegraph<=0){Emit("bossSlam",enemy.SlamPoint);if(Vec.Distance(Player,enemy.SlamPoint)<1.7)HurtPlayer(28);enemy.Cooldown=2.2;}return;}
            bool chase=enemy.Alert>.55 && !Hidden;
            if(chase)
            {
                enemy.State=enemy.HP<enemy.MaxHP*.25 && enemy.Kind!=Species.Guardian?Mind.Flee:Mind.Chase;
                if(enemy.State==Mind.Flee){EnemyMove(enemy,enemy.Position-toPlayer.Unit*2,dt,2);return;}
                if(enemy.Kind==Species.Guardian && toPlayer.Length<4 && enemy.Cooldown==0)
                {enemy.SlamPoint=Player;enemy.Telegraph=1.1;Emit("bossWarn",Player);return;}
                bool ranged=enemy.Kind==Species.HeroMage || enemy.Kind==Species.FireLizard;
                if(toPlayer.Length>(ranged?3:1))EnemyMove(enemy,Player,dt,enemy.Kind==Species.Guardian?1.5:enemy.Hero?2.4:2.1);
                else if(enemy.Cooldown==0)
                {
                    if(ranged)Shots.Add(new Shot{Position=enemy.Position,Direction=toPlayer.Unit,Friendly=false,Damage=enemy.Kind==Species.HeroMage?15:9,Life=2});
                    else HurtPlayer(enemy.Hero?22:6);enemy.Cooldown=ranged?1.6:1;
                }
            }
            else if(rival!=null)
            {
                enemy.State=Mind.Chase;
                if(rivalDistance>1)EnemyMove(enemy,rival.Position,dt,enemy.Hero?2:1.6);
                else if(enemy.Cooldown==0){Hit(rival,enemy.Hero?12:6,false);enemy.Cooldown=.85;Emit("battle",enemy.Position);}
            }
            else if(enemy.Alert>.1) {enemy.State=Mind.Notice;enemy.Facing=toPlayer.Unit;}
            else {enemy.State=Mind.Patrol;Vec destination=enemy.Home+new Vec(Math.Sin(enemy.PatrolTime*.5)*1.5,Math.Cos(enemy.PatrolTime*.5)*.8);EnemyMove(enemy,destination,dt,.65);}
        }
        public void Step(double dt,Controls controls)
        {
            dt=Clamp(dt,0,.05);
            if(State==Phase.Intro){IntroTime+=dt;if(IntroTime>=8)SkipIntro();return;}
            if(State==Phase.Dead){DeathTime+=dt;if(DeathTime>=3)RetryFloor();return;}
            if(State!=Phase.Playing)return;
            Clock+=dt;Stats.Biomass=Math.Max(0,Stats.Biomass-dt*.18);Protection=Math.Max(0,Protection-dt);HardenTime=Math.Max(0,HardenTime-dt);EchoTime=Math.Max(0,EchoTime-dt);ManaTime=Math.Max(0,ManaTime-dt);
            BiteCooldown=Math.Max(0,BiteCooldown-dt);SlamCooldown=Math.Max(0,SlamCooldown-dt);TackleCooldown=Math.Max(0,TackleCooldown-dt);for(int i=0;i<4;i++)skillCooldown[i]=Math.Max(0,skillCooldown[i]-dt);
            Facing=controls.Aim.Length>.01?controls.Aim.Unit:Facing;
            Hidden=false;if(controls.Hide)foreach(var spot in HideSpots)if(Vec.Distance(Player,spot)<.75){Hidden=true;Player=spot;break;}
            if(!Hidden)
            {
                Vec movement=new Vec(Math.Sign(controls.X),Math.Sign(controls.Y));if(movement.Length>0)Player=Move(Player,movement.Unit*(Speed*dt));
                if(controls.Bite && BiteCooldown==0){Melee(1.1,1,false);BiteCooldown=.25;Emit("bite",Player);}
                if(controls.Slam && SlamCooldown==0){Melee(1.8,2.5,true);Smash();dashDirection=Facing;dashTime=.12;SlamCooldown=1.1;Emit("slam",Player);}
                if(controls.Tackle && TackleCooldown==0){Melee(1.4,1.3,true);dashDirection=Facing;dashTime=.2;TackleCooldown=1.2;Protection=Math.Max(Protection,.2);Emit("tackle",Player);}
                if(dashTime>0){Player=Move(Player,dashDirection*(8*dt));dashTime-=dt;}
                Skill(controls.Skill);
            }
            else CancelDevour();
            Detection=0;foreach(var enemy in Enemies)TickEnemy(enemy,dt);
            TickShots(dt);TickHazards(dt);
            if(Stats.FireRank==3 && !Hidden){fireBodyTick-=dt;if(fireBodyTick<=0){fireBodyTick=.8;foreach(var enemy in Enemies)if(Vec.Distance(Player,enemy.Position)<1)Hit(enemy,Damage*.8);foreach(var hazard in Hazards)if(hazard.Kind==HazardType.Oil&&Vec.Distance(Player,hazard.Position)<.7)Trigger(hazard);}}
            if(State!=Phase.Playing)return;
            Devour(controls.Devour,dt);
            if(controls.Gate && !Hidden && Vec.Distance(Player,Gate)<1.2)
            {
                if(!GateOpen){Say("The Guardian's Dungeon Core seals this Rift Gate.");return;}
                if(Stats.Floor<2)EnterFloor(Stats.Floor+1,true);
                else {State=Phase.Escaped;Emit("escape",Player);if(Escaped!=null)Escaped();}
            }
        }
    }
}
