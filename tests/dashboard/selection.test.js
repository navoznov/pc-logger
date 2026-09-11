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
