'use strict';
const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const server = require('../server.cjs');
let browser, base;
const errors = [];
before(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  base = `http://127.0.0.1:${server.address().port}`;
  const executablePath = process.env.CHROME_PATH || (fs.existsSync('/usr/bin/chromium') ? '/usr/bin/chromium' : undefined);
  browser = await chromium.launch({ executablePath, args: ['--no-sandbox'] });
  fs.mkdirSync(path.join(__dirname, '../test-results'), { recursive: true });
});
after(async () => { await browser?.close(); await new Promise(resolve => server.close(resolve)); });
async function page(options = {}) {
  const context = await browser.newContext(options);
  const p = await context.newPage();
  p.on('pageerror', error => errors.push(error.message));
  await p.goto(base);
  return p;
}

test('desktop: start, keyboard movement, dash, pause, audio, restart and focus loss', async () => {
  const p = await page({ viewport: { width: 1440, height: 1100 } });
  assert.equal(await p.locator('#modal-title').textContent(), 'A light in the dark.');
  await p.screenshot({ path: 'test-results/desktop-start.png', fullPage: true });
  await p.locator('#primary').click();
  assert.equal(await p.locator('#overlay').isVisible(), false);
  const initial = await p.evaluate(() => game.player.x);
  await p.keyboard.down('d'); await p.waitForTimeout(350); await p.keyboard.up('d');
  assert.ok(await p.evaluate(() => game.player.x) > initial + 25);
  await p.keyboard.down('Shift'); await p.keyboard.down('s'); await p.waitForTimeout(250);
  await p.keyboard.up('s'); await p.keyboard.up('Shift');
  assert.ok(await p.evaluate(() => game.stamina) < .95);
  await p.keyboard.press('p');
  const paused = await p.evaluate(() => game.elapsed);
  await p.waitForTimeout(150);
  assert.equal(await p.evaluate(() => game.elapsed), paused);
  assert.equal(await p.locator('#modal-title').textContent(), 'The forest can wait.');
  await p.locator('#primary').click();
  await p.locator('#sound').click();
  assert.equal(await p.locator('#sound').getAttribute('aria-pressed'), 'true');
  await p.locator('#restart').click();
  assert.equal(await p.evaluate(() => game.collected), 0);
  assert.equal(await p.evaluate(() => game.player.hp), 3);
  await p.screenshot({ path: 'test-results/desktop-game.png', fullPage: true });
  await p.evaluate(() => window.dispatchEvent(new Event('blur')));
  assert.equal(await p.evaluate(() => game.state), 'paused');
  assert.deepEqual(errors, []);
  await p.context().close();
});

test('game rules: reachable objectives, obstacle navigation, collisions, damage and full campaign', async () => {
  const p = await page();
  const result = await p.evaluate(() => {
    // Use the actual game rules with controlled positions and fixed time steps.
    // This verifies transitions independently of human reflexes and wall-clock timing.
    const check = (condition, message) => { if (!condition) throw Error(message); };
    const g = new Game(); g.start();
    const tree = g.trees[0];
    g.player.x = tree.x - 40; g.player.y = tree.y;
    g.move(g.player, 80, 0);
    check(g.player.x < tree.x - 35, 'Player passed through a tree');
    g.move(g.player, -10000, -10000);
    check(g.player.x >= 51 && g.player.y >= 51, 'Player escaped map');
    for (let level = 0; level < 3; level++) {
      g.level = level; g.loadLevel(); g.state = 'playing';
      for (const target of [...g.crystals, g.gate]) check(g.path(g.player, target).length > 0, `Unreachable objective in chapter ${level+1}`);
      // A spirit on one side of a tree must find its way around it.
      const t = g.trees[0], spirit = g.enemies[0];
      spirit.x = t.x - 40; spirit.y = t.y;
      g.player.x = t.x + 40; g.player.y = t.y;
      const waypoints = g.path(spirit, g.player);
      check(waypoints.length >= 3, 'Spirit route cut through a tree');
      for (const waypoint of waypoints) {
        for (let frame = 0; frame < 100 && distance(spirit, waypoint) > 1; frame++) {
          const d = distance(spirit, waypoint);
          g.move(spirit, (waypoint.x-spirit.x)/d*Math.min(d,3), (waypoint.y-spirit.y)/d*Math.min(d,3));
        }
        check(distance(spirit, waypoint) <= 1, 'Spirit became stuck on route');
      }
      g.loadLevel(); g.state = 'playing';
      g.player.x = g.gate.x; g.player.y = g.gate.y; g.update(0);
      check(g.state === 'playing', 'Closed gate allowed exit');
      for (const crystal of [...g.crystals]) {g.player.x = crystal.x;g.player.y = crystal.y;g.update(0);}
      check(g.collected === 8 && g.state === 'playing', 'Crystal collection or gate unlock failed');
      g.player.x = g.gate.x; g.player.y = g.gate.y; g.update(0);
      check(g.state === (level === 2 ? 'won' : 'cleared'), 'Chapter completion failed');
      if(level<2) {g.primary();check(g.level===level+1 && g.player.hp===3 && g.stamina===1, 'Chapter advance did not reset supplies');}
    }
    g.primary(); check(g.level===0 && g.state==='playing', 'New campaign did not restart');
    g.enemies[0].x = g.player.x; g.enemies[0].y = g.player.y; g.update(0);
    check(g.player.hp===2, 'Contact did not cause damage');g.update(0);check(g.player.hp===2, 'Damage protection failed');
    for(let hit=0;hit<2;hit++){g.invulnerable=0;g.update(0);}
    check(g.state==='lost' && g.player.hp===0, 'Loss failed');g.primary();check(g.player.hp===3 && g.state==='playing', 'Retry failed');
    keys.add('d');keys.add('Shift');for(let i=0;i<50;i++)g.update(.04);
    check(g.stamina>=0 && g.dashExhausted, 'Dash exhaustion failed');keys.clear();g.update(.04);check(!g.dashExhausted && g.stamina>0, 'Dash recovery failed');
    return 'rules passed';
  });
  assert.equal(result, 'rules passed');
  assert.deepEqual(errors, []);
  await p.context().close();
});

test('a moving player can win all three chapters with enemies active, and best time persists', async () => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 1000 } });
  // Fixed-step simulation makes the run repeatable while using the same input,
  // collision, damage, navigation and campaign rules as real play.
  await context.addInitScript(() => { window.requestAnimationFrame = () => 0; });
  const p = await context.newPage();
  p.on('pageerror', error => errors.push(error.message));
  await p.goto(base); await p.locator('#primary').click();
  const outcomes = await p.evaluate(() => {
    const results = [];
    for (let chapter = 0; chapter < 3; chapter++) {
      let frames = 0;
      while (game.state === 'playing' && frames < 20000) {
        const target = game.collected === 8 ? game.gate : game.crystals.reduce((best, crystal) =>
          game.path(game.player, crystal).length < game.path(game.player, best).length ? crystal : best, game.crystals[0]);
        for (const waypoint of game.path(game.player, target)) {
          let steps = 0;
          while (distance(game.player, waypoint) > 3 && game.state === 'playing' && steps++ < 200) {
            keys.clear();
            const dx = waypoint.x-game.player.x, dy = waypoint.y-game.player.y;
            if (Math.abs(dx)>2) keys.add(dx>0?'d':'a');
            if (Math.abs(dy)>2) keys.add(dy>0?'s':'w');
            if (game.enemies.some(enemy => distance(enemy,game.player)<160) && !game.dashExhausted) keys.add('Shift');
            game.update(.02); frames++;
          }
          if(game.state!=='playing') break;
        }
      }
      results.push({ state:game.state, hearts:game.player.hp, crystals:game.collected });
      if(game.state==='cleared') game.primary(); else break;
    }
    draw();
    return results;
  });
  assert.deepEqual(outcomes.map(o=>o.state), ['cleared','cleared','won']);
  assert.ok(outcomes.every(o=>o.hearts>0 && o.crystals===8));
  assert.equal(await p.locator('#modal-title').textContent(), 'You found your way home.');
  await p.screenshot({ path: 'test-results/victory.png', fullPage: true });
  const best = await p.evaluate(() => game.best);
  assert.ok(best>0);
  await p.reload();
  assert.equal(await p.evaluate(() => game.best), best);
  assert.ok((await p.locator('#best').textContent()).startsWith('BEST ADVENTURE'));
  assert.deepEqual(errors, []);
  await context.close();
});

test('mobile: viewport fits, start and multi-touch controls work, pointer cancellation releases input', async () => {
  const p = await page({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 2 });
  assert.ok(await p.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  assert.ok(await p.locator('#primary').isVisible());
  await p.screenshot({ path: 'test-results/mobile-start.png', fullPage: true });
  await p.locator('#primary').tap();
  const right = p.locator('[data-key="ArrowRight"]');
  // Dispatch pointer events while retaining the browser's pointer-capture API.
  // Physical multi-touch is not exposed by Playwright's single-point touchscreen API.
  await p.evaluate(() => {
    const button = document.querySelector('[data-key="ArrowRight"]');
    const dash = document.querySelector('[data-key="Shift"]');
    button.setPointerCapture = () => {}; dash.setPointerCapture = () => {};
    button.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 1, bubbles: true }));
    dash.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, bubbles: true }));
  });
  await p.waitForTimeout(250);
  assert.ok(await p.evaluate(() => game.player.x) > 130);
  assert.ok(await p.evaluate(() => game.stamina) < 1);
  await right.dispatchEvent('pointercancel', { pointerId: 1 });
  await p.locator('[data-key="Shift"]').dispatchEvent('pointerup', { pointerId: 2 });
  assert.equal(await p.evaluate(() => keys.size), 0);
  await p.locator('#pause').tap();
  assert.equal(await p.evaluate(() => game.state), 'paused');
  await p.screenshot({ path: 'test-results/mobile-paused.png', fullPage: true });
  assert.deepEqual(errors, []);
  await p.context().close();
});

test('storage is optional: play and sound work when browser persistence is unavailable', async () => {
  const context = await browser.newContext();
  await context.addInitScript(() => {
    Storage.prototype.getItem = () => { throw new Error('Storage blocked'); };
    Storage.prototype.setItem = () => { throw new Error('Storage blocked'); };
  });
  const p = await context.newPage();
  p.on('pageerror', error => errors.push(error.message));
  await p.goto(base);
  await p.locator('#primary').click();
  assert.equal(await p.evaluate(() => game.state), 'playing');
  await p.locator('#sound').click();
  assert.equal(await p.locator('#sound').getAttribute('aria-pressed'), 'true');
  await p.keyboard.down('ArrowRight');await p.waitForTimeout(200);await p.keyboard.up('ArrowRight');
  assert.ok(await p.evaluate(() => game.player.x) > 110);
  assert.deepEqual(errors, []);
  await context.close();
});

test('static server serves the game and rejects unsupported paths and methods', async () => {
  for(const file of ['', 'style.css', 'game.js']) assert.equal((await fetch(`${base}/${file}`)).status, 200);
  assert.equal((await fetch(`${base}/package.json`)).status, 404);
  assert.equal((await fetch(`${base}/`, { method: 'POST' })).status, 405);
  assert.equal((await fetch(`${base}/game.js`, { method: 'HEAD' })).status, 200);
});
