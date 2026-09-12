(function (root) {
  'use strict';

  // Every user-visible string in the report. Keys are semantic rather than English text:
  // half of these are two-sentence tooltips, which make unreadable keys and break on any
  // editorial change — and a semantic key privileges neither language.
  const DICTS = {
    ru: {
      $name: 'Русский',

      // Форматы и шапка
      'fmt.hm': '{h} ч {m} м',
      'fmt.m': '{m} м',
      'nav.prevDay': 'Предыдущий день',
      'nav.nextDay': 'Следующий день',
      'nav.prevWeek': 'Предыдущая неделя',
      'nav.nextWeek': 'Следующая неделя',
      'nav.pickDay': 'Выбрать день',
      'nav.mode.day': 'День',
      'nav.mode.week': 'Неделя',
      'nav.mode.tip': 'Переключить между лентой одного дня и сводкой за семь дней',
      'nav.theme': 'Тема',
      'nav.theme.tip': 'Светлая или тёмная тема. По умолчанию отчёт следует настройке системы.',

      // Слайдеры порогов
      'opt.session.label': 'Блок, мин',
      'opt.session.tip': 'Сколько минут подряд за ПК считается нормальным блоком. Блок длиннее этого порога плюс допуска помечается превышением.',
      'opt.break.label': 'Перерыв, мин',
      'opt.break.tip': 'Пауза такой длины и больше закрывает блок и засчитывается как полноценный перерыв.',
      'opt.micro.label': 'Микропауза, мин',
      'opt.micro.tip': 'Пауза короче этой поглощается блоком: отойти налить чай — не перерыв, а часть работы за ПК.',
      'opt.tol.label': 'Допуск, мин',
      'opt.tol.tip': 'Сколько минут сверх блока прощается, прежде чем вердикт станет «превышение».',
      'opt.norm.label': 'Норма в день, ч',
      'opt.norm.tip': 'Дневная норма экранного времени. Рисуется пунктиром на столбиках недели, на вердикт по блокам не влияет.',
      'opt.reset': 'Сбросить пороги',
      'opt.reset.tip': 'Вернуть все пять порогов к значениям по умолчанию',

      // Панель выделения
      'sel.prompt': 'Выдели участок на дорожке или на строке недели, чтобы увидеть статистику за период.',
      'sel.tip': 'Числа считаются от полной длины выделения и не зависят от слайдеров порогов',
      'sel.heading': 'Выделено: {from} – {to}',
      'sel.atPc': 'За ПК',
      'sel.notAtPc': 'Не за ПК',
      'sel.away': '— отходил',
      'sel.off': '— ПК был выключен',
      'sel.gameFg': 'Игра в фокусе',
      'sel.ratio': 'За ПК : не за ПК',
      'sel.dragging': 'отпустите кнопку, чтобы посчитать период',

      // Зум
      'zoom.hint': 'Ctrl/⌘+колесо — приблизить к курсору; колесо, Shift+колесо или свайп — сдвинуть',
      'zoom.out.tip': 'Отдалить: показать промежуток вдвое длиннее',
      'zoom.in.tip': 'Приблизить: показать промежуток вдвое короче',
      'zoom.all': 'Весь день',
      'zoom.all.tip': 'Вернуть ленту к целым суткам',

      // Дорожки
      'track.power.name': 'ПК включён',
      'track.power.tip': 'Были ли вообще записи. Пусто означает, что компьютер был выключен или спал, а не что за ним никого не было.',
      'track.power.nothing': 'ПК был выключен или спал',
      'track.active.name': 'Активность',
      'track.active.tip': 'Был ли ввод с клавиатуры, мыши или геймпада. Отсутствие ввода при включённом ПК читается как «отошёл».',
      'track.active.nothing': 'ввода не было',
      'track.game.name': 'Игра',
      'track.game.tip': 'Игра из папок в config.json: в фокусе или запущена в фоне. На вердикт по режиму не влияет никогда.',
      'track.game.nothing': 'игра не запущена',
      'track.regime.name': 'Режим',
      'track.regime.tip': 'Единственная дорожка-интерпретация: блоки, перерывы и вердикт. Перекрашивается слайдерами порогов.',
      'track.regime.nothing': 'данных нет',

      // Подсказка под курсором
      'cursor.intensity': 'Интенсивность ввода — {pct} %',
      'cursor.future': 'это время ещё не наступило',
      'cursor.away': 'отошёл, ввода не было',
      'cursor.input': 'был ввод',
      'cursor.game.fg': '{app} — в фокусе',
      'cursor.game.bg': '{app} — в фоне',
      'cursor.off': 'ПК выключен',
      'cursor.break': 'полноценный перерыв',
      'cursor.short': 'недостаточный перерыв',
      'cursor.block.ok': 'блок за ПК — в пределах нормы',
      'cursor.block.over': 'блок за ПК — превышение',

      // Гистограмма и легенда
      'bars.scale': 'интенсивность ввода — доля секунд с вводом в минуте',
      'legend.ok': 'в пределах нормы',
      'legend.ok.tip': 'Блок за ПК не длиннее порога «блок» плюс допуска',
      'legend.over': 'превышение',
      'legend.over.tip': 'Блок за ПК длиннее порога: помечен и цветом, и штриховкой',
      'legend.short': 'недостаточный перерыв',
      'legend.short.tip': 'Пауза длиннее микропаузы, но короче порога перерыва: блок не закрывает',
      'legend.break': 'перерыв',
      'legend.break.tip': 'Пауза не короче порога перерыва: блок закрыт, отдых засчитан',
      'legend.off': 'ПК выключен',
      'legend.off.tip': 'Записей нет: компьютер был выключен или спал',
      'legend.gameFg': 'игра в фокусе',
      'legend.gameFg.tip': 'Окно игры было на переднем плане',
      'legend.gameBg': 'игра в фоне',
      'legend.gameBg.tip': 'Игра запущена, но в фокусе было другое окно',

      // Баннеры
      'health.heading': 'Запись прерывалась в этот день.',
      'health.body': 'Пробелы ниже могут означать не отдых, а сбой сборщика.',
      'health.unknown': 'ошибка',
      'empty.note': 'За этот день записей нет: компьютер не включали или программа не работала. Пустые дорожки ниже не означают, что за ПК никого не было.',

      // KPI дня
      'kpi.total.label': 'Всего за ПК',
      'kpi.total.tip': 'Сумма времени с вводом за день. Минуты, когда ПК был включён, но ввода не было, сюда не входят.',
      'kpi.blocks.label': 'Блоков',
      'kpi.blocks.tip': 'Сколько раз за день начинался блок за ПК. Блок закрывается только полноценным перерывом.',
      'kpi.violations.label': 'Нарушений',
      'kpi.violations.tip': 'Блоки длиннее порога «блок» плюс допуска.',
      'kpi.longest.label': 'Самый длинный блок',
      'kpi.longest.tip': 'От начала до конца самого долгого блока, вместе с поглощёнными микропаузами.',
      'kpi.median.label': 'Медиана перерыва',
      'kpi.median.tip': 'Середина списка полноценных перерывов за день: половина была короче, половина длиннее. Устойчивее среднего — один шестичасовой сон не делает день образцовым.',

      // Таблица блоков и панель игр
      'table.start': 'Начало',
      'table.start.tip': 'Когда начался блок',
      'table.duration': 'Длительность',
      'table.duration.tip': 'От начала блока до его конца, вместе с поглощёнными паузами',
      'table.screen': 'Экранное время',
      'table.screen.tip': 'Только минуты с вводом внутри блока: длительность минус паузы',
      'table.breakAfter': 'Перерыв после',
      'table.breakAfter.tip': 'Пауза сразу после блока. Прочерк означает, что блок последний за день.',
      'table.topApp': 'Топ-приложение',
      'table.topApp.tip': 'Приложение, продержавшее фокус дольше всех внутри блока',
      'table.verdict': 'Вердикт',
      'table.verdict.tip': '«Превышение», если блок длиннее порога «блок» плюс допуска',
      'table.row.tip': 'Нажмите, чтобы посмотреть статистику за этот блок',
      'verdict.ok': 'ок',
      'verdict.over': 'превышение',
      'games.title': 'Во что играл: ',
      'games.none': 'в этот день игр в фокусе не было.',

      // Вид «Неделя»
      'week.cell.tip': '{day} {clock} · {pct} % · нажмите, чтобы открыть этот день',
      'week.bar.tip': '{day} · {dur} · нажмите, чтобы открыть этот день',
      'week.bar.tipViolations': '{day} · {dur}, нарушений: {n} · нажмите, чтобы открыть этот день',
      'week.norm': 'норма {dur}',
      'week.fullHeight': 'полная высота — {dur}',
      'week.kpi.badDays.label': 'Дней с нарушениями',
      'week.kpi.badDays.tip': 'Дни, в которых был хотя бы один блок длиннее порога «блок» плюс допуска',
      'week.kpi.badDays.value': '{bad} из {total}',
      'week.kpi.total.label': 'Всего за неделю',
      'week.kpi.total.tip': 'Сумма экранного времени за семь дней, которые показаны выше',
      'week.legend.screen': 'экранное время за день',
      'week.legend.violations': 'день с нарушениями'
    },
    en: {
      $name: 'English',

      // Formats and header
      'fmt.hm': '{h}h {m}m',
      'fmt.m': '{m}m',
      'nav.prevDay': 'Previous day',
      'nav.nextDay': 'Next day',
      'nav.prevWeek': 'Previous week',
      'nav.nextWeek': 'Next week',
      'nav.pickDay': 'Pick a day',
      'nav.mode.day': 'Day',
      'nav.mode.week': 'Week',
      'nav.mode.tip': 'Switch between the single-day lane and the seven-day summary',
      'nav.theme': 'Theme',
      'nav.theme.tip': 'Light or dark theme. By default the report follows the system setting.',

      // Threshold sliders
      'opt.session.label': 'Block, min',
      'opt.session.tip': 'How many consecutive minutes at the PC count as a normal block. A block longer than this threshold plus the tolerance is marked as over the limit.',
      'opt.break.label': 'Break, min',
      'opt.break.tip': 'A pause this long or longer closes the block and counts as a full break.',
      'opt.micro.label': 'Micro-pause, min',
      'opt.micro.tip': 'A pause shorter than this is absorbed into the block: stepping away to make tea is not a break but part of the time at the PC.',
      'opt.tol.label': 'Tolerance, min',
      'opt.tol.tip': 'How many minutes beyond the block are forgiven before the verdict becomes “over the limit”.',
      'opt.norm.label': 'Daily limit, h',
      'opt.norm.tip': 'The daily screen-time limit. Drawn as a dashed line across the week\'s bars; it does not affect the per-block verdict.',
      'opt.reset': 'Reset thresholds',
      'opt.reset.tip': 'Return all five thresholds to their defaults',

      // Selection panel
      'sel.prompt': 'Select a stretch of a track or of a week row to see the statistics for that period.',
      'sel.tip': 'The numbers are taken from the full length of the selection and do not depend on the threshold sliders',
      'sel.heading': 'Selected: {from} – {to}',
      'sel.atPc': 'At the PC',
      'sel.notAtPc': 'Away from the PC',
      'sel.away': '— stepped away',
      'sel.off': '— PC was off',
      'sel.gameFg': 'Game in focus',
      'sel.ratio': 'At the PC : away',
      'sel.dragging': 'release to measure the range',

      // Zoom
      'zoom.hint': 'Ctrl/⌘+wheel zooms to the cursor; wheel, Shift+wheel or a swipe pans',
      'zoom.out.tip': 'Zoom out: show twice as long a stretch',
      'zoom.in.tip': 'Zoom in: show half as long a stretch',
      'zoom.all': 'Whole day',
      'zoom.all.tip': 'Return the lane to the whole day',

      // Tracks
      'track.power.name': 'PC on',
      'track.power.tip': 'Whether anything was recorded at all. Empty means the computer was off or asleep, not that nobody was at it.',
      'track.power.nothing': 'the PC was off or asleep',
      'track.active.name': 'Input',
      'track.active.tip': 'Whether there was keyboard, mouse or gamepad input. No input while the PC is on reads as “stepped away”.',
      'track.active.nothing': 'there was no input',
      'track.game.name': 'Game',
      'track.game.tip': 'A game from the folders in config.json: in focus or running in the background. It never affects the regime verdict.',
      'track.game.nothing': 'no game was running',
      'track.regime.name': 'Regime',
      'track.regime.tip': 'The one interpreted track: blocks, breaks and the verdict. The threshold sliders repaint it.',
      'track.regime.nothing': 'no data',

      // Cursor tooltip
      'cursor.intensity': 'Input intensity — {pct} %',
      'cursor.future': 'this time has not come yet',
      'cursor.away': 'stepped away, no input',
      'cursor.input': 'there was input',
      'cursor.game.fg': '{app} — in focus',
      'cursor.game.bg': '{app} — in the background',
      'cursor.off': 'PC off',
      'cursor.break': 'a full break',
      'cursor.short': 'too short a break',
      'cursor.block.ok': 'block at the PC — within the limit',
      'cursor.block.over': 'block at the PC — over the limit',

      // Histogram and legend
      'bars.scale': 'input intensity — the share of seconds with input in a minute',
      'legend.ok': 'within the limit',
      'legend.ok.tip': 'A block at the PC no longer than the “block” threshold plus the tolerance',
      'legend.over': 'over the limit',
      'legend.over.tip': 'A block at the PC longer than the threshold: marked by colour and by hatching',
      'legend.short': 'too short a break',
      'legend.short.tip': 'A pause longer than a micro-pause but shorter than the break threshold: it does not close the block',
      'legend.break': 'break',
      'legend.break.tip': 'A pause at least as long as the break threshold: the block is closed and the rest counted',
      'legend.off': 'PC off',
      'legend.off.tip': 'No records: the computer was off or asleep',
      'legend.gameFg': 'game in focus',
      'legend.gameFg.tip': 'The game window held the foreground',
      'legend.gameBg': 'game in the background',
      'legend.gameBg.tip': 'The game is running, but another window held the focus',

      // Banners
      'health.heading': 'Recording was interrupted on this day.',
      'health.body': 'The gaps below may mean a collector failure rather than rest.',
      'health.unknown': 'error',
      'empty.note': 'There are no records for this day: the computer was not turned on, or the program was not running. The empty tracks below do not mean nobody was at the PC.',

      // Day KPIs
      'kpi.total.label': 'Total at the PC',
      'kpi.total.tip': 'The sum of time with input for the day. Minutes when the PC was on but there was no input are not counted here.',
      'kpi.blocks.label': 'Blocks',
      'kpi.blocks.tip': 'How many times a block at the PC began during the day. Only a full break closes a block.',
      'kpi.violations.label': 'Violations',
      'kpi.violations.tip': 'Blocks longer than the “block” threshold plus the tolerance.',
      'kpi.longest.label': 'Longest block',
      'kpi.longest.tip': 'From the start to the end of the longest block, including absorbed micro-pauses.',
      'kpi.median.label': 'Median break',
      'kpi.median.tip': 'The middle of the day\'s list of full breaks: half were shorter, half longer. Steadier than the mean — one six-hour sleep does not make a day exemplary.',

      // Block table and games panel
      'table.start': 'Start',
      'table.start.tip': 'When the block began',
      'table.duration': 'Duration',
      'table.duration.tip': 'From the start of the block to its end, including absorbed pauses',
      'table.screen': 'Screen time',
      'table.screen.tip': 'Only the minutes with input inside the block: the duration minus the pauses',
      'table.breakAfter': 'Break after',
      'table.breakAfter.tip': 'The pause right after the block. A dash means the block was the last of the day.',
      'table.topApp': 'Top app',
      'table.topApp.tip': 'The app that held the focus longest inside the block',
      'table.verdict': 'Verdict',
      'table.verdict.tip': '“Over the limit” if the block is longer than the “block” threshold plus the tolerance',
      'table.row.tip': 'Click to see the statistics for this block',
      'verdict.ok': 'ok',
      'verdict.over': 'over',
      'games.title': 'Games played: ',
      'games.none': 'no game held the foreground on this day.',

      // Week view
      'week.cell.tip': '{day} {clock} · {pct} % · click to open this day',
      'week.bar.tip': '{day} · {dur} · click to open this day',
      'week.bar.tipViolations': '{day} · {dur}, violations: {n} · click to open this day',
      'week.norm': 'limit {dur}',
      'week.fullHeight': 'full height — {dur}',
      'week.kpi.badDays.label': 'Days with violations',
      'week.kpi.badDays.tip': 'Days with at least one block longer than the “block” threshold plus the tolerance',
      'week.kpi.badDays.value': '{bad} of {total}',
      'week.kpi.total.label': 'Week total',
      'week.kpi.total.tip': 'The sum of screen time over the seven days shown above',
      'week.legend.screen': 'screen time per day',
      'week.legend.violations': 'a day with violations'
    }
  };

  const FALLBACK = 'en';
  let current = FALLBACK;

  function set(lang) { current = DICTS[lang] ? lang : FALLBACK; }
  function lang() { return current; }
  function langs() { return Object.keys(DICTS); }

  // A key that reaches the screen is a visible bug, which is the point: a blank label or an
  // "undefined" would hide a missing translation instead of reporting it.
  function t(key, params) {
    let text = DICTS[current][key];
    if (text === undefined) text = DICTS[FALLBACK][key];
    if (text === undefined) return key;
    if (!params) return text;

    return text.replace(/\{(\w+)\}/g, function (whole, name) {
      return params[name] === undefined ? whole : String(params[name]);
    });
  }

  root.I18n = { t: t, set: set, lang: lang, langs: langs, DICTS: DICTS };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
