using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nightfall;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
    private static int Main()
    {
        try
        {
            Test("All Unity C# files parse with the supported C# 9 syntax", () => {
                string project=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Nightfall"));
                foreach(string file in Directory.GetFiles(Path.Combine(project,"Assets"),"*.cs",SearchOption.AllDirectories)) {
                    var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.CSharp9));
                    foreach(var issue in syntax.GetDiagnostics())Check(issue.Severity!=DiagnosticSeverity.Error,file+": "+issue.ToString());
                }
            });
            Test("Start, normalized movement, pause and restart", () => {
                var game = new Campaign(); Check(game.State == RunState.Menu, "Missing start screen");
                game.Step(.04,1,0,false); Check(game.Player.X == 100, "Menu simulation advanced");
                game.PrimaryAction(); var start = game.Player; game.Step(.04,1,1,false);
                Check(Math.Abs(Point.Distance(start,game.Player)-7.2)<.001, "Diagonal movement gained speed");
                game.Pause(); double time=game.Elapsed; game.Step(.04,1,0,false); Check(game.Elapsed==time, "Pause advanced time");
                game.Pause(); game.RestartChapter(); Check(game.Hearts==3 && game.Collected==0 && game.Elapsed==0, "Restart failed");
            });
            Test("Collision prevents tunneling through trees and map bounds", () => {
                var game=new Campaign(); var tree=game.Trees[0];
                var result=game.Move(new Point(tree.X-40,tree.Y),80,0);
                Check(result.X<tree.X-35, "Movement tunneled through tree");
                result=game.Move(new Point(100,100),-10000,-10000);
                Check(result.X>=51 && result.Y>=51,"Player escaped the map");
            });
            Test("Damage protection, loss and chapter retry", () => {
                var game=new Campaign();game.PrimaryAction();game.Spirits[0].Position=game.Player;
                game.Step(0,0,0,false);Check(game.Hearts==2,"Contact did not cause damage");
                game.Step(0,0,0,false);Check(game.Hearts==2,"Protection did not prevent repeated hits");
                int frames=0;while(game.State==RunState.Playing && frames++<500)game.Step(.02,0,0,false);
                Check(game.State==RunState.Lost && game.Hearts==0,"Loss did not complete");
                game.PrimaryAction();Check(game.State==RunState.Playing && game.Hearts==3,"Retry failed");
            });
            Test("Dash exhaustion and release-to-recharge", () => {
                var game=new Campaign();game.PrimaryAction();
                for(int i=0;i<50;i++)game.Step(.04,1,0,true);
                Check(game.DashExhausted && game.Stamina>=0,"Dash did not exhaust");
                game.Step(.04,0,0,false);Check(!game.DashExhausted && game.Stamina>0,"Dash did not recharge");
            });
            Test("Enemy navigation goes around a blocking tree", () => {
                var game=new Campaign();game.PrimaryAction();var tree=game.Trees[0];
                game.Player=new Point(tree.X+80,tree.Y);var spirit=game.Spirits[0];spirit.Position=new Point(tree.X-40,tree.Y);
                Check(game.FindPath(spirit.Position,game.Player).Count>=3,"Route crossed solid tree");
                for(int i=0;i<400 && Point.Distance(spirit.Position,game.Player)>22;i++)game.Step(.02,0,0,false);
                Check(Point.Distance(spirit.Position,game.Player)<23,"Enemy got stuck at obstacle");
            });
            Test("Closed gates, crystal collection, chapter advance and victory", () => {
                var game=new Campaign();game.PrimaryAction();
                for(int level=0;level<3;level++) {
                    foreach(var target in new List<Point>(game.Crystals))Check(game.FindPath(game.Player,target).Count>0,"Unreachable crystal");
                    Check(game.FindPath(game.Player,game.Gate).Count>0,"Unreachable gate");
                    game.Player=game.Gate;game.Step(0,0,0,false);Check(game.State==RunState.Playing,"Locked gate allowed exit");
                    foreach(var target in new List<Point>(game.Crystals)){game.Player=target;game.Step(0,0,0,false);}
                    Check(game.GateOpen && game.Crystals.Count==0,"Crystals did not unlock gate");
                    game.Player=game.Gate;game.Step(0,0,0,false);
                    Check(game.State==(level==2?RunState.Won:RunState.Cleared),"Chapter did not finish");
                    game.PrimaryAction();Check(game.Hearts==3 && game.Stamina==1,"New chapter did not replenish supplies");
                }
                Check(game.Level==0 && game.State==RunState.Playing,"New campaign did not restart");
            });
            Test("Full three-chapter campaign with active enemies and directional input", () => {
                var game=new Campaign();game.PrimaryAction();
                for(int level=0;level<3;level++) {
                    int frames=0;
                    while(game.State==RunState.Playing && frames<20000) {
                        Point target=game.Gate;
                        if(!game.GateOpen) {
                            target=game.Crystals[0];int shortest=game.FindPath(game.Player,target).Count;
                            foreach(var crystal in game.Crystals){int size=game.FindPath(game.Player,crystal).Count;if(size<shortest){target=crystal;shortest=size;}}
                        }
                        foreach(var waypoint in game.FindPath(game.Player,target)) {
                            int steps=0;
                            while(Point.Distance(game.Player,waypoint)>3 && game.State==RunState.Playing && steps++<200) {
                                double x=waypoint.X-game.Player.X,y=waypoint.Y-game.Player.Y;
                                bool danger=false;foreach(var enemy in game.Spirits)if(Point.Distance(enemy.Position,game.Player)<160)danger=true;
                                game.Step(.02,Math.Abs(x)>2?Math.Sign(x):0,Math.Abs(y)>2?Math.Sign(y):0,danger&&!game.DashExhausted);frames++;
                            }
                            if(game.State!=RunState.Playing)break;
                        }
                    }
                    Check(game.State==(level==2?RunState.Won:RunState.Cleared),"Automated player did not survive glade "+(level+1));
                    Check(game.Collected==8 && game.Hearts>0,"Incomplete winning glade");
                    Console.WriteLine("  Glade "+(level+1)+": "+game.Elapsed.ToString("F2")+"s, "+game.Hearts+" hearts");
                    if(level<2)game.PrimaryAction();
                }
                Check(game.CampaignTime>0,"Campaign timer did not advance");
            });
            Console.WriteLine(passed+" checks passed; no Unity Editor tests were run."); return 0;
        }
        catch(Exception error) {Console.Error.WriteLine("FAIL "+error.Message);return 1;}
    }
}
