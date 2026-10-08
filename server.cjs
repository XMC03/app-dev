'use strict';
// A small static server; the game itself also works directly from index.html.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = __dirname;
const files = new Set(['index.html', 'style.css', 'game.js']);
const types = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8' };
const port = Number(process.env.PORT || 8000);
const server = http.createServer((req, res) => {
  let name;
  try { name = decodeURIComponent(new URL(req.url, 'http://localhost').pathname).slice(1) || 'index.html'; }
  catch { res.writeHead(400).end('Bad request'); return; }
  if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405, { Allow: 'GET, HEAD' }).end(); return; }
  if (!files.has(name)) { res.writeHead(404).end('Not found'); return; }
  fs.readFile(path.join(root, name), (error, data) => {
    if (error) { res.writeHead(500).end('Unable to load file'); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(name)], 'Cache-Control': 'no-cache' });
    res.end(req.method === 'HEAD' ? undefined : data);
  });
});
if (require.main === module) server.listen(port, '0.0.0.0', () => console.log(`Nightfall is running on port ${port}`));
module.exports = server;
