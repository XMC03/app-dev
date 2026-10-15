using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SlimeAscent;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
    private static Rules Start(bool isolated=true)
    {var game=new Rules();game.NewRun();game.SkipIntro();if(isolated){game.Enemies.Clear();game.Hazards.Clear();}return game;}
    private static void Tick(Rules game,double seconds,Controls input)
    {for(int i=0;i<(int)Math.Ceiling(seconds/.02);i++)game.Step(.02,input);}
    private static Enemy Prey(Rules game,Species kind,Vec point,bool dead=true)
    {var enemy=new Enemy(game.Enemies.Count,kind,point);game.Enemies.Add(enemy);if(dead)game.Hit(enemy,enemy.MaxHP);return enemy;}
    private static void Feed(Rules game,Species kind)
    {Prey(game,kind,game.Player);Tick(game,1.3,new Controls{Aim=new Vec(1,0),Devour=true});}
    private static int Main()
    {
        try
        {
            Test("Unity sources parse as supported C# 9",()=>{
                string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../SlimeAscent"));
                foreach(string file in Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories))
                    foreach(var issue in CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.CSharp9)).GetDiagnostics())
                        Check(issue.Severity!=DiagnosticSeverity.Error,file+": "+issue);
            });
            Test("Opening leads to Lower floor; paused and evolution menus freeze simulation",()=>{
                var game=new Rules();game.NewRun();Check(game.State==Phase.Intro,"Opening missing");Tick(game,8.1,new Controls());Check(game.State==Phase.Playing&&game.Stats.Floor==0,"Intro did not enter floor");
                game.Pause();double clock=game.Clock;Tick(game,.5,new Controls{X=1});Check(game.Clock==clock,"Paused time advanced");game.Pause();game.EvolveMenu();Tick(game,.5,new Controls{X=1});Check(game.Clock==clock,"Evolution did not pause");game.EvolveMenu();
            });
            Test("Three hand-built layouts expose reachable creatures, cracks, hazards and gates",()=>{
                for(int floor=0;floor<3;floor++){
                    var game=new Rules();Check(game.Continue(new Progress{Floor=floor}),"Floor did not load");
                    var targets=new List<Vec>{game.Gate};targets.AddRange(game.HideSpots);foreach(var e in game.Enemies)targets.Add(e.Position);foreach(var h in game.Hazards)targets.Add(h.Position);
                    foreach(var target in targets)Check(game.FindPath(game.Player,target).Count>0,"Unreachable target on floor "+floor);
                    var counts=new int[3];foreach(var e in game.Enemies)counts[Math.Min(2,(int)e.Position.X/16)]++;
                    foreach(int count in counts)Check(count<=8,"Room exceeded enemy cap");
                }
            });
            Test("Normalized movement, solid walls and vision obstruction",()=>{
                var game=Start();Vec before=game.Player;game.Step(.05,new Controls{X=1,Y=1});Check(Math.Abs(Vec.Distance(before,game.Player)-game.Speed*.05)<.001,"Diagonal speed changed");
                Vec stopped=game.Move(new Vec(14.5,4.5),new Vec(6,0));Check(stopped.X<14.75,"Crossed solid partition");
                Check(!game.Sight(new Vec(14.5,4.5),new Vec(18.5,4.5)),"Vision passed through wall");Check(game.Sight(new Vec(14.5,7.5),new Vec(18.5,7.5)),"Tunnel blocked vision");
            });
            Test("Bite respects facing and cooldown; Slam smashes barrels; Tackle knocks back",()=>{
                var game=Start();var front=Prey(game,Species.ArmoredBeetle,new Vec(4.3,7.5),false);var back=Prey(game,Species.ArmoredBeetle,new Vec(2.7,7.5),false);
                game.Step(0,new Controls{Aim=new Vec(1,0),Bite=true});Check(front.HP==front.MaxHP-game.Damage&&back.HP==back.MaxHP,"Bite hit wrong side");double hp=front.HP;game.Step(0,new Controls{Aim=new Vec(1,0),Bite=true});Check(front.HP==hp,"Bite ignored cooldown");
                var oil=new Hazard(HazardType.Oil,4.5,7.5,true);game.Hazards.Add(oil);game.Step(0,new Controls{Aim=new Vec(1,0),Slam=true});Check(oil.Triggered,"Slam did not smash barrel");
                game.Player=front.Position-new Vec(.8,0);Vec position=front.Position;game.Step(0,new Controls{Aim=new Vec(1,0),Tackle=true});Check(front.Position.X>position.X,"Tackle did not knock back");Check(game.Protection>0,"Tackle gave no dodge protection");
            });
            Test("Only weakened/defeated prey is Devoured; channel yields XP, Essence and traits",()=>{
                var game=Start();var rat=Prey(game,Species.Rat,game.Player+new Vec(.7,0),false);
                Tick(game,1.3,new Controls{Devour=true});Check(game.Stats.Eaten[0]==0,"Devoured healthy creature");
                game.Hit(rat,rat.MaxHP);rat.Position=game.Player+new Vec(.7,0);Tick(game,.6,new Controls{Devour=true});Check(game.DevourTime>.5&&game.Stats.Eaten[0]==0,"Devour was immediate");
                game.Step(.02,new Controls());Check(game.DevourTime==0,"Released E did not cancel channel");Tick(game,1.3,new Controls{Devour=true});
                Check(game.Stats.Eaten[0]==1&&game.Stats.Essence>0&&game.Stats.XP==12,"Devour rewards missing");Check(rat.State==Mind.Devoured,"Corpse remained edible");
            });
            Test("Biomass capacity limits consumption and digestion makes room",()=>{
                var game=Start();game.Stats.Biomass=game.Capacity;var rat=Prey(game,Species.Rat,game.Player);Tick(game,1.4,new Controls{Devour=true});Check(rat.State!=Mind.Devoured,"Capacity ignored");
                game.Stats.Biomass=game.Capacity-1.1;Tick(game,1.3,new Controls{Devour=true});Check(rat.State==Mind.Devoured,"Digestion did not unblock eating");
            });
            Test("All five traits and the complete fire evolution chain",()=>{
                var game=Start();Feed(game,Species.Rat);Feed(game,Species.CaveBat);Feed(game,Species.FireLizard);Feed(game,Species.ArmoredBeetle);
                game.Stats.Biomass=0;Feed(game,Species.HeroMage);Check(game.HasTrait(Species.Rat)&&game.HasTrait(Species.CaveBat)&&game.HasTrait(Species.FireLizard)&&game.HasTrait(Species.ArmoredBeetle)&&game.HasTrait(Species.HeroMage),"Trait unlock missing");
                Check(game.Stats.FireRank==1&&!game.UpgradeFire(),"Heat Immunity unlocked too soon");game.Stats.Biomass=0;Feed(game,Species.FireLizard);Check(game.UpgradeFire()&&game.Stats.FireRank==2,"Heat Immunity failed");
                Feed(game,Species.FireLizard);Check(game.UpgradeFire()&&game.Stats.FireRank==3,"Flame Body failed");Check(!game.UpgradeFire(),"Fire rank exceeded chain");
            });
            Test("Skill learning, Essence costs, projectile creation and cooldown",()=>{
                var game=Start();game.Stats.Essence=30;game.Step(0,new Controls{Aim=new Vec(1,0),Skill=1});Check(game.Shots.Count==0&&game.Stats.Essence==30,"Unlearned skill fired");
                game.Stats.Eaten[(int)Species.FireLizard]=1;game.Step(0,new Controls{Aim=new Vec(1,0),Skill=1});Check(game.Shots.Count==1&&game.Stats.Essence==24,"Flame Spit did not spend Essence");game.Step(0,new Controls{Skill=1});Check(game.Stats.Essence==24,"Cooldown did not block repeat");
                game.Stats.Eaten[(int)Species.ArmoredBeetle]=1;game.Stats.Eaten[(int)Species.CaveBat]=1;game.Stats.Eaten[(int)Species.HeroMage]=1;
                game.Step(0,new Controls{Skill=2});game.Step(0,new Controls{Skill=3});game.Step(0,new Controls{Skill=4});Check(game.HardenTime>0&&game.EchoTime>0&&game.ManaTime>0,"Learned skills did not activate");
            });
            Test("Fire and poison chains spread across three linked tiles and damage prey",()=>{
                foreach(var kind in new[]{HazardType.Oil,HazardType.Gas}){
                    var game=Start();for(int i=0;i<3;i++)game.Hazards.Add(new Hazard(kind,4.5+i,7.5,i==0));
                    var prey=Prey(game,Species.ArmoredBeetle,new Vec(6.5,7.5),false);
                    game.Step(0,new Controls{Aim=new Vec(1,0),Slam=true});Tick(game,1.2,new Controls());Check(game.Hazards.TrueForAll(h=>h.Triggered),"Chain failed to spread");Check(prey.HP<prey.MaxHP,"Chain damaged no prey");
                }
            });
            Test("Flame Spit ignites oil and gas; Heat Immunity prevents fire damage",()=>{
                var game=Start();game.Stats.Eaten[2]=1;game.Stats.FireRank=2;game.Stats.Essence=20;var oil=new Hazard(HazardType.Oil,4.5,7.5);game.Hazards.Add(oil);
                game.Step(0,new Controls{Aim=new Vec(1,0),Skill=1});Tick(game,.15,new Controls());Check(oil.Triggered,"Flame projectile did not ignite oil");game.Player=oil.Position;double hp=game.Stats.HP;Tick(game,1,new Controls());Check(game.Stats.HP==hp,"Heat Immunity did not protect player");
                var gas=new Hazard(HazardType.Gas,5.5,7.5);game.Hazards.Add(gas);Tick(game,.6,new Controls());game.Step(0,new Controls{Aim=new Vec(1,0),Skill=1});Tick(game,.15,new Controls());Check(gas.Triggered,"Flame projectile did not ignite gas");
            });
            Test("Pressure plate warns before linked rocks fall",()=>{
                var game=Start();var plate=new Hazard(HazardType.Plate,game.Player.X,game.Player.Y);var rock=new Hazard(HazardType.Rock,game.Player.X+2,game.Player.Y);game.Hazards.Add(plate);game.Hazards.Add(rock);game.Step(.02,new Controls());Check(rock.RockTimer>.7&&!rock.Triggered,"Trap had no warning delay");Tick(game,1,new Controls());Check(rock.Triggered,"Linked rocks did not fall");
            });
            Test("Hide requires a marked spot, breaks detection and blocks attacks",()=>{
                var game=Start();var rat=Prey(game,Species.Rat,game.Player+new Vec(.7,0),false);rat.Facing=new Vec(-1,0);rat.Alert=.8;Tick(game,.3,new Controls());double alert=rat.Alert;
                game.HideSpots.Add(game.Player);double hp=rat.HP;game.Step(.02,new Controls{Hide=true,Bite=true,Aim=new Vec(1,0)});Check(game.Hidden&&rat.HP==hp,"Hidden slime attacked");Tick(game,.3,new Controls{Hide=true});Check(rat.Alert<alert,"Hiding did not decay suspicion");
                game.Player=new Vec(4.5,9.5);game.Step(0,new Controls{Hide=true});Check(!game.Hidden,"Unmarked hiding allowed");
            });
            Test("Heroes and monsters fight, leaving Devour opportunities",()=>{
                var game=Start();game.Player=new Vec(3.5,12.5);var rat=Prey(game,Species.Rat,new Vec(7.2,7.5),false);var hero=Prey(game,Species.HeroWarrior,new Vec(6.5,7.5),false);Tick(game,.6,new Controls());Check(rat.HP<rat.MaxHP&&hero.HP<hero.MaxHP,"Faction battle did not happen after wind-ups");Check(rat.Alert<.1,"Monster blamed an unseen slime for hero damage");
            });
            Test("Guardian slam has a readable warning and can be dodged",()=>{
                var game=Start();game.Player=new Vec(5.5,7.5);var boss=Prey(game,Species.Guardian,new Vec(7.5,7.5),false);boss.Facing=new Vec(-1,0);boss.Alert=1;game.Step(0,new Controls());Check(boss.Telegraph>1,"Guardian did not warn");double hp=game.Stats.HP;game.Player=new Vec(5.5,10.5);Tick(game,1.2,new Controls());Check(game.Stats.HP==hp,"Dodging the slam did not avoid damage");
            });
            Test("Death restores the current floor checkpoint and discards current-floor gains",()=>{
                var game=new Rules();var p=new Progress{Floor=1,Level=3,HP=60,Essence=20,FireRank=1};p.Eaten[2]=1;Check(game.Continue(p),"Continue failed");game.Stats.Eaten[0]=4;game.Stats.Essence=1;game.Stats.HP=1;
                game.Shots.Add(new Shot{Position=game.Player,Direction=new Vec(1,0),Damage=10});game.Step(.02,new Controls());Check(game.State==Phase.Dead,"Death did not trigger");Tick(game,3.1,new Controls());Check(game.State==Phase.Playing&&game.Stats.Floor==1&&game.Stats.Eaten[0]==0&&game.Stats.Essence==20&&game.Stats.HP==60,"Checkpoint restore failed");
            });
            Test("All floor transitions, defeated Guardian, held-E Core Devour and final escape",()=>{
                var game=Start(false);var entered=new List<int>();game.FloorEntered+=f=>entered.Add(f);bool escaped=false;game.Escaped+=()=>escaped=true;
                for(int floor=0;floor<2;floor++){game.Player=game.Gate;game.Step(0,new Controls{Gate=true});Check(game.Stats.Floor==floor+1,"Rift did not change floor");}
                Check(entered.Count==2&&entered[0]==1&&entered[1]==2,"Floor events missing");game.Player=game.Gate;game.Step(0,new Controls{Gate=true});Check(game.State==Phase.Playing,"Escaped without Core");
                Enemy boss=game.Enemies.Find(e=>e.Kind==Species.Guardian);game.Hit(boss,boss.MaxHP);Check(game.CoreAvailable&&!game.GateOpen,"Boss did not drop locked Core");
                game.Enemies.RemoveAll(e=>e.Kind!=Species.Guardian);game.Player=game.CorePosition;Tick(game,1.3,new Controls{Devour=true});Check(game.CoreDevoured&&game.GateOpen,"Core was not Devoured");game.Player=game.Gate;game.Step(0,new Controls{Gate=true});Check(game.State==Phase.Escaped&&escaped,"Ending did not load");
            });
            Test("A fresh slime can hunt and Devour its first rat using combat and movement",()=>{
                var game=Start(false);var rat=game.Enemies[0];
                for(int i=0;i<500&&rat.State!=Mind.Devoured&&game.State==Phase.Playing;i++){
                    Vec delta=rat.Position-game.Player;bool edible=rat.State==Mind.Defeated||rat.HP<=rat.MaxHP*.25;
                    game.Step(.02,new Controls{X=delta.Length>.8?Math.Sign(delta.X):0,Y=delta.Length>.8?Math.Sign(delta.Y):0,Aim=delta,Bite=!edible,Devour=edible});
                }
                Check(rat.State==Mind.Devoured&&game.Stats.HP>0&&game.HasTrait(Species.Rat),"Opening prey encounter failed");
            });
            Test("All five human classes spawn across the campaign with stable prey rewards",()=>{
                var found=new HashSet<HumanClass>();
                for(int floor=0;floor<3;floor++){var game=new Rules();game.Continue(new Progress{Floor=floor});foreach(var enemy in game.Enemies)if(enemy.Hero){found.Add(enemy.Class);Check(enemy.Kind==(enemy.Class==HumanClass.Warrior||enemy.Class==HumanClass.Rogue?Species.HeroWarrior:Species.HeroMage),"Class broke GDD reward category");}}
                Check(found.Count==5,"A supplied human class is missing");
            });
            Test("Normal melee warns first and a sidestep avoids the committed strike",()=>{
                foreach(bool dodge in new[]{false,true}){
                    var game=Start();var hero=Prey(game,Species.HeroWarrior,game.Player+new Vec(.8,0),false);hero.Alert=1;hero.Facing=new Vec(-1,0);double hp=game.Stats.HP;
                    game.Step(0,new Controls());Check(hero.AttackTime>.4&&game.Stats.HP==hp,"Melee hit without a wind-up");
                    if(dodge)game.Player=hero.Position+new Vec(-.4,.9);Tick(game,.55,new Controls());Check(dodge?game.Stats.HP==hp:game.Stats.HP<hp,"Committed melee strike resolved incorrectly");
                }
            });
            Test("Slam interrupts normal attacks and defeating a boss clears its warning",()=>{
                var game=Start();var hero=Prey(game,Species.HeroWarrior,game.Player+new Vec(.8,0),false);hero.Alert=1;hero.Facing=new Vec(-1,0);game.Step(0,new Controls());
                game.Step(0,new Controls{Aim=new Vec(1,0),Slam=true});Check(hero.AttackTime==0&&hero.Stagger>0,"Slam did not interrupt");double hp=game.Stats.HP;Tick(game,.25,new Controls());Check(game.Stats.HP==hp,"Interrupted strike still hit");
                var boss=Prey(game,Species.Guardian,game.Player+new Vec(2,0),false);boss.Telegraph=1;game.Hit(boss,boss.MaxHP);Check(boss.Telegraph==0,"Defeated boss retained danger warning");
            });
            Test("Ranger holds firing distance, commits aim and retreats during recovery",()=>{
                var game=Start();var ranger=Prey(game,Species.HeroMage,game.Player+new Vec(3,0),false);ranger.Class=HumanClass.Ranger;ranger.Alert=1;ranger.Facing=new Vec(-1,0);Vec start=ranger.Position;
                game.Step(0,new Controls());Check(ranger.Attack==AttackStyle.Ranged&&ranger.AttackTime>0&&Vec.Distance(start,ranger.Position)<.001,"Ranger charged into melee");
                game.Player+=new Vec(0,2);Tick(game,.5,new Controls());Check(game.Shots.Count==1&&game.Shots[0].Class==HumanClass.Ranger&&game.Shots[0].Direction.Y==0,"Arrow homed after aim was committed");
                game.Shots.Clear();game.Player=ranger.Position-new Vec(1,0);Vec before=ranger.Position;game.Step(.05,new Controls());Check(ranger.Position.X>before.X,"Ranger did not retreat at close range");
            });
            Test("Healer supports living humans, cannot revive a corpse and can be interrupted",()=>{
                foreach(int scenario in new[]{0,1,2}){
                    var game=Start();game.Player=new Vec(3.5,12.5);var healer=Prey(game,Species.HeroMage,new Vec(7.5,7.5),false);healer.Class=HumanClass.Healer;
                    var ally=Prey(game,Species.HeroWarrior,new Vec(8.5,7.5),false);game.Hit(ally,20,false);double hp=ally.HP;game.Step(0,new Controls());Check(healer.Attack==AttackStyle.Heal&&healer.AttackTime>.6,"Healer did not begin support cast");
                    if(scenario==1)game.Hit(ally,ally.HP);if(scenario==2){game.Player=healer.Position-new Vec(.8,0);game.Step(0,new Controls{Aim=new Vec(1,0),Slam=true});}
                    Tick(game,.75,new Controls());Check(scenario==0?ally.HP>hp:scenario==1?ally.State==Mind.Defeated&&ally.HP==0:ally.HP<=hp,"Healing ignored life state or interruption");
                }
            });
            Test("Swept projectiles hit fast crossings and terminate at walls before hazards",()=>{
                var game=Start();var prey=Prey(game,Species.ArmoredBeetle,new Vec(4.5,7.5),false);game.Shots.Add(new Shot{Position=game.Player,Direction=new Vec(1,0),Friendly=true,Speed=80,Damage=10});game.Step(.05,new Controls());Check(prey.HP==prey.MaxHP-10&&game.Shots.Count==0,"Fast shot skipped prey");
                game=Start();game.Player=new Vec(3.5,12.5);prey=Prey(game,Species.ArmoredBeetle,new Vec(7.5,4.5),false);var oil=new Hazard(HazardType.Oil,6.5,4.5);game.Hazards.Add(oil);
                game.Shots.Add(new Shot{Position=new Vec(5.8,4.5),Direction=new Vec(1,0),Friendly=true,Fire=true,Speed=80,Damage=10});game.Step(.05,new Controls());Check(prey.HP==prey.MaxHP&&!oil.Triggered&&game.Shots.Count==0,"Shot interacted through solid stone");
            });
            Test("Devour requires sight around wall corners and keeps a valid channel target",()=>{
                var game=Start();game.Player=new Vec(5.7,4.8);var rat=Prey(game,Species.Rat,new Vec(6.5,5.3));Check(Vec.Distance(game.Player,rat.Position)<1.05&&!game.Sight(game.Player,rat.Position),"Wall-corner fixture is invalid");
                Tick(game,1.4,new Controls{Devour=true});Check(rat.State!=Mind.Devoured,"Devoured through a wall corner");
                var barrel=new Hazard(HazardType.Oil,rat.Position.X,rat.Position.Y,true);game.Hazards.Add(barrel);game.Step(0,new Controls{Aim=rat.Position-game.Player,Slam=true});Check(!barrel.Triggered,"Slam smashed a barrel through stone");
                game=Start();rat=Prey(game,Species.Rat,game.Player+new Vec(.7,0));game.Step(.2,new Controls{Devour=true});var bat=Prey(game,Species.CaveBat,game.Player+new Vec(.3,0));
                Tick(game,1.1,new Controls{Devour=true});Check(rat.State==Mind.Devoured&&bat.State!=Mind.Devoured,"Closer prey stole the active channel");
            });
            Test("Dodge takes priority, does not stack movement and hits each prey once",()=>{
                var game=Start();var prey=Prey(game,Species.ArmoredBeetle,game.Player+new Vec(.8,0),false);var events=new List<string>();game.Effect+=(name,p)=>events.Add(name);Vec start=game.Player;
                game.Step(.2,new Controls{X=1,Aim=new Vec(1,0),Tackle=true,Slam=true,Bite=true});
                Check(Math.Abs(Vec.Distance(start,game.Player)-1.6)<.001,"Dodge stacked walking or overshot duration");Check(prey.HP==prey.MaxHP-game.Damage*1.3,"Dodge damaged prey more than once");Check(events.Contains("tackle")&&!events.Contains("slam")&&!events.Contains("bite"),"Input frame stacked attacks");
            });
            Test("A nearly ready dodge can be buffered and UI exposes real skill cooldown",()=>{
                var game=Start();game.TackleCooldown=.08;game.Step(.02,new Controls{Tackle=true,Aim=new Vec(1,0)});Check(game.TackleCooldown<.1,"Dodge fired before cooldown");game.Step(.08,new Controls());Check(game.TackleCooldown>1,"Buffered dodge was lost");
                game=Start();game.Stats.Eaten[(int)Species.ArmoredBeetle]=1;game.Stats.Essence=20;game.Step(0,new Controls{Skill=2});Check(game.SkillRemaining(2)==7,"HUD cooldown differs from rules");game.Step(.2,new Controls());Check(Math.Abs(game.SkillRemaining(2)-6.8)<.001,"Cooldown lost elapsed frame time");
            });
            Test("Substeps retain elapsed time, movement and single-press skill semantics",()=>{
                var a=Start();var b=Start();foreach(var game in new[]{a,b}){game.Stats.Eaten[2]=1;game.Stats.Essence=30;}
                a.Step(.2,new Controls{X=1,Aim=new Vec(1,0),Skill=1});for(int i=0;i<10;i++)b.Step(.02,new Controls{X=1,Aim=new Vec(1,0),Skill=i==0?1:0});
                Check(Math.Abs(a.Clock-.2)<.001&&Vec.Distance(a.Player,b.Player)<.001&&a.Stats.Essence==24&&a.Shots.Count==b.Shots.Count,"Frame grouping changed simulation");
                a.Pause();double clock=a.Clock;a.Step(.2,new Controls{Tackle=true});Check(a.Clock==clock,"Paused catch-up advanced the game");
            });
            Test("Damage and offensive actions interrupt Devour; hiding cancels an active dash",()=>{
                var game=Start();var rat=Prey(game,Species.Rat,game.Player+new Vec(.5,0));game.Step(.2,new Controls{Devour=true});Check(game.DevourTime>.1,"Channel did not start");
                game.Shots.Add(new Shot{Position=game.Player,Direction=new Vec(1,0),Damage=1,Speed=0});game.Step(.02,new Controls{Devour=true});Check(game.DevourTime==0,"Taking damage continued the channel");
                game.Step(.2,new Controls{Devour=true});game.Step(0,new Controls{Bite=true,Devour=true,Aim=new Vec(1,0)});Check(game.DevourTime==0,"Biting continued the channel");
                game.Step(.2,new Controls{Devour=true});game.Stats.Eaten[2]=1;game.Stats.Essence=20;game.Step(0,new Controls{Skill=1,Devour=true});Check(game.DevourTime==0,"Offensive spell continued the channel");
                game=Start();game.Step(.04,new Controls{Tackle=true,Aim=new Vec(1,0)});game.HideSpots.Add(game.Player);game.Step(.02,new Controls{Hide=true});Vec hidden=game.Player;game.Step(.04,new Controls());Check(Vec.Distance(hidden,game.Player)<.001,"Dash resumed after leaving a hiding spot");
            });
            Test("Invalid checkpoint metadata is rejected",()=>{
                var game=new Rules();Check(!game.Continue(new Progress{Floor=3}),"Invalid floor accepted");Check(!game.Continue(new Progress{HP=double.PositiveInfinity}),"Invalid HP accepted");Check(!game.Continue(new Progress{Eaten=null}),"Invalid traits accepted");
            });
            Console.WriteLine(passed+" checks passed. Unity Editor import, rendering, audio and builds were not run.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("FAIL "+e.Message);return 1;}
    }
}
