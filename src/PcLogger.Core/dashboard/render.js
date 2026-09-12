(function (root) {
  'use strict';

  function fmtDuration(seconds) {
    const total = Math.max(0, Math.floor(seconds / 60));
    const hours = Math.floor(total / 60);
    const minutes = total % 60;
    if (hours === 0) return minutes + ' м';
    return hours + ' ч ' + String(minutes).padStart(2, '0') + ' м';
  }

  // Every clock in the report is local wall time built by shifting the epoch and reading the
  // UTC fields back: the browser's own zone is irrelevant, because the report can be opened on
  // a machine that is not the one that recorded it.
  function clock(ts, tzOffsetMinutes) {
    return new Date((ts + tzOffsetMinutes * 60) * 1000).toISOString().slice(11, 16);
  }

  const WEEKDAYS = ['вс', 'пн', 'вт', 'ср', 'чт', 'пт', 'сб'];

  function weekday(ts, tzOffsetMinutes) {
    return WEEKDAYS[new Date((ts + tzOffsetMinutes * 60) * 1000).getUTCDay()];
  }

  // What sits under a moment. Searched from the end, because items are painted in order and
  // the one the cursor visibly points at is the last that covers it.
  function itemAt(items, t) {
    for (let i = (items || []).length - 1; i >= 0; i--) {
      const item = items[i];
      if (t >= item.t && t < item.t + item.d) return item;
    }
    return null;
  }

  function layout(items, from, to) {
    const window = to - from;
    if (window <= 0) return [];

    return items
      .map(function (item) {
        const start = Math.max(item.t, from);
        const end = Math.min(item.t + item.d, to);
        return { start: start, end: end, item: item };
      })
      .filter(function (box) { return box.end > box.start; })
      .map(function (box) {
        return {
          left: ((box.start - from) / window) * 100,
          width: ((box.end - box.start) / window) * 100,
          // The clipped bounds travel with the box: a caller that wants to label a segment
          // needs the part that is actually on screen, not the item's full extent.
          start: box.start,
          end: box.end,
          item: box.item
        };
      });
  }

  function dayBounds(ts, tzOffsetMinutes) {
    const offset = tzOffsetMinutes * 60;
    const local = ts + offset;
    const from = local - mod(local, 86400) - offset;
    return { from: from, to: from + 86400 };
  }

  function mod(value, size) {
    return ((value % size) + size) % size;
  }

  // Folds presence spans into a day x slot grid of activity density: each cell is
  // the share of its slot that was active, so a heatmap can shade it directly.
  // The caller passes a day boundary that already carries the local-time offset,
  // which is why no timezone argument appears here.
  function weekMatrix(spans, lastDayFrom, days, slotMinutes) {
    const slotSeconds = slotMinutes * 60;
    const perDay = Math.round(86400 / slotSeconds);
    const active = spans.filter(function (s) { return s.s === 'active'; });
    const rows = [];

    for (let day = days - 1; day >= 0; day--) {
      const from = lastDayFrom - day * 86400;
      const slots = new Array(perDay).fill(0);

      active.forEach(function (s) {
        for (let i = 0; i < perDay; i++) {
          const slotFrom = from + i * slotSeconds;
          const covered = Math.max(0,
            Math.min(s.t + s.d, slotFrom + slotSeconds) - Math.max(s.t, slotFrom));
          if (covered > 0) slots[i] += covered / slotSeconds;
        }
      });

      rows.push({ from: from, slots: slots.map(function (v) { return Math.min(1, v); }) });
    }

    return rows;
  }

  // Ten minutes is the floor on purpose: buckets are ten seconds wide and the intensity
  // histogram is per minute, so a tighter window stretches the same rectangles without
  // revealing anything that was not already on screen.
  const MIN_WINDOW = 600;

  // Steps a clock reads without arithmetic. The axis takes the tightest one that still fits
  // inside MAX_TICKS labels, so a zoomed lane keeps about the density of the full day.
  const TICK_STEPS = [60, 120, 300, 600, 900, 1800, 3600, 7200, 10800];
  const MAX_TICKS = 10;

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  // Slides a window of a fixed width until it sits inside the bounds. Clamping the two edges
  // separately would instead shorten the window at the day's edges, so zooming out at 23:50
  // would hand back less than the width it just promised.
  function fit(from, width, bounds) {
    const start = clamp(from, bounds.from, bounds.to - width);
    return { from: start, to: start + width };
  }

  // factor < 1 zooms in, factor > 1 zooms out. The moment under the cursor keeps its
  // position in the window, which is what makes the wheel land where the eye is pointing.
  function zoom(view, anchor, factor, bounds) {
    const width = view.to - view.from;
    const next = Math.round(clamp(width * factor, MIN_WINDOW, bounds.to - bounds.from));
    const ratio = width === 0 ? 0.5 : (anchor - view.from) / width;
    return fit(Math.round(anchor - ratio * next), next, bounds);
  }

  // A wheel event reports its delta in one of three units, and only the browser knows which.
  // Firefox sends a mouse notch as three LINES, some browsers send whole PAGES, and a handler
  // that reads either number as pixels moves the lane by almost nothing on the first and by a
  // whole day on the second.
  const LINE_HEIGHT = 16;

  // Which way the wheel actually went. One branch has to answer three devices: a plain wheel
  // reports on Y, a two-finger swipe on X, and Shift+wheel lands on either — some browsers
  // turn it into a horizontal delta, others keep it vertical and only set the modifier.
  function wheelAxis(deltaX, deltaY) {
    return Math.abs(deltaX) > Math.abs(deltaY) ? deltaX : deltaY;
  }

  function wheelPixels(delta, mode, pageSize) {
    if (mode === 1) return delta * LINE_HEIGHT;
    if (mode === 2) return delta * pageSize;
    return delta;
  }

  function pan(view, seconds, bounds) {
    return fit(Math.round(view.from + seconds), view.to - view.from, bounds);
  }

  // Labels are multiples of the step counted FROM the origin, not from the window: a window
  // that starts at 08:27 must still be labelled 08:30, 08:40, and so on. The origin is the
  // day boundary, which already carries the local-time offset.
  function ticks(from, to, origin) {
    const width = to - from;
    const step = TICK_STEPS.find(function (s) { return width / s <= MAX_TICKS; }) ||
                 TICK_STEPS[TICK_STEPS.length - 1];
    const out = [];
    for (let t = origin + Math.ceil((from - origin) / step) * step; t < to; t += step) {
      out.push(t);
    }
    return out;
  }

  root.Render = { fmtDuration: fmtDuration, layout: layout,
                  dayBounds: dayBounds, weekMatrix: weekMatrix,
                  clock: clock, weekday: weekday, itemAt: itemAt,
                  zoom: zoom, pan: pan, ticks: ticks,
                  wheelAxis: wheelAxis, wheelPixels: wheelPixels,
                  MIN_WINDOW: MIN_WINDOW };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
