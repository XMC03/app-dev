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
    public enum HumanClass { None=-1, Warrior, Cleric, Ranger, Healer, Rogue }
    public enum AttackStyle { Melee, Ranged, Heal }
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
        public HumanClass Class;
        public Vec Position, Home, Facing=new Vec(1,0);
        public double HP, MaxHP, Alert, Cooldown, PatrolTime, PathTimer;
        public Mind State;
        public readonly Queue<Vec> Route=new Queue<Vec>();
        public double Telegraph;
        public Vec SlamPoint;
        public double AttackTime, AttackDuration, LastAttack=-10, Stagger;
        public Vec AttackPoint, AttackDirection;
        public AttackStyle Attack;
        public int AttackTarget=-1;
        public bool Hero { get { return Kind==Species.HeroWarrior || Kind==Species.HeroMage; } }
        public Enemy(int id,Species kind,Vec position) { Id=id;Kind=kind;Class=kind==Species.HeroWarrior?HumanClass.Warrior:kind==Species.HeroMage?HumanClass.Cleric:HumanClass.None;Home=Position=position;HP=MaxHP=Rules.Health(kind); }
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
        public double Speed=5;
        public int Owner=-1;
        public HumanClass Class=HumanClass.None;
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
        private double slamBuffer,tackleBuffer;
        private bool tackleDash,damagedThisStep;
        private readonly HashSet<int> dashHits=new HashSet<int>();
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
        public double SkillRemaining(int slot) {return slot>=1&&slot<=4?skillCooldown[slot-1]:0;}
        public static string EnemyName(Enemy enemy) {return enemy.Hero?enemy.Class.ToString():SpeciesName(enemy.Kind);}
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
            slamBuffer=tackleBuffer=0;tackleDash=false;dashHits.Clear();
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
            Enemies[6].Class=Stats.Floor==0?HumanClass.Cleric:Stats.Floor==1?HumanClass.Ranger:HumanClass.Healer;
            if(Stats.Floor>=1)Enemies[8].Class=HumanClass.Rogue;
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
            // Traverse every crossed tile; coarse samples miss thin wall corners.
            int x=(int)Math.Floor(a.X),y=(int)Math.Floor(a.Y),endX=(int)Math.Floor(b.X),endY=(int)Math.Floor(b.Y);
            if(Solid(x,y)||Solid(endX,endY))return false;
            double dx=b.X-a.X,dy=b.Y-a.Y;int sx=Math.Sign(dx),sy=Math.Sign(dy);
            double stepX=sx==0?double.PositiveInfinity:Math.Abs(1/dx),stepY=sy==0?double.PositiveInfinity:Math.Abs(1/dy);
            double nextX=sx==0?double.PositiveInfinity:(x+(sx>0?1:0)-a.X)/dx,nextY=sy==0?double.PositiveInfinity:(y+(sy>0?1:0)-a.Y)/dy;
            while(x!=endX||y!=endY)
            {
                if(Math.Abs(nextX-nextY)<1e-10)
                {if(Solid(x+sx,y)||Solid(x,y+sy))return false;x+=sx;y+=sy;nextX+=stepX;nextY+=stepY;}
                else if(nextX<nextY){x+=sx;nextX+=stepX;}
                else {y+=sy;nextY+=stepY;}
                if(Solid(x,y))return false;
            }
            return true;
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
            Stats.HP=Math.Max(0,Stats.HP-damage);Protection=.45;damagedThisStep=true;CancelDevour();Emit("hurt",Player);
            if(Stats.HP<=0){State=Phase.Dead;DeathTime=0;Hidden=false;Say("Your body fades. The current floor will restart from its checkpoint.");Emit("death",Player);}
        }
        public void Hit(Enemy enemy,double damage,bool alertPlayer=true)
        {
            if(enemy.State==Mind.Defeated||enemy.State==Mind.Devoured)return;
            enemy.HP=Math.Max(0,enemy.HP-damage);
            if(enemy.HP<=0){enemy.State=Mind.Defeated;enemy.AttackTime=enemy.Telegraph=0;enemy.Route.Clear();Emit("defeat",enemy.Position);if(enemy.Kind==Species.Guardian){CoreAvailable=true;CorePosition=enemy.Position;Say("The Guardian fell! Hold E near the Dungeon Core.");}}
            else {if(alertPlayer)enemy.Alert=1;enemy.State=Mind.Chase;}
        }
        private void Melee(double range,double multiplier,bool knock,bool dash=false)
        {
            foreach(var enemy in Enemies)if(enemy.State!=Mind.Defeated && enemy.State!=Mind.Devoured && Vec.Distance(Player,enemy.Position)<=range && Vec.Dot((enemy.Position-Player).Unit,Facing)>.15 && Sight(Player,enemy.Position))
            {if(dash&&!dashHits.Add(enemy.Id))continue;Hit(enemy,Damage*multiplier);if(knock&&enemy.Kind!=Species.Guardian){enemy.Position=Move(enemy.Position,Facing*.65);enemy.Stagger=.3;enemy.AttackTime=0;enemy.LastAttack=-10;}}
        }
        private void Trigger(Hazard hazard)
        {
            if(hazard.Triggered)return;hazard.Triggered=true;hazard.Age=0;hazard.SpreadTimer=.35;
            if(hazard.Kind==HazardType.Plate){foreach(var rock in Hazards)if(rock.Kind==HazardType.Rock)rock.RockTimer=.85;Emit("trap",hazard.Position);Say("Pressure plate triggered! Leave the marked falling-rock tiles.");}
            else if(hazard.Kind==HazardType.Oil||hazard.Kind==HazardType.Gas)Emit("hazard",hazard.Position);
        }
        private void Smash()
        {foreach(var hazard in Hazards)if(hazard.Barrel && Vec.Distance(Player,hazard.Position)<1.8 && Vec.Dot((hazard.Position-Player).Unit,Facing)>.1&&Sight(Player,hazard.Position))Trigger(hazard);}
        private void Devour(bool holding,double dt)
        {
            if(!holding||Hidden||damagedThisStep){CancelDevour();return;}
            int target=-1;double nearest=1.05;
            if(CoreAvailable&&!CoreDevoured&&Vec.Distance(Player,CorePosition)<nearest&&Sight(Player,CorePosition)){target=999;nearest=Vec.Distance(Player,CorePosition);}
            foreach(var enemy in Enemies)if(enemy.Kind!=Species.Guardian && enemy.State!=Mind.Devoured && enemy.HP<=enemy.MaxHP*.25)
            {double d=Vec.Distance(Player,enemy.Position);if(d<nearest&&Sight(Player,enemy.Position)){target=enemy.Id;nearest=d;}}
            var locked=Enemies.Find(e=>e.Id==DevourTarget);
            if(locked!=null&&locked.Kind!=Species.Guardian&&locked.State!=Mind.Devoured&&locked.HP<=locked.MaxHP*.25&&Vec.Distance(Player,locked.Position)<1.05&&Sight(Player,locked.Position))target=locked.Id;
            if(target<0){CancelDevour();return;}
            var prey=target==999?null:Enemies.Find(e=>e.Id==target);
            int mass=target==999?4:Mass(prey.Kind);
            if(Stats.Biomass+mass>Capacity){CancelDevour();Say("Biomass capacity full. Give digestion a moment.");return;}
            if(target!=DevourTarget){DevourTime=0;DevourTarget=target;}
            DevourTime+=dt;
            if(DevourTime<1.25)return;
            Species kind=target==999?Species.Guardian:prey.Kind;
            if(target==999){CoreDevoured=true;CoreAvailable=false;Say("The Core is yours. Press F at the final Rift Gate to escape.");}
            else {prey.State=Mind.Devoured;Stats.Eaten[(int)kind]++;Say("Devoured "+EnemyName(prey)+". Power becomes yours.");}
            Stats.Biomass+=mass;Stats.Essence=Math.Min(MaxEssence,Stats.Essence+10+mass*2);Stats.XP+=Reward(kind);FloorDevours++;
            if(kind==Species.FireLizard && Stats.FireRank==0)Stats.FireRank=1;
            while(Stats.XP>=NextXP){Stats.XP-=NextXP;Stats.Level++;Stats.HP=Math.Min(MaxHP,Stats.HP+12);Emit("level",Player);}
            Stats.HP=Math.Min(MaxHP,Stats.HP+5);Emit("devour",Player);CancelDevour();
        }
        public static string SpeciesName(Species kind) {return new[]{"Rat","Cave Bat","Fire Lizard","Armored Beetle","Hero Warrior","Hero Mage","Dungeon Guardian"}[(int)kind];}
        public bool SkillKnown(int slot) {return slot==1?HasTrait(Species.FireLizard):slot==2?HasTrait(Species.ArmoredBeetle):slot==3?HasTrait(Species.CaveBat):slot==4&&HasTrait(Species.HeroMage);}
        public double SkillCost(int slot) {return slot==1?6:slot==2?8:5;}
        private bool Skill(int slot)
        {
            if(slot<1||slot>4)return false;
            if(!SkillKnown(slot)){Say("Devour the matching species to learn this skill.");return false;}
            double cost=SkillCost(slot);
            if(skillCooldown[slot-1]>0){Say("Skill is recovering.");return false;}
            if(Stats.Essence<cost){Say("Not enough Essence. Hunt and Devour more prey.");return false;}
            Stats.Essence-=cost;skillCooldown[slot-1]=slot==1?.6:slot==2?7:8;
            if(slot==1)Shots.Add(new Shot{Position=Player+Facing*.4,Direction=Facing,Friendly=true,Fire=true,Damage=Damage*2,Life=1.5,Speed=8});
            if(slot==2)HardenTime=4;
            if(slot==3)EchoTime=6;
            if(slot==4)ManaTime=6;
            Emit("skill",Player);return true;
        }
        private void TickShots(double dt)
        {
            for(int i=Shots.Count-1;i>=0;i--)
            {
                var shot=Shots[i];double travel=Math.Min(dt,Math.Max(0,shot.Life))*shot.Speed;
                int steps=Math.Max(1,(int)Math.Ceiling(travel/.08));
                for(int sample=0;sample<steps && shot.Life>0;sample++)
                {
                    shot.Position+=shot.Direction*(travel/steps);
                    // A wall terminates the ray before any targets or hazards behind it.
                    if(Solid((int)Math.Floor(shot.Position.X),(int)Math.Floor(shot.Position.Y))){shot.Life=0;break;}
                    if(shot.Fire)foreach(var hazard in Hazards)if(!hazard.Triggered&&(hazard.Kind==HazardType.Oil||hazard.Kind==HazardType.Gas)&&Vec.Distance(shot.Position,hazard.Position)<.6&&Sight(shot.Position,hazard.Position)){Trigger(hazard);shot.Life=0;break;}
                    if(shot.Life<=0)break;
                    if(shot.Friendly)
                    {
                        foreach(var enemy in Enemies)if(enemy.State!=Mind.Defeated&&enemy.State!=Mind.Devoured&&Vec.Distance(shot.Position,enemy.Position)<(enemy.Kind==Species.Guardian?.75:.4)&&Sight(shot.Position,enemy.Position)){Hit(enemy,shot.Damage);shot.Life=0;break;}
                    }
                    else
                    {
                        if(Vec.Distance(shot.Position,Player)<.35&&Sight(shot.Position,Player)){HurtPlayer(shot.Damage,shot.Fire);shot.Life=0;}
                        var owner=Enemies.Find(e=>e.Id==shot.Owner);
                        if(owner!=null && shot.Life>0)foreach(var enemy in Enemies)if(enemy.Hero!=owner.Hero&&enemy.Kind!=Species.Guardian&&enemy.State!=Mind.Defeated&&enemy.State!=Mind.Devoured&&Vec.Distance(shot.Position,enemy.Position)<.4&&Sight(shot.Position,enemy.Position)){Hit(enemy,shot.Damage,false);shot.Life=0;break;}
                    }
                }
                shot.Life-=dt;if(shot.Life<=0)Shots.RemoveAt(i);
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
        private void BeginAttack(Enemy enemy,Vec point,int target,AttackStyle style)
        {
            enemy.Attack=style;enemy.AttackTarget=target;enemy.AttackPoint=point;enemy.AttackDirection=(point-enemy.Position).Unit;enemy.Facing=enemy.AttackDirection;
            enemy.AttackDuration=style==AttackStyle.Heal?.65:enemy.Class==HumanClass.Rogue?.28:enemy.Hero?.45:.3;
            enemy.AttackTime=enemy.AttackDuration;enemy.LastAttack=Clock;enemy.Route.Clear();
        }
        private void ResolveAttack(Enemy enemy)
        {
            var target=Enemies.Find(e=>e.Id==enemy.AttackTarget);
            if(enemy.Attack==AttackStyle.Heal)
            {
                if(target!=null&&target.Hero&&target.State!=Mind.Defeated&&target.State!=Mind.Devoured&&Vec.Distance(enemy.Position,target.Position)<=4&&Sight(enemy.Position,target.Position))
                {target.HP=Math.Min(target.MaxHP,target.HP+12);Emit("heal",target.Position);}
                enemy.Cooldown=4;return;
            }
            if(enemy.Attack==AttackStyle.Ranged)
            {
                Shots.Add(new Shot{Position=enemy.Position,Direction=enemy.AttackDirection,Friendly=false,Owner=enemy.Id,Class=enemy.Class,Fire=enemy.Kind==Species.FireLizard,Damage=enemy.Class==HumanClass.Ranger?13:enemy.Class==HumanClass.Healer?10:enemy.Hero?15:9,Speed=enemy.Class==HumanClass.Ranger?7:5,Life=2});
                enemy.Cooldown=enemy.Class==HumanClass.Ranger?1.8:1.6;return;
            }
            Vec point=target==null?Player:target.Position;
            bool eligible=target==null?enemy.AttackTarget==-1&&!Hidden:target.State!=Mind.Defeated&&target.State!=Mind.Devoured;
            if(eligible&&Vec.Distance(enemy.Position,point)<1.35&&Vec.Dot((point-enemy.Position).Unit,enemy.AttackDirection)>.5&&Sight(enemy.Position,point))
            {if(target==null)HurtPlayer(enemy.Class==HumanClass.Rogue?16:enemy.Hero?22:6);else Hit(target,enemy.Hero?12:6,false);}
            enemy.Cooldown=enemy.Class==HumanClass.Rogue?.8:1;Emit("battle",enemy.Position);
        }
        private void TickEnemy(Enemy enemy,double dt)
        {
            if(enemy.State==Mind.Defeated||enemy.State==Mind.Devoured)return;
            enemy.Cooldown=Math.Max(0,enemy.Cooldown-dt);enemy.PatrolTime+=dt;
            if(enemy.Stagger>0){enemy.Stagger=Math.Max(0,enemy.Stagger-dt);return;}
            double range=enemy.Kind==Species.Guardian?7:enemy.Class==HumanClass.Ranger?6.5:enemy.Kind==Species.HeroMage?5.5:4.5;
            Vec toPlayer=Player-enemy.Position;
            bool sees=!Hidden&&toPlayer.Length<range&&Vec.Dot(toPlayer.Unit,enemy.Facing)>.5&&Sight(enemy.Position,Player);
            enemy.Alert=Clamp(enemy.Alert+(sees?dt*.9:-dt*.55),0,1);
            if(HasTrait(Species.Rat)&&!Hidden&&toPlayer.Length<3.5)Detection=Math.Max(Detection,.12);
            Detection=Math.Max(Detection,enemy.Alert);
            if(enemy.AttackTime>0){enemy.AttackTime=Math.Max(0,enemy.AttackTime-dt);if(enemy.AttackTime<=0)ResolveAttack(enemy);return;}
            if(enemy.Kind==Species.Guardian && enemy.Telegraph>0)
            {enemy.Telegraph-=dt;if(enemy.Telegraph<=0){Emit("bossSlam",enemy.SlamPoint);if(Vec.Distance(Player,enemy.SlamPoint)<1.7&&Sight(enemy.Position,Player))HurtPlayer(28);enemy.Cooldown=2.2;}return;}
            if(enemy.Class==HumanClass.Healer&&enemy.Cooldown==0)
            {
                Enemy wounded=null;double missing=0;
                foreach(var ally in Enemies)if(ally.Hero&&ally.State!=Mind.Defeated&&ally.State!=Mind.Devoured&&ally.MaxHP-ally.HP>missing&&Vec.Distance(enemy.Position,ally.Position)<=4&&Sight(enemy.Position,ally.Position))
                {wounded=ally;missing=ally.MaxHP-ally.HP;}
                if(wounded!=null&&missing>=6){BeginAttack(enemy,wounded.Position,wounded.Id,AttackStyle.Heal);return;}
            }
            Enemy rival=null;double rivalDistance=3.5;
            foreach(var other in Enemies)if(other!=enemy && other.Hero!=enemy.Hero && other.Kind!=Species.Guardian && enemy.Kind!=Species.Guardian && other.State!=Mind.Defeated && other.State!=Mind.Devoured)
            {double d=Vec.Distance(enemy.Position,other.Position);if(d<rivalDistance&&Sight(enemy.Position,other.Position)){rival=other;rivalDistance=d;}}
            bool chase=enemy.Alert>.55 && !Hidden;
            if(chase || rival!=null)
            {
                Vec target=chase?Player:rival.Position,delta=target-enemy.Position;int targetId=chase?-1:rival.Id;
                enemy.State=enemy.HP<enemy.MaxHP*.25 && enemy.Kind!=Species.Guardian?Mind.Flee:Mind.Chase;
                if(enemy.State==Mind.Flee){EnemyMove(enemy,enemy.Position-delta.Unit*2,dt,2);return;}
                if(enemy.Kind==Species.Guardian && delta.Length<4 && enemy.Cooldown==0)
                {enemy.SlamPoint=Player;enemy.Telegraph=1.1;Emit("bossWarn",Player);return;}
                bool ranged=enemy.Kind==Species.HeroMage||enemy.Kind==Species.FireLizard;
                double attackRange=ranged?(enemy.Class==HumanClass.Ranger?4.5:3.5):1.1;
                if(ranged&&delta.Length<2 && enemy.Cooldown>0)
                {EnemyMove(enemy,enemy.Position-delta.Unit*1.5,dt,1.5);enemy.Facing=delta.Unit;}
                else if(delta.Length>attackRange||!Sight(enemy.Position,target))EnemyMove(enemy,target,dt,enemy.Kind==Species.Guardian?1.5:enemy.Class==HumanClass.Rogue?2.8:enemy.Hero?2.4:2.1);
                else
                {
                    enemy.Facing=delta.Unit;
                    if(enemy.Cooldown==0)BeginAttack(enemy,target,targetId,ranged?AttackStyle.Ranged:AttackStyle.Melee);
                }
            }
            else if(enemy.Alert>.1) {enemy.State=Mind.Notice;enemy.Facing=toPlayer.Unit;}
            else {enemy.State=Mind.Patrol;Vec destination=enemy.Home+new Vec(Math.Sin(enemy.PatrolTime*.5)*1.5,Math.Cos(enemy.PatrolTime*.5)*.8);EnemyMove(enemy,destination,dt,.65);}
        }
        public void Step(double dt,Controls controls)
        {
            // Bound catch-up after a stall, but retain elapsed time within a normal frame.
            if(double.IsNaN(dt)||double.IsInfinity(dt))return;
            dt=Clamp(dt,0,.25);
            if(dt==0){StepSlice(0,controls);return;}
            while(dt>1e-8)
            {
                double slice=Math.Min(.02,dt);var phase=State;int floor=Stats.Floor;
                StepSlice(slice,controls);dt-=slice;
                controls.Slam=controls.Tackle=controls.Gate=false;controls.Skill=0;
                if(State!=phase||Stats.Floor!=floor)break;
            }
        }
        private void StepSlice(double dt,Controls controls)
        {
            damagedThisStep=false;
            if(State==Phase.Intro){IntroTime+=dt;if(IntroTime>=8)SkipIntro();return;}
            if(State==Phase.Dead){DeathTime+=dt;if(DeathTime>=3)RetryFloor();return;}
            if(State!=Phase.Playing)return;
            Clock+=dt;Stats.Biomass=Math.Max(0,Stats.Biomass-dt*.18);Protection=Math.Max(0,Protection-dt);HardenTime=Math.Max(0,HardenTime-dt);EchoTime=Math.Max(0,EchoTime-dt);ManaTime=Math.Max(0,ManaTime-dt);
            BiteCooldown=Math.Max(0,BiteCooldown-dt);SlamCooldown=Math.Max(0,SlamCooldown-dt);TackleCooldown=Math.Max(0,TackleCooldown-dt);for(int i=0;i<4;i++)skillCooldown[i]=Math.Max(0,skillCooldown[i]-dt);
            Facing=controls.Aim.Length>.01?controls.Aim.Unit:Facing;
            Hidden=false;if(controls.Hide)foreach(var spot in HideSpots)if(Vec.Distance(Player,spot)<.75){Hidden=true;Player=spot;dashTime=0;tackleDash=false;break;}
            slamBuffer=Math.Max(0,slamBuffer-dt);tackleBuffer=Math.Max(0,tackleBuffer-dt);
            if(controls.Slam)slamBuffer=.15;if(controls.Tackle)tackleBuffer=.15;
            bool attacking=false;
            if(!Hidden)
            {
                Vec movement=new Vec(Math.Sign(controls.X),Math.Sign(controls.Y));
                // Dodge has priority; one input frame cannot stack all three attacks.
                if(tackleBuffer>0&&TackleCooldown==0)
                {dashHits.Clear();tackleDash=true;Melee(1.4,1.3,true,true);dashDirection=Facing;dashTime=.2;TackleCooldown=1.2;Protection=Math.Max(Protection,.2);tackleBuffer=slamBuffer=0;attacking=true;Emit("tackle",Player);}
                else if(dashTime<=0&&slamBuffer>0&&SlamCooldown==0)
                {Melee(1.8,2.5,true);Smash();dashDirection=Facing;dashTime=.12;tackleDash=false;SlamCooldown=1.1;slamBuffer=0;attacking=true;Emit("slam",Player);}
                else if(dashTime<=0&&controls.Bite&&BiteCooldown==0)
                {Melee(1.1,1,false);BiteCooldown=.25;attacking=true;Emit("bite",Player);}
                if(dashTime>0)
                {
                    Facing=dashDirection;double distance=8*Math.Min(dt,dashTime);int samples=Math.Max(1,(int)Math.Ceiling(distance/.1));
                    for(int i=0;i<samples;i++){Player=Move(Player,dashDirection*(distance/samples));if(tackleDash)Melee(.75,1.3,true,true);}
                    dashTime=Math.Max(0,dashTime-dt);if(dashTime<1e-7)dashTime=0;attacking=true;
                }
                else if(movement.Length>0)Player=Move(Player,movement.Unit*(Speed*dt));
                if(Skill(controls.Skill)&&controls.Skill==1)attacking=true;
            }
            else {CancelDevour();slamBuffer=tackleBuffer=0;}
            Detection=0;foreach(var enemy in Enemies)TickEnemy(enemy,dt);
            TickShots(dt);TickHazards(dt);
            if(Stats.FireRank==3 && !Hidden){fireBodyTick-=dt;if(fireBodyTick<=0){fireBodyTick=.8;foreach(var enemy in Enemies)if(Vec.Distance(Player,enemy.Position)<1&&Sight(Player,enemy.Position))Hit(enemy,Damage*.8);foreach(var hazard in Hazards)if(hazard.Kind==HazardType.Oil&&Vec.Distance(Player,hazard.Position)<.7)Trigger(hazard);}}
            if(State!=Phase.Playing)return;
            Devour(controls.Devour&&!attacking,dt);
            if(controls.Gate && !Hidden && Vec.Distance(Player,Gate)<1.2)
            {
                if(!GateOpen){Say("The Guardian's Dungeon Core seals this Rift Gate.");return;}
                if(Stats.Floor<2)EnterFloor(Stats.Floor+1,true);
                else {State=Phase.Escaped;Emit("escape",Player);if(Escaped!=null)Escaped();}
            }
        }
    }
}
