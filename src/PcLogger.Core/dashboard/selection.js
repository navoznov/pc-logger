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

    // Intersected with presence on purpose. A game can hold the foreground while nobody is
    // at the keyboard, so a plain sum of fg spans can exceed the time spent at the PC, and a
    // panel that prints "За ПК 23 %" above "Игра в фокусе 45 %" reads as broken. The game
    // TRACK still shows the physical fact; this is the answer to a different question — how
    // much of the child's time at the PC went into a game.
    const activeSpans = spans.filter(s => s.s === 'active');
    const gameFg = (gameSpans || [])
      .filter(s => s.lvl === 'fg')
      .reduce((acc, g) => acc + activeSpans.reduce(
        (inner, a) => inner + overlap(a, Math.max(g.t, from), Math.min(g.t + g.d, to)), 0), 0);

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
        // The complement, not an independent rounding. Presence spans partition the window,
        // so these two are the only categories a parent sees and they have to add up. Rounding
        // both halves of an exact 22.5 / 77.5 split prints 23 beside 78 — a visible 101 %.
        notAtPc: total === 0 ? 0 : 100 - pctOf(active),
        gameFg: pctOf(gameFg)
      },
      ratio: notAtPc === 0 ? null : active / notAtPc
    };
  }

  root.Selection = { stats: stats };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
