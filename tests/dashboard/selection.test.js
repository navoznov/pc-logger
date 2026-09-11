const test = require('node:test');
const assert = require('node:assert');
const { Selection } = require('../../src/PcLogger.Core/dashboard/selection.js');

const MIN = 60;
const span = (startMin, lengthMin, s) => ({ t: startMin * MIN, d: lengthMin * MIN, s });
const game = (startMin, lengthMin, lvl) => ({ t: startMin * MIN, d: lengthMin * MIN, lvl, app: 'g.exe' });

test('sums each state across the selection', () => {
  const spans = [span(0, 30, 'active'), span(30, 20, 'away'), span(50, 10, 'off')];

  const s = Selection.stats(spans, [], 0, 60 * MIN);

  assert.equal(s.total, 60 * MIN);
  assert.equal(s.active, 30 * MIN);
  assert.equal(s.away, 20 * MIN);
  assert.equal(s.off, 10 * MIN);
});

test('percentages are taken against the whole selection', () => {
  const spans = [span(0, 30, 'active'), span(30, 30, 'away')];

  const s = Selection.stats(spans, [], 0, 60 * MIN);

  assert.equal(s.pct.active, 50);
  assert.equal(s.pct.away, 50);
  assert.equal(s.pct.off, 0);
});

test('percentages are taken against the whole selection, not the spans covered within it', () => {
  // Only 40 of the 60 selected minutes are covered by spans. If pct divided by
  // active + away + off instead of the full selection, active would read 75, not 50.
  const spans = [span(0, 30, 'active'), span(30, 10, 'away')];

  const s = Selection.stats(spans, [], 0, 60 * MIN);

  assert.equal(s.total, 3600);
  assert.equal(s.pct.active, 50);
  assert.equal(s.pct.away, 17);
});

test('gameFg percentage is a share of the whole selection, not of active time', () => {
  // active only covers half the selection. Dividing gameFg by active instead of the
  // full selection would read 50, not 25.
  const spans = [span(0, 30, 'active')];
  const games = [game(0, 15, 'fg')];

  const s = Selection.stats(spans, games, 0, 60 * MIN);

  assert.equal(s.pct.gameFg, 25);
});

test('clips spans that straddle the selection edges', () => {
  const spans = [span(0, 60, 'active')];

  const s = Selection.stats(spans, [], 10 * MIN, 20 * MIN);

  assert.equal(s.total, 10 * MIN);
  assert.equal(s.active, 10 * MIN);
});

test('ignores spans entirely outside the selection', () => {
  const spans = [span(0, 10, 'active'), span(100, 10, 'active')];

  const s = Selection.stats(spans, [], 50 * MIN, 60 * MIN);

  assert.equal(s.active, 0);
});

test('counts only focused game time', () => {
  const spans = [span(0, 60, 'active')];
  const games = [game(0, 20, 'fg'), game(20, 30, 'bg')];

  const s = Selection.stats(spans, games, 0, 60 * MIN);

  assert.equal(s.gameFg, 20 * MIN);
  assert.equal(s.pct.gameFg, 33);
});

test('ratio compares time at the PC against time away from it', () => {
  const spans = [span(0, 40, 'active'), span(40, 20, 'away')];

  const s = Selection.stats(spans, [], 0, 60 * MIN);

  assert.equal(s.ratio, 2);
});

test('ratio is null when nothing counts as away', () => {
  const spans = [span(0, 60, 'active')];

  assert.equal(Selection.stats(spans, [], 0, 60 * MIN).ratio, null);
});

test('an empty selection reports zeroes rather than NaN', () => {
  const s = Selection.stats([], [], 0, 0);

  assert.equal(s.total, 0);
  assert.equal(s.pct.active, 0);
  assert.equal(s.ratio, null);
});

test('the two printed percentages add up to a hundred', () => {
  // active 22.5 %, away+off 77.5 %: rounded independently these print 23 and 78.
  const spans = [
    { t: 0, d: 225, s: 'active' },
    { t: 225, d: 387, s: 'away' },
    { t: 612, d: 388, s: 'off' }
  ];

  const { pct } = Selection.stats(spans, [], 0, 1000);

  assert.equal(pct.active + pct.notAtPc, 100);
});

test('game time in focus never exceeds time spent at the pc', () => {
  // the game holds the foreground for the whole window, but the child is there for a third
  const spans = [{ t: 0, d: 100, s: 'active' }, { t: 100, d: 200, s: 'away' }];
  const gameSpans = [{ t: 0, d: 300, lvl: 'fg', app: 'game.exe' }];

  const stats = Selection.stats(spans, gameSpans, 0, 300);

  assert.equal(stats.gameFg, 100);
  assert.ok(stats.gameFg <= stats.active);
});

test('game time in focus is clipped to the selection window', () => {
  const spans = [{ t: 0, d: 300, s: 'active' }];
  const gameSpans = [{ t: 0, d: 300, lvl: 'fg', app: 'game.exe' }];

  assert.equal(Selection.stats(spans, gameSpans, 100, 200).gameFg, 100);
});

test('a zero-length span contributes nothing', () => {
  const spans = [{ t: 500, d: 0, s: 'active' }, { t: 0, d: 1000, s: 'away' }];

  assert.equal(Selection.stats(spans, [], 0, 1000).active, 0);
});

test('a span touching the window only at its edge contributes nothing', () => {
  const spans = [{ t: 900, d: 100, s: 'active' }];

  assert.equal(Selection.stats(spans, [], 1000, 2000).active, 0);
});

test('the at-pc ratio counts powered-off time, not only time spent away', () => {
  // 60 min active, 30 away, 30 off. Ignoring `off` would give 2:1 instead of 1:1.
  const spans = [
    { t: 0, d: 3600, s: 'active' },
    { t: 3600, d: 1800, s: 'away' },
    { t: 5400, d: 1800, s: 'off' }
  ];

  assert.equal(Selection.stats(spans, [], 0, 7200).ratio, 1);
});
