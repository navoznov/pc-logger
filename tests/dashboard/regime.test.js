const test = require('node:test');
const assert = require('node:assert');
const { Regime } = require('../../src/PcLogger.Core/dashboard/regime.js');

const OPTS = { microGap: 3, breakMinutes: 15, sessionMinutes: 15, tolerance: 2 };
const MIN = 60;

// Builds a presence span list from an alternating description:
// span(0, 15, 'active') means 15 minutes of activity starting at minute 0.
const span = (startMin, lengthMin, s) => ({ t: startMin * MIN, d: lengthMin * MIN, s });

test('honouring the regime yields two blocks separated by a break', () => {
  const spans = [span(0, 15, 'active'), span(15, 15, 'away'), span(30, 15, 'active')];

  const { blocks, gaps } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks.length, 2);
  assert.deepEqual(blocks.map(b => b.d), [15 * MIN, 15 * MIN]);
  assert.deepEqual(blocks.map(b => b.verdict), ['ok', 'ok']);
  assert.equal(gaps.filter(g => g.kind === 'break').length, 1);
});

test('a micro pause is absorbed into the surrounding block', () => {
  const spans = [span(0, 15, 'active'), span(15, 2, 'away'), span(17, 15, 'active')];

  const { blocks, gaps } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].d, 32 * MIN);
  assert.equal(blocks[0].verdict, 'over');
  assert.deepEqual(gaps.map(g => g.kind), ['micro']);
});

test('an insufficient break keeps the block open and is marked short', () => {
  const spans = [span(0, 15, 'active'), span(15, 8, 'away'), span(23, 15, 'active')];

  const { blocks, gaps } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].verdict, 'over');
  assert.deepEqual(gaps.map(g => g.kind), ['short']);
});

test('block duration counts wall time but screen time excludes inner pauses', () => {
  const spans = [span(0, 15, 'active'), span(15, 8, 'away'), span(23, 15, 'active')];

  const { blocks } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks[0].d, 38 * MIN);
  assert.equal(blocks[0].screen, 30 * MIN);
});

test('a block exactly at the tolerance limit is still ok', () => {
  const { blocks } = Regime.buildBlocks([span(0, 17, 'active')], OPTS);

  assert.equal(blocks[0].verdict, 'ok');
});

test('one second past the tolerance limit is a violation', () => {
  const spans = [{ t: 0, d: 17 * MIN + 1, s: 'active' }];

  const { blocks } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks[0].verdict, 'over');
});

test('a powered-off gap breaks the block exactly like an away gap', () => {
  const withOff = Regime.buildBlocks(
    [span(0, 15, 'active'), span(15, 15, 'off'), span(30, 15, 'active')], OPTS);
  const withAway = Regime.buildBlocks(
    [span(0, 15, 'active'), span(15, 15, 'away'), span(30, 15, 'active')], OPTS);

  assert.deepEqual(withOff.blocks, withAway.blocks);
});

test('thresholds are configurable and change the verdict', () => {
  const spans = [span(0, 25, 'active')];

  assert.equal(Regime.buildBlocks(spans, OPTS).blocks[0].verdict, 'over');
  assert.equal(
    Regime.buildBlocks(spans, { ...OPTS, sessionMinutes: 30 }).blocks[0].verdict, 'ok');
});

test('a block spanning midnight stays one block', () => {
  const midnight = 1757548800;
  const spans = [{ t: midnight - 10 * MIN, d: 20 * MIN, s: 'active' }];

  const { blocks } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].d, 20 * MIN);
  assert.equal(blocks[0].verdict, 'over');
});

test('empty input produces no blocks', () => {
  assert.deepEqual(Regime.buildBlocks([], OPTS), { blocks: [], gaps: [] });
});

test('leading and trailing inactivity produce no blocks of their own', () => {
  const spans = [span(0, 60, 'off'), span(60, 5, 'active'), span(65, 60, 'off')];

  const { blocks } = Regime.buildBlocks(spans, OPTS);

  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].t, 60 * MIN);
});
