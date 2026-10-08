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
