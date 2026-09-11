(function (root) {
  'use strict';

  // Turns presence spans into screen-time blocks and the gaps between them.
  // A gap shorter than microGap is absorbed into the block; a gap of at least
  // breakMinutes closes it; anything in between is an insufficient break that
  // is drawn separately but does not reset the block.
  function buildBlocks(spans, opts) {
    const micro = opts.microGap * 60;
    const full = opts.breakMinutes * 60;
    const limit = (opts.sessionMinutes + opts.tolerance) * 60;

    const active = spans.filter(s => s.s === 'active').sort((a, b) => a.t - b.t);
    const blocks = [];
    const gaps = [];

    let start = null;
    let end = null;
    let screen = 0;

    const close = () => {
      if (start === null) return;
      blocks.push({
        t: start,
        d: end - start,
        screen: screen,
        verdict: end - start > limit ? 'over' : 'ok'
      });
      start = null;
      screen = 0;
    };

    for (const span of active) {
      if (start === null) {
        start = span.t;
        end = span.t + span.d;
        screen = span.d;
        continue;
      }

      const gap = span.t - end;
      if (gap >= full) {
        gaps.push({ t: end, d: gap, kind: 'break' });
        close();
        start = span.t;
        end = span.t + span.d;
        screen = span.d;
        continue;
      }

      if (gap > 0) gaps.push({ t: end, d: gap, kind: gap < micro ? 'micro' : 'short' });
      end = span.t + span.d;
      screen += span.d;
    }

    close();
    return { blocks: blocks, gaps: gaps };
  }

  root.Regime = { buildBlocks: buildBlocks };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
