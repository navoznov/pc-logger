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

  root.Render = { fmtDuration: fmtDuration, layout: layout,
                  dayBounds: dayBounds, weekMatrix: weekMatrix,
                  clock: clock, weekday: weekday, itemAt: itemAt };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
