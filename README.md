# Nightfall

## Unity version

The Unity 2D desktop project is in **`unity/Nightfall`**. Install Unity Hub and Unity 6.0 LTS, add that folder as a project, open `Assets/Scenes/Nightfall.unity`, and press **Play**. The forest is generated when Play starts.

See **[Unity opening, playing, and building instructions](unity/Nightfall/README.md)** for the full steps and validation status. C# gameplay checks pass; Unity Editor import, Play mode, and executable builds have not been validated in this environment.

## Browser version

A complete, three-chapter top-down forest adventure built with HTML Canvas and vanilla JavaScript. Gather the light, evade the spirits, and find your way home.

## Run

From the repository directory, with Node.js installed:

```sh
npm start
```

Visit port **8000** in your browser. No dependency installation or build step is needed to play. Set `PORT` to choose a different port.

Alternatively, use any static web server, for example `python3 -m http.server 8000`. The game uses only local files and makes no external requests.

## Play

Collect **eight crystals in each glade**, then enter the gate in the northeast corner. A guiding arrow appears when the gate opens. Cross all three glades to win.

| Action | Keyboard | Touch |
| --- | --- | --- |
| Move | WASD or arrow keys | Direction buttons |
| Dash | Hold Shift while moving | Hold DASH and a direction |
| Pause / resume | P or Escape | Pause / Continue buttons |
| Restart current chapter | Restart chapter button | Restart chapter button |

- You have three hearts. Contact with a spirit costs one heart and gives brief protection from further damage.
- Dash uses stamina. Release it to recharge; a fully depleted dash must be released before it can be used again.
- Trees block movement. Spirits navigate around them, so keep moving.
- Each new chapter restores your hearts and stamina. Losing or restarting keeps completed chapters.
- The game pauses automatically when its tab loses focus.
- Optional synthesized sound is controlled with the Sound button. Sound preferences and the best successful campaign time are saved in your browser when storage is available.
- On phones and tablets, on-screen controls support holding a direction and dash together.

## Development and checks

Node.js **20 or newer** is required for the browser test tools. Runtime game files have no third-party dependencies.

```sh
npm ci
npx playwright install chromium
npm test
```

Tests automatically start an isolated server. If Chromium is already installed at `/usr/bin/chromium`, the tests use it. Set `CHROME_PATH` to use another browser executable.

The browser suite covers desktop controls, dash, pause, sound, restart, focus loss, mobile input, storage restrictions, map reachability, collisions, enemy navigation, damage, chapter transitions, and an entire campaign with enemies active. The campaign test uses fixed time steps and simulated directional input. Screenshots are written to the ignored `test-results/` directory.

## Files

- `index.html` — interface and accessible buttons.
- `style.css` — responsive desktop and mobile layouts.
- `game.js` — game rules, obstacle navigation, Canvas artwork, and sound synthesis.
- `server.cjs` — dependency-free local static server.
- `tests/game.test.cjs` — automated Chromium checks.

All artwork is drawn in code, and sound is synthesized locally. There are no external assets or API keys to configure.
