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

test('week matrix has one row per day, newest last', () => {
  const rows = Render.weekMatrix([], 700000, 7, 30);

  assert.equal(rows.length, 7);
  assert.equal(rows[6].from, 700000);
  assert.equal(rows[0].from, 700000 - 6 * 86400);
});

test('each row is split into slots of the requested size', () => {
  const rows = Render.weekMatrix([], 0, 1, 30);

  assert.equal(rows[0].slots.length, 48);
});

test('a fully active slot reads as one', () => {
  const spans = [{ t: 0, d: 1800, s: 'active' }];

  const rows = Render.weekMatrix(spans, 0, 1, 30);

  assert.equal(rows[0].slots[0], 1);
  assert.equal(rows[0].slots[1], 0);
});

test('a half active slot reads as one half', () => {
  const spans = [{ t: 0, d: 900, s: 'active' }];

  assert.equal(Render.weekMatrix(spans, 0, 1, 30)[0].slots[0], 0.5);
});

test('a span crossing a slot boundary is split between both slots', () => {
  const spans = [{ t: 900, d: 1800, s: 'active' }];

  const rows = Render.weekMatrix(spans, 0, 1, 30);

  assert.deepEqual(rows[0].slots.slice(0, 3), [0.5, 0.5, 0]);
});

test('away and off time is not counted as activity', () => {
  const spans = [{ t: 0, d: 1800, s: 'away' }, { t: 1800, d: 1800, s: 'off' }];

  const rows = Render.weekMatrix(spans, 0, 1, 30);

  assert.deepEqual(rows[0].slots.slice(0, 2), [0, 0]);
});

test('a laid out box carries the clipped bounds, not the item extent', () => {
  const [box] = Render.layout([{ t: -50, d: 200 }], 0, 100);

  assert.equal(box.start, 0);
  assert.equal(box.end, 100);
  assert.equal(box.item.t, -50);
});

test('the clock prints local hours and minutes', () => {
  assert.equal(Render.clock(1757656020, 180), '08:47');
  assert.equal(Render.clock(1757606400, 180), '19:00');
  assert.equal(Render.clock(0, 0), '00:00');
});

test('the clock follows the offset across a day boundary', () => {
  assert.equal(Render.clock(1757606400, -300), '11:00');
});

test('the weekday is the local one, in short Russian', () => {
  assert.equal(Render.weekday(1757606400, 180), 'чт');
  assert.equal(Render.weekday(1757606400 + 86400, 180), 'пт');
});

test('a moment inside an item finds it', () => {
  const item = { t: 100, d: 50 };

  assert.equal(Render.itemAt([item], 120), item);
  assert.equal(Render.itemAt([item], 100), item);
});

test('an item ends before its last second, so the next one owns the boundary', () => {
  const first = { t: 0, d: 100 };
  const second = { t: 100, d: 100 };

  assert.equal(Render.itemAt([first, second], 100), second);
});

test('a moment outside every item finds nothing', () => {
  assert.equal(Render.itemAt([{ t: 100, d: 50 }], 99), null);
  assert.equal(Render.itemAt([{ t: 100, d: 50 }], 150), null);
  assert.equal(Render.itemAt([], 0), null);
});

// Segments are absolutely positioned in paint order, so the one the cursor visibly sits on
// is the last that covers the moment — not the first.
test('overlapping items resolve to the one painted last', () => {
  const under = { t: 0, d: 100, lvl: 'bg' };
  const over = { t: 20, d: 10, lvl: 'fg' };

  assert.equal(Render.itemAt([under, over], 25), over);
});

const DAY = { from: 0, to: 86400 };

test('zooming in halves the window around the cursor', () => {
  const view = Render.zoom({ from: 0, to: 86400 }, 43200, 0.5, DAY);

  assert.deepEqual(view, { from: 21600, to: 64800 });
});

// The whole point of zooming at the cursor: whatever the eye was on stays under the pointer.
test('the moment under the cursor keeps its place in the window', () => {
  const view = Render.zoom({ from: 0, to: 86400 }, 21600, 0.5, DAY);

  assert.equal((21600 - view.from) / (view.to - view.from), 0.25);
});

test('zooming out at the day edge shifts the window instead of running past it', () => {
  const view = Render.zoom({ from: 0, to: 3600 }, 3600, 2, DAY);

  assert.deepEqual(view, { from: 0, to: 7200 });
});

test('zooming out never grows past the day', () => {
  assert.deepEqual(Render.zoom({ from: 0, to: 86400 }, 43200, 2, DAY), DAY);
});

// Buckets are ten seconds and the histogram is per minute: below ten minutes there is
// nothing left to reveal, only empty stretching.
test('zooming in stops at the minimum window', () => {
  const view = Render.zoom({ from: 0, to: 1200 }, 600, 0.25, DAY);

  assert.deepEqual(view, { from: 300, to: 900 });
});

test('panning moves the window without changing its width', () => {
  assert.deepEqual(Render.pan({ from: 3600, to: 7200 }, 1800, DAY),
                   { from: 5400, to: 9000 });
});

test('panning stops at the day edges and keeps the width', () => {
  assert.deepEqual(Render.pan({ from: 0, to: 3600 }, -1800, DAY), { from: 0, to: 3600 });
  assert.deepEqual(Render.pan({ from: 82800, to: 86400 }, 1800, DAY),
                   { from: 82800, to: 86400 });
});

test('a full day is labelled every three hours, as it always was', () => {
  const ticks = Render.ticks(0, 86400, 0);

  assert.equal(ticks.length, 8);
  assert.deepEqual(ticks.slice(0, 3), [0, 10800, 21600]);
});

test('a one-hour window is labelled every ten minutes', () => {
  assert.deepEqual(Render.ticks(0, 3600, 0),
                   [0, 600, 1200, 1800, 2400, 3000]);
});

// Aligned to the day, not to the window: a zoomed axis still has to read 08:30, never 08:27.
test('ticks land on round clock times whatever the window edges are', () => {
  const ticks = Render.ticks(1000, 4600, 0);

  assert.equal(ticks[0], 1200);
  assert.ok(ticks.every(t => t % 600 === 0));
  assert.ok(ticks[ticks.length - 1] < 4600);
});

test('the tightest window is still labelled, and never with more than ten labels', () => {
  [600, 1800, 3600, 10800, 21600, 43200, 86400].forEach(width => {
    const ticks = Render.ticks(0, width, 0);
    assert.ok(ticks.length >= 4 && ticks.length <= 10,
              width + ' s window produced ' + ticks.length + ' labels');
  });
});

// Firefox reports a mouse notch in LINES and some browsers in PAGES, so a handler that reads
// the number as pixels moves the lane by three pixels per notch on one browser and by a whole
// day on another. Only the sign is safe without this.
test('a wheel delta in pixels is taken as it is', () => {
  assert.equal(Render.wheelPixels(120, 0, 990), 120);
  assert.equal(Render.wheelPixels(-120, 0, 990), -120);
});

test('a wheel delta in lines becomes pixels', () => {
  assert.equal(Render.wheelPixels(3, 1, 990), 48);
  assert.equal(Render.wheelPixels(-3, 1, 990), -48);
});

test('a wheel delta in pages becomes the width of what it scrolls', () => {
  assert.equal(Render.wheelPixels(1, 2, 990), 990);
});

// Shift+wheel is delivered as a HORIZONTAL delta by some browsers and as a vertical one with
// a modifier by others, and a two-finger swipe is horizontal outright. Reading whichever axis
// actually moved is what makes one branch answer all three.
test('the axis that moved is the one that scrolls the lane', () => {
  assert.equal(Render.wheelAxis(120, 0), 120);
  assert.equal(Render.wheelAxis(0, 120), 120);
  assert.equal(Render.wheelAxis(-120, 0), -120);
});

test('a diagonal swipe follows its longer side', () => {
  assert.equal(Render.wheelAxis(5, -120), -120);
  assert.equal(Render.wheelAxis(-120, 5), -120);
});

test('a wheel that moved on neither axis scrolls nothing', () => {
  assert.equal(Render.wheelAxis(0, 0), 0);
});
