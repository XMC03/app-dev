"""Original pixel sprites for Slime Ascent. Run with Python 3 + Pillow.
Draws every frame from geometry; it does not modify the illustrated backgrounds.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math
import json
import random

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'unity/SlimeAscent/Assets/Resources/Art'
OUT.mkdir(parents=True, exist_ok=True)
CELL = 64
ACTIONS = ['idle', 'walk', 'bite', 'slam', 'hurt', 'devour']
DIRECTIONS = ['up', 'right', 'down', 'left']
FORMS = ['awakening', 'fire_resistance', 'heat_immunity', 'flame_body']
SPECIES = ['rat', 'cave_bat', 'fire_lizard', 'armored_beetle', 'hero_warrior', 'hero_mage', 'guardian']

def blank(): return Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
def ellipse(d, box, fill, outline=None): d.ellipse(tuple(int(v) for v in box), fill=fill, outline=outline)
def poly(d, pts, color): d.polygon([(int(x), int(y)) for x, y in pts], fill=color)
def shadow(d, width=22): ellipse(d, (32-width, 45, 32+width, 53), (7, 13, 24, 95))
def diamond(d, x, y, r, color): poly(d, [(x,y-r),(x+r,y),(x,y+r),(x-r,y)], color)

def slime(form, direction, action, frame):
    im=blank(); d=ImageDraw.Draw(im); shadow(d)
    colors=[('#28684e','#52b57e','#9ae0ae','#ddf8cf'),('#697934','#a7c457','#e7e697','#fff3cc'),('#98482e','#dc874b','#ffd191','#fff2c8'),('#522f55','#a85765','#ef9c65','#ffe6a9')][form]
    dark,body,light,shine=colors
    pulse=[0,1,0,-1][frame]; w=22+pulse; h=14-pulse; cx=32; cy=35
    if action=='walk': cx += [0,1,0,-1][frame]; cy += [0,-1,0,1][frame]
    if action=='slam': w+=frame*2; h-=frame; cx += [0,2,4,1][frame]*(1 if direction==1 else -1 if direction==3 else 0); cy += [0,3,5,1][frame]*(1 if direction==2 else -1 if direction==0 else 0)
    if action=='hurt': cx += [-2,2,-1,1][frame]
    if action=='devour': w+=[0,2,4,1][frame]; h+=[0,1,2,0][frame]
    poly(d,[(cx-w,cy+5),(cx-w+3,cy-5),(cx-w+8,cy-h+3),(cx-7,cy-h),(cx+7,cy-h-1),(cx+w-7,cy-h+3),(cx+w-2,cy-5),(cx+w,cy+7),(cx+w-5,cy+h),(cx+7,cy+h+2),(cx-9,cy+h),(cx-w+3,cy+h-3)], dark)
    ellipse(d,(cx-w+3,cy-h+2,cx+w-3,cy+h-1),body)
    ellipse(d,(cx-w+7,cy-h+3,cx+w-7,cy+8),light)
    ellipse(d,(cx-w+10,cy-h+4,cx-3,cy-h+8),shine)
    d.rectangle((cx-w+5,cy-2,cx-w+6,cy+5),fill=shine)
    d.line((cx-w+5,cy+h-2,cx+w-5,cy+h-2),fill=body,width=2)
    if form>0:
        diamond(d,cx,cy+3,6+form,'#e2a447'); diamond(d,cx-1,cy+2,3,'#fff0a7')
    if form>1:
        for x,y in [(cx-13,cy+5),(cx+12,cy+4),(cx-9,cy-6)]: d.line((x,y,x+2,y+3,x,y+4),fill='#ffdca2',width=1)
    if form==3:
        poly(d,[(cx-16,cy-7),(cx-22,cy-22),(cx-11,cy-13)],'#33273d'); poly(d,[(cx-17,cy-10),(cx-21,cy-20),(cx-14,cy-13)],'#d6ba84')
        poly(d,[(cx+16,cy-7),(cx+22,cy-22),(cx+11,cy-13)],'#33273d'); poly(d,[(cx+17,cy-10),(cx+21,cy-20),(cx+14,cy-13)],'#d6ba84')
    eyes={0:[],1:[(cx+8,cy-2),(cx+17,cy-3)],2:[(cx-7,cy+1),(cx+7,cy+1)],3:[(cx-17,cy-3),(cx-8,cy-2)]}[direction]
    for ex,ey in eyes:
        ellipse(d,(ex-3,ey-3,ex+3,ey+3),'#16342d'); d.rectangle((ex-1,ey-1,ex+1,ey+1),fill='#f9e3a4')
        if action=='idle' and frame==3:d.line((ex-3,ey,ex+3,ey),fill=dark,width=2)
    if direction==0: d.line((cx-6,cy-h+5,cx+3,cy-h+4),fill=shine,width=2)
    if action in ('bite','devour') and direction!=0:
        mx=cx+(14 if direction==1 else -14 if direction==3 else 0); my=cy+(7 if direction==2 else 3)
        ellipse(d,(mx-5, my-2, mx+5, my+3+frame),'#18312a')
        if action=='bite':d.rectangle((mx-3,my-1,mx-1,my+1),fill='#edf4d4');d.rectangle((mx+2,my-1,mx+3,my+1),fill='#edf4d4')
        else:diamond(d,mx,my+1,2,'#b2f4c4')
    if action=='hurt': d.line((cx-10,cy-6,cx-5,cy-2),fill='#ffc8bd',width=2)
    for x,y in [(cx+10,cy+8),(cx-12,cy+6)]:ellipse(d,(x,y,x+2,y+2),body)
    return im

def enemy(kind,direction,frame):
    im=blank(); d=ImageDraw.Draw(im); shadow(d,24 if kind==6 else 20)
    b=[0,-1,0,1][frame]; cy=32+b; cx=32
    if kind==0:
        d.line((46,39,55,44,58,39,57,34),fill='#926b75',width=3)
        ellipse(d,(15,23+b,47,45+b),'#655c51'); ellipse(d,(17,23+b,45,41+b),'#a89a7b');ellipse(d,(20,25+b,40,36+b),'#cfbfa0')
        for x in (21,41):ellipse(d,(x-5,19+b,x+5,29+b),'#c99b9a');ellipse(d,(x-3,21+b,x+3,27+b),'#895b68')
        poly(d,[(26,36+b),(38,36+b),(37,48+b),(32,51+b),(27,47+b)],'#bdb08e');ellipse(d,(30,46+b,34,50+b),'#4d3846')
        for x in (19,42):d.rectangle((x,43+b,x+4,47+b),fill='#655c51')
    elif kind==1:
        flap=[-7,-1,7,-1][frame]
        for sign in (-1,1):poly(d,[(32+sign*7,29),(32+sign*27,17+flap),(32+sign*25,36+flap),(32+sign*19,32+flap),(32+sign*13,39),(32+sign*5,35)],'#4f355b');d.line((32+sign*8,30,32+sign*24,23+flap),fill='#a170a9',width=2)
        ellipse(d,(22,22+b,42,44+b),'#79518b');poly(d,[(23,26+b),(21,13+b),(29,21+b)],'#a575a6');poly(d,[(41,26+b),(43,13+b),(35,21+b)],'#a575a6')
        d.rectangle((28,39+b,29,44+b),fill='#eadcca');d.rectangle((35,39+b,36,44+b),fill='#eadcca')
    elif kind==2:
        d.line((39,37,48,45,55,43,57,34),fill='#713e44',width=7);d.line((40,36,48,42,54,40),fill='#d78b50',width=3)
        ellipse(d,(18,21+b,44,45+b),'#8b4140');ellipse(d,(20,21+b,42,40+b),'#ce7246');poly(d,[(23,36+b),(34,36+b),(38,47+b),(26,49+b),(20,44+b)],'#e1a060')
        for y in (17,23,29):poly(d,[(22,y),(17,y-4),(19,y+7)],'#e7ae59')
        for x in (16,42):d.line((x,33+b,x-2,40+b,x+3,44+b),fill='#cc8355',width=3)
        diamond(d,30,29+b,3,'#ffda87')
    elif kind==3:
        for sign in (-1,1):
            for y in (24,32,40):d.line((32+sign*13,y,32+sign*22,y-4+frame%2*2,32+sign*24,y+2),fill='#455457',width=3)
        ellipse(d,(16,15+b,48,47+b),'#243941');ellipse(d,(19,17+b,45,44+b),'#457f83');ellipse(d,(22,19+b,42,40+b),'#6bb3b0');d.line((32,18+b,32,42+b),fill='#2c565e',width=2)
        d.line((24,23+b,28,20+b),fill='#c6e3cc',width=2);ellipse(d,(25,42+b,39,51+b),'#35515a');d.line((29,46+b,27,51+b),fill='#dbe3b6',width=1)
    elif kind==4:
        poly(d,[(18,29+b),(46,29+b),(47,51+b),(32,45+b),(17,51+b)],'#923f45')
        for x in (23,36):d.rectangle((x,43+b+(frame%2)*2,x+6,52+b),fill='#3d4656')
        d.rectangle((21,28+b,43,44+b),fill='#536678');d.rectangle((24,29+b,40,38+b),fill='#98afbc');d.line((24,33+b,40,33+b),fill='#d3dccf',width=2)
        ellipse(d,(22,13+b,42,31+b),'#536371');d.rectangle((23,14+b,41,23+b),fill='#b3c1c4');d.rectangle((25,24+b,39,29+b),fill='#313d4e');d.line((26,26+b,38,26+b),fill='#e9d5a5',width=1)
        d.rectangle((31,7+b,34,16+b),fill='#b66057')
        poly(d,[(14,30+b),(23,30+b),(24,42+b),(19,48+b),(13,41+b)],'#ccb484');poly(d,[(15,32+b),(21,32+b),(21,41+b),(18,44+b),(15,39+b)],'#687c85')
        d.line((47,23+b,47,45+b),fill='#dce4d9',width=3);d.line((42,40+b,51,40+b),fill='#d5b874',width=2)
    elif kind==5:
        poly(d,[(26,25+b),(39,25+b),(47,49+b),(18,49+b)],'#3d3359');poly(d,[(29,24+b),(37,24+b),(42,46+b),(23,46+b)],'#785496');d.line((32,30+b,32,47+b),fill='#ba8dba',width=2)
        poly(d,[(19,27+b),(24,15+b),(35,8+b),(45,28+b)],'#563b78');poly(d,[(26,24+b),(35,15+b),(39,27+b)],'#211f39');d.rectangle((29,23+b,37,27+b),fill='#c4a18f')
        d.line((48,19,48,51),fill='#977250',width=3);diamond(d,48,15,7,'#715195');diamond(d,48,14,4,'#dcc0ee');d.rectangle((22,49,28,53),fill='#382e49');d.rectangle((36,49,42,53),fill='#382e49')
    else:
        for x in (16,37):d.rectangle((x,40+b,x+11,58+b),fill='#28283f');d.rectangle((x+1,43+b,x+10,51+b),fill='#606079');d.rectangle((x-2,55+b,x+13,59+b),fill='#39364e')
        poly(d,[(15,20+b),(49,20+b),(47,44+b),(18,44+b)],'#26263d');poly(d,[(20,22+b),(44,22+b),(42,40+b),(22,40+b)],'#56546b')
        for x in (8,43):d.rectangle((x,23+b,x+13,41+b),fill='#3a374f');poly(d,[(x-1,25+b),(x+6,15+b),(x+13,25+b)],'#797184');d.line((x+3,30+b,x+10,30+b),fill='#b093a4',width=2)
        poly(d,[(21,22+b),(23,7+b),(40,7+b),(43,23+b)],'#333248');d.rectangle((25,11+b,39,22+b),fill='#6c6479');d.rectangle((27,17+b,37,19+b),fill='#d5a8ef')
        poly(d,[(23,12+b),(17,3+b),(20,15+b)],'#afa28d');poly(d,[(40,12+b),(47,3+b),(43,15+b)],'#afa28d')
        if direction!=0:
            core_x=32+(4 if direction==1 else -4 if direction==3 else 0)
            diamond(d,core_x,32+b,9,'#201f36');diamond(d,core_x,31+b,6,'#966ab7');diamond(d,core_x-1,29+b,3,'#ecd4ff')
    # Directional eyes make poses distinguishable without rotating the sprite plane.
    if direction==0:
        if kind==4:d.rectangle((24,24+b,40,29+b),fill='#718593')
        if kind==6:d.rectangle((27,17+b,37,19+b),fill='#6c6479')
        d.line((25,23+b,39,23+b),fill='#8f819b' if kind in (1,5,6) else '#b6aa91',width=2)
    elif kind in (4,6):
        left,right=(33,39) if direction==1 else (25,31) if direction==3 else (26,38)
        y=26+b if kind==4 else 18+b
        d.rectangle((25,y-1,39,y+1),fill='#313d4e' if kind==4 else '#333248')
        d.line((left,y,right,y),fill='#e9d5a5' if kind==4 else '#e5bdff',width=1)
    elif kind not in (4,6):
        eyes=[(39,31+b)] if direction==1 else [(24,31+b)] if direction==3 else [(27,34+b),(36,34+b)]
        for x,y in eyes:ellipse(d,(x-2,y-2,x+2,y+2),'#272b35');d.point((x,y),fill='#f7df93')
    return im

THEMES=[('#26363a','#34474a','#4f6360','#77847b','#9fc19d'),('#34323b','#4b4750','#66606b','#908b8a','#c2ae90'),('#242234','#373145','#51425c','#80718d','#bb93d4')]

def tile(theme,index):
    im=blank();d=ImageDraw.Draw(im);p=THEMES[theme];rng=random.Random(500+theme*53+index)
    d.rectangle((0,0,63,63),fill=p[0])
    if index<4:
        for y in (0,32):
            for x in (0,32):d.rectangle((x+1,y+1,x+30,y+30),fill=p[1]);d.line((x+2,y+2,x+28,y+2),fill=p[2],width=1);d.line((x+30,y+3,x+30,y+30),fill=p[0],width=1)
        for _ in range(24):x,y=rng.randrange(4,60),rng.randrange(4,60);d.rectangle((x,y,x+1,y+1),fill=p[2])
        if index==1:d.line((8,10,20,15,18,27,29,36,22,48),fill=p[0],width=2)
        if index==2:
            for _ in range(9):x,y=rng.randrange(8,53),rng.randrange(7,53);ellipse(d,(x,y,x+4,y+2),p[3] if theme else '#4f765d')
        if index==3 and theme==2:diamond(d,32,32,14,p[2]);diamond(d,32,32,9,p[1]);d.line((32,22,32,42),fill=p[4],width=1)
    elif index<9:
        d.rectangle((1,2,62,59),fill=p[1]);d.rectangle((3,3,60,9),fill=p[3]);d.rectangle((3,10,60,15),fill=p[2])
        for y in (17,31,45):
            d.line((2,y,61,y),fill=p[0],width=2)
            for x in range(0 if y==31 else 16,64,32):d.line((x,y-12,x,y),fill=p[0],width=2)
        d.rectangle((2,56,61,62),fill=p[0]);d.line((3,55,60,55),fill=p[2],width=1)
        if theme==0:
            for x,y in [(7,12),(9,15),(51,48),(55,53)]:d.rectangle((x,y,x+6,y+3),fill='#446853')
        if theme==2:diamond(d,31,31,10,p[0]);d.line((31,23,31,38),fill=p[4],width=1)
    elif index==9:
        ellipse(d,(7,47,58,62),(8,11,21,140));d.rectangle((13,10,51,54),fill=p[0]);d.rectangle((17,8,47,51),fill=p[2]);d.rectangle((12,8,52,17),fill=p[3]);d.rectangle((11,49,53,55),fill=p[1]);d.line((23,19,23,47),fill=p[3],width=2)
    elif index==10:
        d.rectangle((0,0,63,63),fill=p[1]);d.rectangle((3,3,60,60),outline=p[2],width=3)
        if theme==0:
            for _ in range(14):x,y=rng.randrange(6,52),rng.randrange(6,52);ellipse(d,(x,y,x+8,y+6),'#476d55')
        elif theme==1:d.rectangle((8,6,55,57),fill='#624b57');d.rectangle((12,10,51,53),outline='#b59a70',width=2)
        else:diamond(d,32,32,24,p[2]);diamond(d,32,32,16,p[4]);diamond(d,32,32,12,p[1])
    else:
        for _ in range(14):x,y=rng.randrange(5,52),rng.randrange(8,51);d.rectangle((x,y,x+7,y+3),fill=p[2]);d.line((x,y,x+5,y),fill=p[3],width=1)
    return im

def prop(index):
    im=blank();d=ImageDraw.Draw(im)
    if index in (0,30):
        shadow(d,17);d.rounded_rectangle((17,10,47,50),radius=7,fill='#563f39',outline='#aa8659',width=2)
        for x in (22,30,39):d.line((x,15,x,47),fill='#896046',width=2)
        for y in (18,38):d.rectangle((16,y,48,y+5),fill='#72746c');d.line((18,y,46,y),fill='#bcb394',width=1)
        ellipse(d,(18,8,46,17),'#947651');ellipse(d,(22,10,42,14),'#403933');d.rectangle((28,24,36,31),fill='#82ac7b' if index==30 else '#dba85c')
    elif index in (1,2,21):
        c='#242830' if index==1 else '#516e51' if index==2 else '#325b63';h='#525454' if index==1 else '#a9cc7e' if index==2 else '#76a9a2'
        ellipse(d,(3,13,58,51),c);ellipse(d,(12,10,47,49),c);d.arc((9,18,50,41),200,310,fill=h,width=2);ellipse(d,(40,31,49,35),h)
        if index==2:
            for x,y in [(20,21),(35,29),(23,36)]:ellipse(d,(x,y,x+4,y+4),h)
    elif index==3:
        d.rounded_rectangle((7,11,57,54),radius=8,fill='#284138',outline='#6c9071',width=2);poly(d,[(30,12),(38,24),(29,35),(35,43),(28,53),(24,39),(30,30),(25,19)],'#142928');d.line((28,17,31,27,26,37,29,48),fill='#a4e3a7',width=2)
    elif index==4:
        d.rectangle((8,10,56,53),fill='#444147',outline='#ad9570',width=3);d.rectangle((14,16,50,46),fill='#696052',outline='#c7b887',width=1);diamond(d,32,31,12,'#aa895b');diamond(d,32,31,8,'#615346');d.rectangle((49,8,52,24),fill='#d2b97e')
    elif index in (5,14,18,26):
        shadow(d,21)
        for x,y,w,h in [(8,36,15,12),(23,26,18,19),(38,37,19,11)]:poly(d,[(x,y),(x+w-3,y-4),(x+w,y+h-2),(x+3,y+h)],'#5a6071');d.line((x+2,y,x+w-5,y-2),fill='#96949b',width=2)
        if index==18:d.line((15,25,45,42),fill='#c7bea4',width=3);ellipse(d,(24,24,40,38),'#c7bea4');d.rectangle((27,29,30,32),fill='#4a4650');d.rectangle((35,29,38,32),fill='#4a4650')
        if index==26:d.rectangle((19,9,44,39),fill='#73727c');d.line((31,17,31,29),fill='#a8a392',width=2);d.line((25,23,37,23),fill='#a8a392',width=2)
    elif index in (6,7):
        shadow(d,26)
        d.rectangle((7,12,15,56),fill='#49405c');d.rectangle((49,12,57,56),fill='#49405c');poly(d,[(7,14),(15,3),(49,3),(57,14)],'#8b7b9c');d.rectangle((11,15,15,53),fill='#b6a0c7');d.rectangle((49,15,53,53),fill='#b6a0c7');d.rectangle((8,54,56,60),fill='#72607f')
        if index==6:
            ellipse(d,(18,8,47,54),(111,70,163,120));ellipse(d,(23,13,42,50),(184,133,225,130));d.arc((17,7,48,55),110,300,fill='#dac1f2',width=2);diamond(d,32,4,3,'#ebd89b')
        else:
            for x in (23,31,39):d.rectangle((x,13,x+3,52),fill='#655575')
    elif index==8:
        for r,a in [(17,20),(12,35),(8,60)]:ellipse(d,(32-r,32-r,32+r,32+r),(167,126,220,a))
        diamond(d,32,31,13,'#322a51');diamond(d,32,30,10,'#a075cb');diamond(d,30,27,5,'#f2daff');d.line((26,37,36,24),fill='#dcbef3',width=2)
    elif index in (9,24):
        shadow(d,25);d.rectangle((12,28,52,57),fill='#b8a276');d.rectangle((27,40,37,57),fill='#534449');d.rectangle((16,35,24,44),fill='#6e8f9b');d.rectangle((40,35,48,44),fill='#6e8f9b');poly(d,[(5,31),(31,7),(59,31)],'#814c4e');poly(d,[(10,27),(31,12),(53,27)],'#b57461')
        for y in (20,26):d.line((14,y,50,y),fill='#d69a76',width=1)
    elif index==10:
        for x,y in [(17,39),(31,31),(46,43)]:d.rectangle((x,y,x+3,y+11),fill='#c7caaa');ellipse(d,(x-7,y-7,x+9,y+3),'#467e77');ellipse(d,(x-5,y-7,x+6,y-1),'#9ad0af');d.point((x-2,y-5),fill='#e6ebbb')
    elif index in (11,16):
        shadow(d,14);d.rectangle((27,35,36,54),fill='#756247');d.rectangle((19,31,45,39),fill='#5b5661');poly(d,[(22,31),(21,22),(29,26),(32,10),(39,24),(42,30)],'#ce764d');poly(d,[(27,31),(30,22),(33,18),(38,31)],'#ffe3a3')
    elif index==12:
        shadow(d,23);d.rectangle((9,16,55,52),fill='#76523d',outline='#c59e68',width=3);d.line((12,19,52,49),fill='#c59e68',width=3);d.line((12,49,52,19),fill='#bd905a',width=3)
    elif index in (13,19):
        d.rectangle((29,6,32,59),fill='#786a61');poly(d,[(12,10),(49,10),(49,36),(30,46),(12,36)],'#754152');poly(d,[(17,14),(44,14),(44,33),(30,41),(17,33)],'#985f63');diamond(d,30,25,7,'#dfbd87')
    elif index in (15,27):
        shadow(d,24);d.rectangle((12,43,52,56),fill='#605469');d.rectangle((18,37,46,44),fill='#938496');poly(d,[(21,35),(20,21),(26,15),(25,7),(38,7),(40,16),(46,22),(44,35)],'#695e78');d.line((27,11,27,33),fill='#afa0ad',width=2);diamond(d,33,22,5,'#b78acb')
    elif index==17:
        ellipse(d,(9,9,55,55),(90,59,121,100));d.ellipse((13,13,51,51),outline='#bb99d3',width=2);diamond(d,32,32,12,'#67527d');d.line((32,20,32,44),fill='#dbc3e8',width=2)
    elif index in (20,29):
        poly(d,[(5,53),(27,13),(51,43),(59,54)],'#80694f');poly(d,[(6,51),(27,18),(31,53)],'#c8b18d');poly(d,[(28,20),(35,53),(50,44)],'#504b4b');d.line((27,13,31,55),fill='#e4ce99',width=2)
    elif index==22:
        for x in (15,35,44):d.line((x,5,x-4,24,x+7,37,x+3,57),fill='#4b684e',width=4);d.line((x,7,x-2,23,x+9,36),fill='#7c9870',width=1)
    elif index==23:
        for x,y in [(18,36),(33,30),(46,42)]:poly(d,[(x-6,y),(x-3,y-22),(x+3,y-27),(x+7,y-3)],'#7f5ba2');d.line((x,y-21,x+2,y-4),fill='#d5b6e7',width=2)
    elif index in (25,28):
        for x in (15,21,35,43):d.line((x,52,x+2,25),fill='#4f7854',width=2);poly(d,[(x+1,30),(x-4,37),(x+2,45)],'#86aa6c')
    else:diamond(d,32,32,3,'#f0dcaa')
    return im

def fx(effect,frame):
    im=blank();d=ImageDraw.Draw(im);f=frame;cx=cy=32
    if effect==0:
        for off,height in [(-14,16),(-5,30),(8,25),(17,12)]:poly(d,[(cx+off-8,53),(cx+off-5,39),(cx+off,53-height-f%2*4),(cx+off+6,36),(cx+off+9,53)],(236,109,60,190));poly(d,[(cx+off-4,52),(cx+off,41-f%2*3),(cx+off+4,52)],(255,211,108,225))
    elif effect==1:
        for x,y,r in [(21+f,33,13),(40-f,31,12),(31,42-f,14)]:ellipse(d,(x-r,y-r,x+r,y+r),(132,187,115,75));d.arc((x-r+3,y-r+3,x+r-3,y+r-3),170,275,fill=(212,224,150,120),width=2)
    elif effect in (2,6,9):
        r=12+f*6;color=(245,185,115,220-f*35) if effect==2 else (209,148,248,200-f*30) if effect==6 else (236,133,115,220)
        d.ellipse((cx-r,cy-r*(1 if effect==9 else .55),cx+r,cy+r*(1 if effect==9 else .55)),outline=color,width=2)
        if effect==9:
            for i in range(8):a=i*math.pi/4;d.line((cx+math.cos(a)*(r-5),cy+math.sin(a)*(r-5),cx+math.cos(a)*r,cy+math.sin(a)*r),fill=color,width=2)
    elif effect==3:
        d.arc((7+f*2,8,58,58-f*2),215,340,fill=(241,235,199,220-f*35),width=3);d.arc((15,14,52,54),225,330,fill=(153,225,169,170),width=1)
    elif effect==4:
        for i in range(7):a=i*math.pi*2/7+f*.4;r=23-f*5;diamond(d,cx+math.cos(a)*r,cy+math.sin(a)*r,3,(182,243,193,230-f*25))
    elif effect==5:
        d.ellipse((10,4,54,60),outline=(176,134,224,160),width=2);d.arc((14,10,50,56),f*70,f*70+160,fill=(239,211,255,230),width=3)
    elif effect==7:
        d.ellipse((6,26,58,56),outline=(246,177,83,160),width=2)
        for off in (-18,-6,7,19):poly(d,[(32+off-3,38),(32+off,24-f%2*5),(32+off+4,38)],(247,193,115,190))
    elif effect==8:
        for r,alpha in [(13,20),(9,35),(6,95)]:ellipse(d,(32-r,32-r,32+r,32+r),(228,151,92,alpha))
        diamond(d,32,32,5,'#f9d794');diamond(d,31,31,2,'#fff6cb')
    elif effect==10:
        poly(d,[(14,12),(50,12),(51,38),(32,57),(13,38)],(148,188,227,95));d.line((14,12,50,12,51,38,32,57,13,38,14,12),fill=(210,233,244,200),width=2)
    else:
        r=9+f*7;d.ellipse((32-r,32-r*.7,32+r,32+r*.7),outline=(166,213,229,200-f*35),width=2)
    return im

def atlas(name,cols,rows,make):
    im=Image.new('RGBA',(cols*CELL,rows*CELL))
    for row in range(rows):
        for col in range(cols):im.alpha_composite(make(col,row),(col*CELL,row*CELL))
    im.save(OUT/f'{name}.png',optimize=True)

atlas('slime',24,16,lambda c,r:slime(r//4,r%4,c//4,c%4))
atlas('enemies',4,28,lambda c,r:enemy(r//4,r%4,c))
atlas('tiles',12,3,lambda c,r:tile(r,c))
atlas('props',8,4,lambda c,r:prop(r*8+c))
atlas('effects',12,4,lambda c,r:fx(r*3+c//4,c%4))
manifest={'cell':64,'directions':DIRECTIONS,'forms':FORMS,'actions':ACTIONS,'species':SPECIES,'atlases':{'slime':[24,16],'enemies':[4,28],'tiles':[12,3],'props':[8,4],'effects':[12,4]},'effects':['fire','gas','slam','bite','essence','rift','boss_shockwave','flame_aura','projectile','telegraph','harden','echo']}
(OUT/'art-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
# Original preview board drawn from the same source functions, for art review.
preview=Image.new('RGBA',(896,640),'#14212b');d=ImageDraw.Draw(preview)
d.text((24,15),'SLIME ASCENT / CHARACTER & ENVIRONMENT STUDIES',fill='#e8cc89')
for i,name in enumerate(FORMS):preview.alpha_composite(slime(i,2,'idle',0),(32+i*170,60));d.text((25+i*170,132),name.replace('_',' '),fill='#dce8db')
for i,name in enumerate(SPECIES):preview.alpha_composite(enemy(i,2,0),(24+i*120,185));d.text((20+i*120,260),name.replace('_',' '),fill='#dce8db')
for row,name in enumerate(['LOWER / MOSS & WATER','MIDDLE / HERO CAMPS','UPPER / OBSIDIAN & RIFTS']):
    y=320+row*96;d.text((24,y+24),name,fill='#e8cc89')
    for col in range(6):preview.alpha_composite(tile(row,col),(260+col*64,y))
preview.convert('RGB').save(OUT/'art-preview.png',optimize=True)
print('Created five sprite atlases, art manifest and art preview.')
