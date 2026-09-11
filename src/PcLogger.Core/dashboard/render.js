(function (root) {
  'use strict';

  function fmtDuration(seconds) {
    const total = Math.max(0, Math.floor(seconds / 60));
    const hours = Math.floor(total / 60);
    const minutes = total % 60;
    if (hours === 0) return minutes + ' м';
    return hours + ' ч ' + String(minutes).padStart(2, '0') + ' м';
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
                  dayBounds: dayBounds, weekMatrix: weekMatrix };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
