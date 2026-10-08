'use strict';

const canvas = document.getElementById('game');
const ctx = canvas.getContext('2d');
const $ = id => document.getElementById(id);
const WIDTH = 960, HEIGHT = 600, TILE = 40, COLS = 24, ROWS = 15;
const keys = new Set();
const clamp = (n, low, high) => Math.max(low, Math.min(high, n));
const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const chapters = [
  { name: 'The quiet grove', speed: 72, trees: [[5,3],[6,3],[10,2],[15,3],[19,4],[4,8],[5,8],[10,7],[11,7],[16,8],[20,11],[8,12],[13,11]], crystals: [[6,1],[12,2],[21,2],[2,11],[9,10],[16,5],[17,12],[21,8]] },
  { name: 'The whispering wood', speed: 84, trees: [[5,2],[5,3],[5,4],[10,4],[11,4],[16,2],[16,3],[19,6],[20,6],[4,9],[9,8],[9,9],[14,9],[15,9],[18,12],[6,12]], crystals: [[3,5],[8,2],[19,2],[2,12],[7,10],[13,6],[15,12],[21,10]] },
  { name: 'The last light', speed: 96, trees: [[4,3],[5,3],[8,2],[8,3],[13,3],[14,3],[18,3],[18,4],[3,8],[4,8],[8,7],[9,7],[13,8],[13,9],[18,9],[19,9],[7,12],[15,12]], crystals: [[2,5],[10,2],[20,3],[3,12],[7,9],[15,6],[20,12],[11,12]] }
];
function readStorage(key) { try { return localStorage.getItem(key); } catch { return null; } }
function writeStorage(key, value) { try { localStorage.setItem(key, value); } catch { /* Storage is optional, including file:// and private browsing. */ } }
function formatTime(seconds) { return `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`; }

class Sound {
  constructor() { this.enabled = readStorage('nightfall-sound') === 'on'; this.context = null; this.refresh(); }
  refresh() { $('sound').textContent = this.enabled ? 'Sound on' : 'Sound off'; $('sound').setAttribute('aria-pressed', String(this.enabled)); $('sound').setAttribute('aria-label', this.enabled ? 'Mute sound' : 'Enable sound'); }
  unlock() {
    if (!this.enabled) return;
    try {
      const Audio = window.AudioContext || window.webkitAudioContext;
      if (Audio && !this.context) this.context = new Audio();
      if (this.context?.state === 'suspended') this.context.resume().catch(() => {});
    } catch { /* Play remains available when audio is unavailable. */ }
  }
  play(kind) {
    if (!this.enabled || !this.context || this.context.state !== 'running') return;
    const notes = { crystal: [660,880], hurt: [170,110], gate: [440,660,880], dash: [250,400], win: [440,554,660,880] }[kind] || [440];
    const now = this.context.currentTime;
    notes.forEach((frequency, i) => {
      const oscillator = this.context.createOscillator(), gain = this.context.createGain();
      const start = now + i * .09;
      oscillator.type = kind === 'hurt' ? 'triangle' : 'sine'; oscillator.frequency.value = frequency;
      gain.gain.setValueAtTime(0, start); gain.gain.linearRampToValueAtTime(.07, start + .01); gain.gain.exponentialRampToValueAtTime(.001, start + .18);
      oscillator.connect(gain); gain.connect(this.context.destination); oscillator.start(start); oscillator.stop(start + .2);
    });
  }
}
const sound = new Sound();

class Game {
  constructor() {
    this.level = 0; this.total = 0; this.state = 'menu'; this.best = Number(readStorage('nightfall-best')) || 0;
    this.loadLevel(); this.showOverlay();
  }
  loadLevel() {
    this.player = { x: 100, y: 100, hp: 3, facing: 1 };
    this.trees = chapters[this.level].trees.map(([x,y],i) => ({ x:x*TILE+20, y:y*TILE+20, variant:i%3 }));
    this.blocked = new Set(chapters[this.level].trees.map(([x,y]) => y*COLS+x));
    this.crystals = chapters[this.level].crystals.map(([x,y]) => ({ x:x*TILE+20, y:y*TILE+20 }));
    this.gate = { x:900, y:100 };
    const starts = [[860,540],[100,540],[900,300],[500,540]];
    this.enemies = starts.slice(0, 2 + this.level).map(([x,y],i) => ({ x,y,phase:i*2,path:[],repath:0 }));
    this.elapsed = 0; this.collected = 0; this.invulnerable = 0; this.stamina = 1; this.dashExhausted = false; this.dashing = false; this.particles = []; this.shake = 0;
    keys.clear(); this.updateHud();
  }
  start() { sound.unlock(); this.state = 'playing'; this.hideOverlay(); canvas.focus({ preventScroll:true }); }
  primary() {
    if (this.state === 'paused') { this.start(); return; }
    if (this.state === 'cleared') { this.total += this.elapsed; this.level++; this.loadLevel(); this.start(); return; }
    if (this.state === 'won') { this.level = 0; this.total = 0; this.loadLevel(); }
    if (this.state === 'lost') this.loadLevel();
    this.start();
  }
  restart() { if(this.state === 'won') { this.level = 0; this.total = 0; } this.loadLevel(); this.start(); }
  pause() {
    if(this.state === 'playing') { this.state = 'paused'; keys.clear(); this.showOverlay(); }
    else if(this.state === 'paused') this.start();
  }
  finish(state) {
    this.state = state; keys.clear(); this.dashing = false;
    if(state === 'won') {
      const time = this.total + this.elapsed;
      if(!this.best || time < this.best) { this.best = time; writeStorage('nightfall-best', String(time)); }
    }
    sound.play(state === 'lost' ? 'hurt' : 'win'); this.showOverlay();
  }
  hideOverlay() { $('overlay').hidden = true; $('pause').disabled = false; $('pause').innerHTML = 'Pause <kbd>P</kbd>'; }
  showOverlay() {
    const messages = {
      menu: ['✦','A THREE CHAPTER ADVENTURE','A light in the dark.','Eight scattered crystals will open the forest gate. Gather them, evade the spirits, and cross three glades to get home.','Enter the forest'],
      paused: ['Ⅱ','TAKE A BREATH','The forest can wait.','Your adventure is paused. Come back when you’re ready.','Continue adventure'],
      lost: ['☾','THE NIGHT CAUGHT UP','A light worth saving.','The spirits found you. Your completed chapters are kept. Try this glade again — use your dash to create some space.','Try this chapter again'],
      cleared: ['✧','GATE CROSSED','One step closer.',`You crossed ${chapters[this.level].name.toLowerCase()} in ${formatTime(this.elapsed)}. Your hearts and dash are restored in the next glade.`,'Enter the next glade'],
      won: ['✦','ALL THREE GATES CROSSED','You found your way home.',`All 24 crystals gathered. All three glades crossed in ${formatTime(this.total + this.elapsed)}. The forest remembers your light.`,'Play a new adventure']
    };
    const [symbol, eyebrow, title, copy, action] = messages[this.state];
    $('modal-symbol').textContent = symbol; $('modal-eyebrow').textContent = eyebrow; $('modal-title').textContent = title; $('modal-copy').textContent = copy;
    $('primary').textContent = `${action} →`; $('intro-controls').hidden = this.state !== 'menu';
    $('best').textContent = this.best ? `BEST ADVENTURE · ${formatTime(this.best)}` : 'No downloads. Just a little courage.';
    $('overlay').hidden = false; $('pause').disabled = this.state !== 'paused'; $('pause').innerHTML = this.state === 'paused' ? 'Resume <kbd>P</kbd>' : 'Pause <kbd>P</kbd>';
    $('announcement').textContent = `${title} ${copy}`;
    if(this.state !== 'menu') $('primary').focus({ preventScroll:true });
  }
  walkable(x,y) { return x >= 1 && y >= 1 && x < COLS-1 && y < ROWS-1 && !this.blocked.has(y*COLS+x); }
  path(from, to) {
    const start = Math.floor(from.y/TILE)*COLS+Math.floor(from.x/TILE);
    let goal = Math.floor(to.y/TILE)*COLS+Math.floor(to.x/TILE);
    if(this.blocked.has(goal)) {
      let nearest = Infinity;
      for(let y=1;y<ROWS-1;y++)for(let x=1;x<COLS-1;x++)if(this.walkable(x,y)) {
        const d = Math.hypot(to.x-(x*TILE+20),to.y-(y*TILE+20));
        if(d<nearest) { nearest=d; goal=y*COLS+x; }
      }
    }
    if(start===goal) return [{x:to.x,y:to.y}];
    const queue=[start], previous=new Map([[start,null]]);
    for(let i=0;i<queue.length && !previous.has(goal);i++) {
      const id=queue[i], x=id%COLS, y=Math.floor(id/COLS);
      for(const [nx,ny] of [[x+1,y],[x-1,y],[x,y+1],[x,y-1]]) {
        const next=ny*COLS+nx;
        if(this.walkable(nx,ny) && !previous.has(next)) {previous.set(next,id);queue.push(next);}
      }
    }
    if(!previous.has(goal)) return [];
    const path=[];
    for(let id=goal;id!==start;id=previous.get(id)) path.unshift({x:(id%COLS)*TILE+20,y:Math.floor(id/COLS)*TILE+20});
    // First align to the current tile center so a turn never cuts through a tree.
    const center={x:(start%COLS)*TILE+20,y:Math.floor(start/COLS)*TILE+20};
    if(distance(from,center)>3) path.unshift(center);
    return path;
  }
  move(entity, dx, dy, radius=11) {
    const steps=Math.max(1,Math.ceil(Math.hypot(dx,dy)/6));
    for(let i=0;i<steps;i++) {
      const x=clamp(entity.x+dx/steps, TILE+radius, WIDTH-TILE-radius);
      if(!this.trees.some(t=>Math.hypot(t.x-x,t.y-entity.y)<radius+25)) entity.x=x;
      const y=clamp(entity.y+dy/steps, TILE+radius, HEIGHT-TILE-radius);
      if(!this.trees.some(t=>Math.hypot(t.x-entity.x,t.y-y)<radius+25)) entity.y=y;
    }
  }
  burst(x,y,color,count=12) {
    for(let i=0;i<count;i++) {const angle=i/count*Math.PI*2;this.particles.push({x,y,dx:Math.cos(angle)*55,dy:Math.sin(angle)*55,life:.6,color});}
  }
  update(dt) {
    if(this.state!=='playing') return;
    dt=clamp(dt,0,.04); this.elapsed+=dt; this.invulnerable=Math.max(0,this.invulnerable-dt); this.shake=Math.max(0,this.shake-dt*20);
    const dx=Number(keys.has('d')||keys.has('ArrowRight'))-Number(keys.has('a')||keys.has('ArrowLeft'));
    const dy=Number(keys.has('s')||keys.has('ArrowDown'))-Number(keys.has('w')||keys.has('ArrowUp'));
    const magnitude=Math.hypot(dx,dy), wantsDash=keys.has('Shift');
    if(!wantsDash) this.dashExhausted=false;
    const dash = wantsDash && magnitude>0 && this.stamina>0 && !this.dashExhausted;
    if(dash && !this.dashing) sound.play('dash'); this.dashing=dash;
    this.stamina=clamp(this.stamina+(dash?-.7:.28)*dt,0,1); if(this.stamina===0)this.dashExhausted=true;
    if(magnitude>0) {this.move(this.player,dx/magnitude*(dash?325:180)*dt,dy/magnitude*(dash?325:180)*dt);if(dx)this.player.facing=dx>0?1:-1;}
    this.crystals=this.crystals.filter(crystal=>{
      if(distance(crystal,this.player)>25)return true;
      this.collected++; this.burst(crystal.x,crystal.y,'#f3d492'); sound.play('crystal');
      $('announcement').textContent = `${this.collected} of 8 crystals collected.`;
      if(this.collected===8) {sound.play('gate');this.burst(this.gate.x,this.gate.y,'#a9e5c1',24);$('announcement').textContent='Gate open. Follow the arrow to the northeast gate.';}
      return false;
    });
    for(const enemy of this.enemies) {
      enemy.repath-=dt;
      // Keep an active route until its next waypoint is reached; replacing it
      // halfway through a tile would make a spirit repeatedly turn backwards.
      if(enemy.repath<=0 && enemy.path.length===0) {enemy.path=this.path(enemy,this.player).slice(0,2);enemy.repath=.25;}
      const target=enemy.path[0];
      if(target) {const d=distance(enemy,target),step=chapters[this.level].speed*dt;this.move(enemy,(target.x-enemy.x)/Math.max(d,1)*Math.min(d,step),(target.y-enemy.y)/Math.max(d,1)*Math.min(d,step));if(distance(enemy,target)<2)enemy.path.shift();}
      if(distance(enemy,this.player)<23 && this.invulnerable===0) {
        this.player.hp--;this.invulnerable=1.8;this.shake=5;this.burst(this.player.x,this.player.y,'#eaa38d');sound.play('hurt');
        $('announcement').textContent=`A spirit hit you. ${this.player.hp} hearts left.`;
        if(this.player.hp<=0) {this.finish('lost');break;}
      }
    }
    if(this.state==='playing' && this.collected===8 && distance(this.player,this.gate)<29) this.finish(this.level===2?'won':'cleared');
    for(const p of this.particles){p.x+=p.dx*dt;p.y+=p.dy*dt;p.life-=dt;}this.particles=this.particles.filter(p=>p.life>0);
    this.updateHud();
  }
  updateHud() {
    $('hearts').textContent = Array.from({length:3},(_,i)=>i<this.player.hp?'♥':'♡').join(' '); $('hearts').setAttribute('aria-label',`${this.player.hp} hearts`);
    $('crystals').textContent=`${this.collected} / 8`; $('time').textContent=formatTime(this.elapsed); $('dash-fill').style.width=`${this.stamina*100}%`;
    $('chapter-number').textContent=`0${this.level+1} / 03`; $('chapter-name').textContent=chapters[this.level].name;
    $('objective').textContent=this.collected===8?'GATE OPEN · HEAD TO THE NORTHEAST':'COLLECT THE CRYSTALS · REACH THE GATE';
    $('hint').textContent=this.collected===8?'The gate is open. Follow the light.':this.dashExhausted?'Release dash to let it recharge.':'The spirits can’t outrun you. Keep moving.';
  }
}
const game = new Game();

function rect(x,y,w,h,color) {ctx.fillStyle=color;ctx.fillRect(Math.round(x),Math.round(y),w,h);}
function circle(x,y,r,color) {ctx.fillStyle=color;ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.fill();}
function glow(x,y,r,color) {const gradient=ctx.createRadialGradient(x,y,0,x,y,r);gradient.addColorStop(0,color);gradient.addColorStop(1,'transparent');ctx.fillStyle=gradient;ctx.fillRect(x-r,y-r,r*2,r*2);}
function drawTree(t) {
  const x=t.x,y=t.y;
  ctx.fillStyle='#102e2680';ctx.beginPath();ctx.ellipse(x+9,y+17,32,17,0,0,Math.PI*2);ctx.fill();
  rect(x-4,y+9,9,20,'#62543d');rect(x+1,y+9,4,20,'#423f30');
  const shades=[['#274e3e','#3b6347','#55754f'],['#224738','#32583e','#476848'],['#2d5141','#406b4d','#5c8056']][t.variant];
  for(const [ox,oy,r] of [[0,-9,28],[-18,-2,19],[17,-3,20],[-8,-24,18],[10,-22,16]]){
    circle(x+ox,y+oy+3,r,shades[0]);circle(x+ox-2,y+oy-3,r-3,shades[1]);
    rect(x+ox-8,y+oy-12,8,4,shades[2]);rect(x+ox+4,y+oy-7,5,3,shades[2]);
  }
}
function drawPlayer() {
  const p=game.player,x=Math.round(p.x),y=Math.round(p.y),walk=(keys.size && game.state==='playing')?Math.sin(game.elapsed*16)*2:0;
  if(game.invulnerable>0 && Math.floor(game.elapsed*12)%2===0)return;
  if(game.dashing)glow(x,y,32,'#9bdebc44');
  ctx.fillStyle='#091d2360';ctx.beginPath();ctx.ellipse(x,y+12,13,6,0,0,Math.PI*2);ctx.fill();
  rect(x-7,y+4+walk,5,9,'#243138');rect(x+3,y+4-walk,5,9,'#243138');
  rect(x-10,y-5,20,13,'#7db3a0');rect(x-8,y-4,6,11,'#a9d0b3');rect(x+5,y-3,6,9,'#578a7b');
  rect(x-7,y-14,14,11,'#e3c49b');rect(x-8,y-17,16,6,'#49675c');rect(x-12,y-12,24,4,'#6d9380');
  rect(x+p.facing*5-1,y-7,2,2,'#27392f');rect(x-8,y-3,16,3,'#d2af70');
  const lx=x+p.facing*16,ly=y+3;glow(lx,ly,43,'#f7d08335');rect(lx-3,ly-4,6,9,'#a68650');rect(lx-2,ly-2,4,5,'#ffdc8c');
}
function drawSpirit(enemy) {
  const x=Math.round(enemy.x),y=Math.round(enemy.y+Math.sin(game.elapsed*3+enemy.phase)*3);
  glow(x,y,32,'#d68e9b22');circle(x,y+14,11,'#10272855');
  rect(x-9,y-8,18,20,'#ab7d91');rect(x-6,y-13,12,5,'#c59ba8');rect(x-12,y-4,6,13,'#875f7b');rect(x+7,y-4,5,13,'#875f7b');
  rect(x-8,y+10,5,6,'#ab7d91');rect(x+3,y+10,5,6,'#ab7d91');rect(x-5,y-3,3,4,'#fde0b0');rect(x+3,y-3,3,4,'#fde0b0');
}
function drawGate() {
  const {x,y}=game.gate,open=game.collected===8;
  glow(x,y,open?85:40,open?'#91dfad55':'#dac78c18');
  rect(x-24,y-35,9,56,'#5e7465');rect(x+15,y-35,9,56,'#5e7465');rect(x-24,y-38,48,10,'#82917b');
  rect(x-20,y-33,4,52,'#91a188');rect(x+16,y-33,4,52,'#91a188');rect(x-23,y+17,46,8,'#425949');
  if(open) {rect(x-14,y-28,28,45,'#a6ebbd44');rect(x-9,y-25,18,40,'#b6f0c54d');circle(x,y-45+Math.sin(game.elapsed*3)*3,3,'#e6deb0');}
  else {for(let i=-10;i<=10;i+=10)rect(x+i,y-28,3,42,'#526957');rect(x-14,y-8,28,4,'#82917b');}
}
function draw() {
  ctx.clearRect(0,0,WIDTH,HEIGHT);ctx.save();
  if(game.shake && !reducedMotion)ctx.translate(Math.sin(game.elapsed*80)*game.shake,Math.cos(game.elapsed*73)*game.shake);
  rect(0,0,WIDTH,HEIGHT,'#243e34');
  for(let y=0;y<ROWS;y++)for(let x=0;x<COLS;x++) {
    const seed=(x*73+y*137)%97;
    rect(x*TILE,y*TILE,TILE,TILE,['#263f35','#294237','#294439','#2b4538'][seed%4]);
    if(x===0||y===0||x===COLS-1||y===ROWS-1) {rect(x*TILE,y*TILE,TILE,TILE,'#1c332d');rect(x*TILE+4,y*TILE+8,30,24,'#2d4838');rect(x*TILE+7,y*TILE+6,24,4,'#3c5540');}
    else {rect(x*TILE+seed%28+5,y*TILE+seed%23+7,2,5,'#56725466');rect(x*TILE+(seed*3)%29+4,y*TILE+(seed*2)%25+5,3,2,'#97aa6a30');}
  }
  // A worn footpath and a shallow, decorative forest pool.
  ctx.strokeStyle='#75846522';ctx.lineWidth=30;ctx.lineCap='round';ctx.beginPath();ctx.moveTo(100,100);ctx.bezierCurveTo(230,240,680,150,900,100);ctx.stroke();
  const poolX=565,poolY=325;ctx.fillStyle='#1b3330';ctx.beginPath();ctx.ellipse(poolX,poolY,58,24,-.2,0,Math.PI*2);ctx.fill();ctx.fillStyle='#355751';ctx.beginPath();ctx.ellipse(poolX,poolY,47,16,-.2,0,Math.PI*2);ctx.fill();
  rect(poolX-26,poolY-4,26,2,'#6b938266');rect(poolX+7,poolY+5,18,2,'#6b938255');circle(poolX+27,poolY-4,5,'#6e8d5e');
  // Small trail stones and flowers add detail without affecting navigation.
  for(let i=0;i<22;i++){const x=65+(i*173)%830,y=70+(i*97)%460;if(!game.trees.some(t=>Math.hypot(t.x-x,t.y-y)<43)){rect(x,y,6,4,'#6b77635c');if(i%3===0){rect(x+10,y-4,2,7,'#67835b');rect(x+8,y-6,6,3,'#a6ab7866');}}}
  drawGate();
  for(const crystal of game.crystals) {
    const y=crystal.y+(reducedMotion?0:Math.sin(game.elapsed*3+crystal.x)*3);
    glow(crystal.x,y,31,'#ecc77833');ctx.fillStyle='#f2cf7f';ctx.beginPath();ctx.moveTo(crystal.x,y-11);ctx.lineTo(crystal.x+7,y);ctx.lineTo(crystal.x,y+11);ctx.lineTo(crystal.x-7,y);ctx.closePath();ctx.fill();rect(crystal.x-1,y-6,2,7,'#fff1b9');
  }
  const objects=[...game.trees.map(t=>({y:t.y,draw:()=>drawTree(t)})),...game.enemies.map(e=>({y:e.y,draw:()=>drawSpirit(e)})),{y:game.player.y,draw:drawPlayer}];
  objects.sort((a,b)=>a.y-b.y).forEach(o=>o.draw());
  for(const p of game.particles){ctx.globalAlpha=Math.max(0,p.life/.6);rect(p.x,p.y,3,3,p.color);}ctx.globalAlpha=1;
  for(let i=0;i<25;i++){const x=60+(i*137)%840,y=65+(i*89)%460;circle(x,y,1.2,`rgba(222,220,158,${.12+Math.sin(game.elapsed*1.4+i)*.1})`);}
  const vignette=ctx.createRadialGradient(WIDTH/2,HEIGHT/2,120,WIDTH/2,HEIGHT/2,550);vignette.addColorStop(0,'transparent');vignette.addColorStop(1,'#0a202c99');ctx.fillStyle=vignette;ctx.fillRect(0,0,WIDTH,HEIGHT);
  if(game.collected===8 && game.state==='playing') {
    const p=game.player,angle=Math.atan2(game.gate.y-p.y,game.gate.x-p.x);ctx.save();ctx.translate(p.x+Math.cos(angle)*32,p.y+Math.sin(angle)*32);ctx.rotate(angle);ctx.fillStyle='#e7d998';ctx.beginPath();ctx.moveTo(7,0);ctx.lineTo(-4,-4);ctx.lineTo(-4,4);ctx.closePath();ctx.fill();ctx.restore();
  }
  ctx.restore();
}
let lastTime=null;
function frame(time) {const dt=Math.min((time-(lastTime??time))/1000,.04);lastTime=time;game.update(dt);draw();requestAnimationFrame(frame);}
const normalizeKey = key => key.length===1?key.toLowerCase():key;
window.addEventListener('keydown',event=>{
  const key=normalizeKey(event.key);
  if((key==='p'||key==='Escape')&&!event.repeat) {event.preventDefault();game.pause();return;}
  if(['w','a','s','d','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Shift'].includes(key) && game.state==='playing') {event.preventDefault();keys.add(key);}
});
window.addEventListener('keyup',event=>keys.delete(normalizeKey(event.key)));
function autoPause(){keys.clear();if(game.state==='playing')game.pause();lastTime=null;}
window.addEventListener('blur',autoPause);
document.addEventListener('visibilitychange',()=>{if(document.hidden)autoPause();});
$('primary').addEventListener('click',()=>game.primary());
$('pause').addEventListener('click',()=>game.pause());
$('restart').addEventListener('click',()=>game.restart());
$('sound').addEventListener('click',()=>{sound.enabled=!sound.enabled;writeStorage('nightfall-sound',sound.enabled?'on':'off');sound.refresh();sound.unlock();if(sound.enabled)sound.play('crystal');});
const pointers = new Map();
document.querySelectorAll('[data-key]').forEach(button=>{
  button.addEventListener('pointerdown',event=>{event.preventDefault();if(game.state!=='playing')return;button.setPointerCapture(event.pointerId);pointers.set(event.pointerId,button.dataset.key);keys.add(button.dataset.key);});
  const release=event=>{const key=pointers.get(event.pointerId);pointers.delete(event.pointerId);if(key && ![...pointers.values()].includes(key))keys.delete(key);};
  for(const event of ['pointerup','pointercancel','lostpointercapture'])button.addEventListener(event,release);
});
requestAnimationFrame(frame);
