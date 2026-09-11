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

  root.Render = { fmtDuration: fmtDuration, layout: layout, dayBounds: dayBounds };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
