(function (root) {
  'use strict';

  function overlap(span, from, to) {
    return Math.max(0, Math.min(span.t + span.d, to) - Math.max(span.t, from));
  }

  // Plain sums over the selected window. Deliberately independent of the regime
  // thresholds, so moving the sliders never changes these numbers.
  function stats(spans, gameSpans, from, to) {
    const total = Math.max(0, to - from);
    const sum = state => spans
      .filter(s => s.s === state)
      .reduce((acc, s) => acc + overlap(s, from, to), 0);

    const active = sum('active');
    const away = sum('away');
    const off = sum('off');
    const gameFg = (gameSpans || [])
      .filter(s => s.lvl === 'fg')
      .reduce((acc, s) => acc + overlap(s, from, to), 0);

    const pctOf = value => (total === 0 ? 0 : Math.round((100 * value) / total));
    const notAtPc = away + off;

    return {
      total: total,
      active: active,
      away: away,
      off: off,
      gameFg: gameFg,
      pct: {
        active: pctOf(active),
        away: pctOf(away),
        off: pctOf(off),
        gameFg: pctOf(gameFg)
      },
      ratio: notAtPc === 0 ? null : active / notAtPc
    };
  }

  root.Selection = { stats: stats };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
