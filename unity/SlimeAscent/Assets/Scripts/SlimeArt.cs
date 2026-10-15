using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeAscent
{
    // Atlas cells are authored from the top-left; Unity sprite rects start bottom-left.
    public sealed class SlimeArt : IDisposable
    {
        public const int Cell=64;
        public const int Idle=0,Walk=1,Bite=2,Slam=3,Hurt=4,Devour=5;
        public readonly Sprite[,,,] Slimes=new Sprite[4,4,6,4];
        public readonly Sprite[,,] Enemies=new Sprite[7,4,4];
        public readonly Sprite[,] Tiles=new Sprite[3,12];
        public readonly Sprite[] Props=new Sprite[32];
        public readonly Sprite[,] Effects=new Sprite[12,4];
        public readonly Sprite[,] Humans=new Sprite[5,6];
        [Serializable] private sealed class HumanLayout
        {public int width,height;public string[] classes;public int[] columns,rowEdges,feet,bodyCenters,pixelsPerUnit;public FrameAdjust[] adjustments;}
        [Serializable] private sealed class FrameAdjust {public int role,pose,left,right;public ClipBand[] bands;}
        [Serializable] private sealed class ClipBand {public int left,right,top,bottom;}
        public readonly Texture2D Title,Ending;
        private readonly List<Sprite> owned=new List<Sprite>();
        public SlimeArt()
        {
            var slimes=Texture("slime",1536,1024);
            for(int form=0;form<4;form++)for(int dir=0;dir<4;dir++)for(int action=0;action<6;action++)for(int frame=0;frame<4;frame++)
                Slimes[form,dir,action,frame]=Slice(slimes,action*4+frame,form*4+dir,48);
            var enemies=Texture("enemies",256,1792);
            for(int species=0;species<7;species++)for(int dir=0;dir<4;dir++)for(int frame=0;frame<4;frame++)
                Enemies[species,dir,frame]=Slice(enemies,frame,species*4+dir,species==6?32:48);
            var tiles=Texture("tiles",768,192);
            for(int floor=0;floor<3;floor++)for(int variant=0;variant<12;variant++)Tiles[floor,variant]=Slice(tiles,variant,floor,64);
            var props=Texture("props",512,256);
            for(int i=0;i<32;i++)Props[i]=Slice(props,i%8,i/8,i==6||i==7?32:i==9?24:48);
            var effects=Texture("effects",768,256);
            for(int effect=0;effect<12;effect++)for(int frame=0;frame<4;frame++)Effects[effect,frame]=Slice(effects,effect%3*4+frame,effect/3,32);
            Title=Texture("title");Ending=Texture("ending");
            var layoutAsset=Resources.Load<TextAsset>("Art/humans-layout");
            if(layoutAsset==null)throw new InvalidOperationException("Missing human sprite layout. Download the complete updated project.");
            var layout=JsonUtility.FromJson<HumanLayout>(layoutAsset.text);
            if(layout==null||layout.columns==null||layout.columns.Length!=7||layout.rowEdges==null||layout.rowEdges.Length!=6||layout.feet==null||layout.feet.Length!=5||layout.bodyCenters==null||layout.bodyCenters.Length!=6||layout.pixelsPerUnit==null||layout.pixelsPerUnit.Length!=5)throw new InvalidOperationException("Invalid human sprite layout.");
            var humans=Texture("humans",layout.width,layout.height);
            for(int role=0;role<5;role++)for(int pose=0;pose<6;pose++)
            {
                FrameAdjust adjust=layout.adjustments==null?null:Array.Find(layout.adjustments,a=>a.role==role&&a.pose==pose);
                int left=adjust==null?layout.columns[pose]:adjust.left,right=adjust==null?layout.columns[pose+1]:adjust.right;
                float w=right-left,h=layout.rowEdges[role+1]-layout.rowEdges[role];
                var rect=new Rect(left,humans.height-layout.rowEdges[role+1],w,h);
                var pivot=new Vector2((layout.bodyCenters[pose]-left)/w,(layout.rowEdges[role+1]-layout.feet[role])/h);
                var sprite=Sprite.Create(humans,rect,pivot,layout.pixelsPerUnit[role],0,SpriteMeshType.FullRect);
                // Two sword poses overlap horizontally in the supplied-style sheet.
                // Separate their render geometry without altering the source PNG.
                if(adjust!=null&&adjust.bands!=null)
                {
                    var vertices=new List<Vector2>();var triangles=new List<ushort>();float ppu=layout.pixelsPerUnit[role];
                    foreach(var band in adjust.bands)
                    {
                        ushort index=(ushort)vertices.Count;
                        float x0=(band.left-layout.bodyCenters[pose])/ppu,x1=(band.right-layout.bodyCenters[pose])/ppu;
                        float y0=(layout.feet[role]-band.bottom)/ppu,y1=(layout.feet[role]-band.top)/ppu;
                        vertices.Add(new Vector2(x0,y0));vertices.Add(new Vector2(x1,y0));vertices.Add(new Vector2(x1,y1));vertices.Add(new Vector2(x0,y1));
                        triangles.Add(index);triangles.Add((ushort)(index+1));triangles.Add((ushort)(index+2));triangles.Add((ushort)(index+2));triangles.Add((ushort)(index+3));triangles.Add(index);
                    }
                    sprite.OverrideGeometry(vertices.ToArray(),triangles.ToArray());
                }
                owned.Add(sprite);Humans[role,pose]=sprite;
            }
        }
        private static Texture2D Texture(string name,int width=0,int height=0)
        {
            var texture=Resources.Load<Texture2D>("Art/"+name);
            if(texture==null)throw new InvalidOperationException("Missing Slime Ascent artwork: Resources/Art/"+name+".png. Download the complete updated project.");
            if(width>0 && (texture.width!=width||texture.height!=height))throw new InvalidOperationException("Atlas dimensions changed: "+name+". Disable non-power-of-two resizing and texture compression in its importer.");
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;return texture;
        }
        private Sprite Slice(Texture2D texture,int column,int row,float pixelsPerUnit)
        {
            var rect=new Rect(column*Cell,texture.height-(row+1)*Cell,Cell,Cell);
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),pixelsPerUnit,0,SpriteMeshType.FullRect);
            owned.Add(sprite);return sprite;
        }
        public void Dispose(){foreach(var sprite in owned)if(sprite!=null)UnityEngine.Object.Destroy(sprite);owned.Clear();}
    }
}
