const test = require('node:test');
const assert = require('node:assert');
const { Render } = require('../../src/PcLogger.Core/dashboard/render.js');

test('formats durations in hours and minutes', () => {
  assert.equal(Render.fmtDuration(0), '0 м');
  assert.equal(Render.fmtDuration(59), '0 м');
  assert.equal(Render.fmtDuration(60), '1 м');
  assert.equal(Render.fmtDuration(45 * 60), '45 м');
  assert.equal(Render.fmtDuration(60 * 60), '1 ч 00 м');
  assert.equal(Render.fmtDuration(2 * 3600 + 35 * 60), '2 ч 35 м');
});

test('positions an item as a percentage of the window', () => {
  const [box] = Render.layout([{ t: 50, d: 25 }], 0, 100);

  assert.equal(box.left, 50);
  assert.equal(box.width, 25);
});

test('clips items to the window edges', () => {
  const [box] = Render.layout([{ t: -50, d: 100 }], 0, 100);

  assert.equal(box.left, 0);
  assert.equal(box.width, 50);
});

test('drops items fully outside the window', () => {
  assert.equal(Render.layout([{ t: 200, d: 10 }], 0, 100).length, 0);
});

test('keeps the original item available for rendering', () => {
  const item = { t: 0, d: 100, s: 'active' };

  assert.equal(Render.layout([item], 0, 100)[0].item, item);
});

test('day bounds cover exactly 24 hours in local time', () => {
  const { from, to } = Render.dayBounds(1757606400, 180);

  assert.equal(to - from, 86400);
  assert.ok(from <= 1757606400 && 1757606400 < to);
  assert.equal((from + 180 * 60) % 86400, 0);
});
