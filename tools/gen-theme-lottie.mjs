import { writeFileSync } from 'node:fs';

// Generates the three-state theme icon animation (system -> light -> dark -> system).
//
// Authored from the `text-to-lottie` skill (Lottie structure: v/fr/ip/op/w/h/nm/assets/layers,
// op exclusive, non-linear keyframe easing) and the `motion-design` skill:
//   - element: icon transition -> 150-250 ms per transition
//   - personality: Corporate -> duration 200-400 ms, cubic-bezier(0.2, 0, 0, 1), 0-3 % overshoot
//   - "never opacity-only" for state changes -> the icons travel like the sun across the sky:
//     they rise from the LEFT edge, cross the centre, and set off the RIGHT edge while fading.
//     All three states (monitor / sun / moon) share that path, so "follow system" takes part in
//     the same choreography instead of breaking it.
//
// IMPORTANT: the three legs are authored FORWARD ONLY.
// AnimatedVisualPlayer.PlayAsync(from, to) in WinUI does not play in reverse: a 1 -> 0 call runs
// forward and lands on progress 1, which left the icon stuck on the moon after dark -> system.
// So the cycle is laid out as three sequential legs and the app always plays forwards:
//
//   frame  0 = system (monitor)   progress 0
//   frame 15 = light  (sun)       progress 1/3   <- leg 1: system -> light
//   frame 30 = dark   (moon)      progress 2/3   <- leg 2: light  -> dark
//   frame 45 = system (monitor)   progress 1     <- leg 3: dark   -> system
//
// fr = 60, so each leg is 15 frames = 250 ms.
//
// Run: node tools/gen-theme-lottie.mjs

const FR = 60;
const OP = 46; // exclusive: frames 0..45, three 15-frame legs
const W = 100;
const H = 100;
const STROKE = 4.2; // tuned so the rendered stroke weight matches a 16 px Segoe MDL2 glyph
                    // (measured: median horizontal ink run 2 px at 7 units vs 1 px for the glyph)
// The artwork is scaled up inside its 100-unit canvas so that at a 20 px player its ink matches a
// 16 px Segoe MDL2 glyph (measured: artwork filled ~52 % of the canvas -> 11 px ink vs 16 px).
const ART_SCALE = 145;
const COLOR = [0, 0, 0, 1]; // single colour -> LottieGen -GenerateColorBindings exposes Color_000000

// cubic-bezier(0.2, 0, 0, 1) out/in control points, used for every keyframe
const easeOut = { o: { x: [0.2], y: [0] }, i: { x: [0], y: [1] } };

const kf = (t, s, e, ease = easeOut) => (e === undefined ? { t, s, ...(ease === null ? {} : {}) } : { t, s, e, ...ease });

const animated = (frames) => ({ a: 1, k: frames });
const fixed = (k) => ({ a: 0, k });

/**
 * Normalises a property value. Numbers become static, keyframe arrays become animated, and an
 * already-wrapped { a, k } object is passed through unchanged.
 */
const asProperty = (value, fallback) => {
  if (value === undefined || value === null) return fixed(fallback);
  if (typeof value === 'number') return fixed(value);
  if (Array.isArray(value)) return animated(value);
  return value;
};

/** Shape-group transform (last item of every group's `it` array). */
const tr = () => ({
  ty: 'tr',
  p: fixed([0, 0]),
  a: fixed([0, 0]),
  s: fixed([100, 100]),
  r: fixed(0),
  o: fixed(100),
  sk: fixed(0),
  sa: fixed(0),
});

const stroke = () => ({
  ty: 'st',
  c: fixed(COLOR),
  o: fixed(100),
  w: fixed(STROKE),
  lc: 2, // round cap
  lj: 2, // round join
  ml: 4,
  bm: 0,
});

/** Rounded-rect outline path (used for the monitor body). */
function roundedRectPath(w, h, r) {
  const x = w / 2;
  const y = h / 2;
  const k = 0.5523 * r;
  const v = [
    [-x + r, -y], [x - r, -y], [x, -y], [x, -y + r],
    [x, y - r], [x, y], [x - r, y], [-x + r, y],
    [-x, y], [-x, y - r], [-x, -y + r], [-x, -y],
  ];
  const i = [
    [0, 0], [0, 0], [-k, 0], [0, -k],
    [0, 0], [0, 0], [k, 0], [0, 0],
    [0, 0], [0, k], [0, 0], [0, 0],
  ];
  const o = [
    [0, 0], [0, 0], [0, 0], [0, 0],
    [0, k], [0, 0], [0, 0], [0, 0],
    [-k, 0], [0, 0], [0, -k], [0, 0],
  ];
  return { ty: 'sh', ks: fixed({ i, o, v, c: true }) };
}

/** Open/closed polyline path from a list of points. */
const polyline = (points, closed = false) => ({
  ty: 'sh',
  ks: fixed({
    i: points.map(() => [0, 0]),
    o: points.map(() => [0, 0]),
    v: points,
    c: closed,
  }),
});

const circle = (diameter) => ({ ty: 'el', p: fixed([0, 0]), s: fixed([diameter, diameter]) });

/**
 * One shape per group: LottieGen warns (LT0027) when several shapes share a group, and would
 * drop the extra ones. Every path therefore gets its own stroke + transform.
 */
const shapeGroup = (name, path) => ({
  ty: 'gr',
  nm: name,
  it: [path, stroke(), tr()],
});

/** Layer helper: shape layer with opacity/scale/position animation. */
function layer({ name, index, shapes, opacity, position, scale = [ART_SCALE, ART_SCALE] }) {
  return {
    ddd: 0,
    ind: index,
    ty: 4,
    nm: name,
    sr: 1,
    ks: {
      o: asProperty(opacity, 100),
      r: fixed(0),
      // position drives the sunrise/sunset ride; default is the centre of the canvas
      p: asProperty(position, [W / 2, H / 2]),
      a: fixed([0, 0]),
      s: asProperty(scale, [100, 100]),
    },
    ao: 0,
    shapes,
    ip: 0,
    op: OP,
    st: 0,
    bm: 0,
  };
}

const round = (value) => Math.round(value * 100) / 100;

/**
 * Crescent moon = circle C1 minus circle C2 (C2 shifted right). The visible outline is therefore
 * C1's LEFT arc plus C2's LEFT arc; using the right-hand arcs instead would produce a plain disc.
 */
function crescent() {
  const R = 21;
  const r = 17;
  const dx = 8.5;
  const a = (dx * dx - r * r + R * R) / (2 * dx);
  const h = Math.sqrt(Math.max(R * R - a * a, 0));
  const theta1 = Math.atan2(h, a);            // intersections on C1
  const theta2 = Math.atan2(h, a - dx);       // ... and on C2
  const steps = 18;
  const points = [];

  // C1 left arc: from +theta1 through 180 deg to -theta1
  for (let s = 0; s <= steps; s++) {
    const t = theta1 + (2 * Math.PI - 2 * theta1) * (s / steps);
    points.push([round(Math.cos(t) * R), round(Math.sin(t) * R)]);
  }

  // C2 left arc back: from -theta2 through -180 deg to +theta2 (opposite direction, closes the ring)
  for (let s = 1; s < steps; s++) {
    const t = -theta2 - (2 * Math.PI - 2 * theta2) * (s / steps);
    points.push([round(dx + Math.cos(t) * r), round(Math.sin(t) * r)]);
  }

  return polyline(points, true);
}

// ---------------------------------------------------------------- travel path (left -> right)
// Each layer gets ONE position track. Travel mirrors the sun crossing the sky:
//   park left (12, 84) -> arc up through (30, 62) -> centre (50, 50)
//   centre -> arc down through (70, 62) -> park right (88, 84)
const PARK_LEFT = [12, 84];
const PARK_RIGHT = [88, 84];
const CENTRE = [50, 50];
const RISE_MID = [30, 62];
const SET_MID = [70, 62];

/** Rises from the left edge into the centre over [from, to]. */
const enterTrack = (from, to) => [
  kf(from, PARK_LEFT, RISE_MID),
  kf((from + to) / 2, RISE_MID, CENTRE),
  kf(to, CENTRE),
];

/**
 * Turns [frame, point] waypoints into one keyframe list with `e` = next point.
 * A single track avoids duplicate timestamps (two keyframes at the same frame are fragile).
 */
const waypoints = (points) =>
  points
    .slice(0, -1)
    .map(([t, point], i) => kf(t, point, points[i + 1][1]))
    .concat([kf(points[points.length - 1][0], points[points.length - 1][1])]);

/** Sets from the centre out to the right edge over [from, to]. */
const exitTrack = (from, to) => [
  kf(from, CENTRE, SET_MID),
  kf((from + to) / 2, SET_MID, PARK_RIGHT),
  kf(to, PARK_RIGHT),
];

// ---------------------------------------------------------------- monitor (follow system)
const monitorBody = roundedRectPath(54, 36, 5);
const monitorStand = polyline([[-9, 18], [-9, 24], [9, 24], [9, 18]]);
const monitorBase = polyline([[-15, 24], [15, 24]]);

const monitor = layer({
  name: 'monitor',
  index: 1,
  shapes: [
    shapeGroup('monitor-body', monitorBody),
    shapeGroup('monitor-stand', monitorStand),
    shapeGroup('monitor-base', monitorBase),
  ],
  // leg 1: sets to the right (system -> light); leg 3: rises from the left (dark -> system)
  opacity: animated([
    kf(0, [100], [0]),
    kf(9, [0]),
    kf(36, [0], [100]),
    kf(45, [100]),
  ]),
  // invisible between the legs, so the trip back to the left edge is never seen
  position: animated(waypoints([
    [0, CENTRE],
    [4.5, SET_MID],
    [9, PARK_RIGHT],
    [36, PARK_LEFT],
    [40.5, RISE_MID],
    [45, CENTRE],
  ])),
});

// ---------------------------------------------------------------- sun
const rayLength = 9;
const rayOffset = 16.5;
const sunRays = [];
for (let n = 0; n < 8; n++) {
  const angle = (Math.PI / 4) * n;
  const x1 = Math.cos(angle) * rayOffset;
  const y1 = Math.sin(angle) * rayOffset;
  const x2 = Math.cos(angle) * (rayOffset + rayLength);
  const y2 = Math.sin(angle) * (rayOffset + rayLength);
  sunRays.push(polyline([[round(x1), round(y1)], [round(x2), round(y2)]]));
}

const sun = layer({
  name: 'sun',
  index: 2,
  shapes: [
    shapeGroup('sun-disc', circle(30)),
    ...sunRays.map((ray, index) => shapeGroup(`sun-ray-${index}`, ray)),
  ],
  // leg 1: rises from the left (system -> light); leg 2: sets to the right (light -> dark)
  opacity: animated([
    kf(6, [0], [100]),
    kf(15, [100], [0]),
    kf(24, [0]),
  ]),
  position: animated(waypoints([
    [0, PARK_LEFT],
    [6, PARK_LEFT],
    [10.5, RISE_MID],
    [15, CENTRE],
    [19.5, SET_MID],
    [24, PARK_RIGHT],
  ])),
});

// ---------------------------------------------------------------- moon
const moon = layer({
  name: 'moon',
  index: 3,
  shapes: [shapeGroup('moon-body', crescent())],
  // leg 2: rises from the left (light -> dark); leg 3: sets to the right (dark -> system)
  opacity: animated([
    kf(0, [0]),
    kf(21, [0], [100]),
    kf(30, [100], [0]),
    kf(39, [0]),
  ]),
  position: animated(waypoints([
    [0, PARK_LEFT],
    [21, PARK_LEFT],
    [25.5, RISE_MID],
    [30, CENTRE],
    [34.5, SET_MID],
    [39, PARK_RIGHT],
  ])),
});

const animation = {
  v: '5.7.4',
  fr: FR,
  ip: 0,
  op: OP,
  w: W,
  h: H,
  nm: 'theme-3state',
  ddd: 0,
  assets: [],
  layers: [monitor, sun, moon],
  markers: [
    { tm: 0, cm: 'system', dr: 0 },
    { tm: 15, cm: 'light', dr: 0 },
    { tm: 30, cm: 'dark', dr: 0 },
    { tm: 45, cm: 'system-again', dr: 0 },
  ],
};

// ---------------------------------------------------------------- self checks
// Guards every regression this animation already had.

const assertNumeric = (value, label = 'property') => {
  if (!Array.isArray(value) || value.some((v) => typeof v !== 'number' || Number.isNaN(v))) {
    throw new Error(`${label} is not a numeric array: ${JSON.stringify(value)}`);
  }

  return value;
};

/** Value of an animated property at a frame (linear between keyframes, hold outside them). */
function valueAt(prop, frame) {
  if (prop === undefined || typeof prop !== 'object' || !('k' in prop)) {
    throw new Error(`property is not wrapped as { a, k }: ${JSON.stringify(prop)}`);
  }

  if (prop.a === 0) {
    return assertNumeric(prop.k);
  }

  const frames = prop.k;
  if (!Array.isArray(frames)) {
    throw new Error(`animated property has no keyframe array: ${JSON.stringify(prop)}`);
  }

  if (frame <= frames[0].t) return assertNumeric(frames[0].s);

  for (let i = 0; i < frames.length - 1; i++) {
    const from = frames[i];
    const to = frames[i + 1];
    if (frame >= from.t && frame <= to.t) {
      // Lottie semantics: `s` is the segment start value, `e` (when present) the end value.
      const start = assertNumeric(from.s);
      const end = assertNumeric(from.e ?? to.s);
      const span = to.t - from.t || 1;
      const t = (frame - from.t) / span;
      return start.map((v, n) => v + ((end[n] ?? v) - v) * t);
    }
  }

  return assertNumeric(frames[frames.length - 1].s);
}

const expectVisible = (value, shouldShow, label) => {
  assertNumeric(value, label);
  const visible = value[0] > 50;
  if (visible !== shouldShow) {
    throw new Error(`visibility check failed at ${label}: ${value[0].toFixed(1)} (expected ${shouldShow ? 'visible' : 'hidden'})`);
  }
};

// Active state centred; the others parked off-stage to one side, so nothing half-visible remains.
const expectPosition = (value, isActive, label) => {
  assertNumeric(value, label);
  const [x, y] = value;
  if (isActive) {
    if (Math.abs(x - CENTRE[0]) > 1 || Math.abs(y - CENTRE[1]) > 1) {
      throw new Error(`position check failed at ${label}: (${x}, ${y}) expected the centre (${CENTRE})`);
    }

    return;
  }

  // Hidden layers may be travelling back to the left edge (invisible), all that matters is that
  // they are not sitting on the centre where the active icon is.
  if (Math.abs(x - CENTRE[0]) < 10 && Math.abs(y - CENTRE[1]) < 10) {
    throw new Error(`position check failed at ${label}: (${x}, ${y}) overlaps the centre while hidden`);
  }
};

for (const [frame, state, expected] of [
  [0, 'system', { monitor: true, sun: false, moon: false }],
  [15, 'light', { monitor: false, sun: true, moon: false }],
  [30, 'dark', { monitor: false, sun: false, moon: true }],
  [45, 'system-again', { monitor: true, sun: false, moon: false }],
]) {
  for (const [name, element] of [['monitor', monitor], ['sun', sun], ['moon', moon]]) {
    const shown = expected[name];
    expectVisible(valueAt(element.ks.o, frame), shown, `${state}/${name}/opacity`);
    expectPosition(valueAt(element.ks.p, frame), shown, `${state}/${name}/position`);
  }
}

// Stagger: the outgoing icon must be gone before the incoming one arrives, so they never overlap.
const monitorAtMid = valueAt(monitor.ks.o, 12)[0];
const sunAtMid = valueAt(sun.ks.o, 12)[0];
if (monitorAtMid > 1) {
  throw new Error(`stagger check failed: monitor still at ${monitorAtMid} when the sun is rising`);
}

if (sunAtMid < 30 || sunAtMid > 95) {
  throw new Error(`stagger check failed: sun at ${sunAtMid} mid-leg (expected a partial value)`);
}

// Travel direction: every layer must enter from the left and leave to the right.
for (const [name, element] of [['monitor', monitor], ['sun', sun], ['moon', moon]]) {
  const xs = element.ks.p.k.map((keyframe) => keyframe.s[0]);
  const first = xs[0];
  const last = xs[xs.length - 1];
  if (!(first <= CENTRE[0] && last >= CENTRE[0])) {
    throw new Error(`travel check failed for ${name}: x goes ${first} -> ${last} (expected left -> right)`);
  }
}

// Crescent must be a crescent: the bite removes the right side, so it cannot reach +R.
const moonPath = moon.shapes[0].it[0].ks.k;
const maxX = Math.max(...moonPath.v.map(([x]) => x));
if (maxX > 15) {
  throw new Error(`crescent check failed: max x = ${maxX} (a disc would reach ~21)`);
}

console.log('self checks: visibility + sunrise-left/sunset-right + stagger + crescent OK');

const out = 'src/TodoApp/Assets/lottie/theme-3state.json';
writeFileSync(out, JSON.stringify(animation, null, 1));
console.log(`wrote ${out}`);
console.log(`frames: 0=system, 15=light, 30=dark, 45=system (${((OP - 1) / FR) * 1000} ms total, ${((OP - 1) / 3 / FR) * 1000} ms per leg)`);






