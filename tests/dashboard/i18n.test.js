const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const { I18n } = require('../../src/PcLogger.Core/dashboard/i18n.js');

const DASHBOARD = path.join(__dirname, '../../src/PcLogger.Core/dashboard');
const TEMPLATE = fs.readFileSync(path.join(DASHBOARD, 'dashboard.template.html'), 'utf8');
const SOURCES = ['dashboard.template.html', 'render.js', 'regime.js', 'selection.js'];
const ALL_SOURCE = SOURCES
  .map(function (name) { return fs.readFileSync(path.join(DASHBOARD, name), 'utf8'); })
  .join('\n');

test('falls back to English and then to the key itself', () => {
  I18n.set('ru');
  assert.equal(I18n.t('legend.break'), 'перерыв');
  assert.equal(I18n.t('no.such.key'), 'no.such.key');
});

test('an unknown language falls back to English', () => {
  // 'de' and friends are candidates, not guarantees — the design's own example (§8) adds a
  // 'de' dictionary, so this picks whichever candidate nobody has added yet instead of
  // hardcoding one that may legitimately exist.
  const unsupported = ['de', 'fr', 'ja', 'zz'].find(function (code) { return !I18n.DICTS[code]; });
  assert.ok(unsupported, 'all candidate codes are now supported languages — add another candidate');
  I18n.set(unsupported);
  assert.equal(I18n.lang(), 'en');
  assert.equal(I18n.t('legend.break'), 'break');
});

test('substitutes named parameters', () => {
  I18n.set('en');
  assert.equal(I18n.t('week.kpi.badDays.value', { bad: 2, total: 7 }), '2 of 7');
  I18n.set('ru');
  assert.equal(I18n.t('week.kpi.badDays.value', { bad: 2, total: 7 }), '2 из 7');
});

test('leaves an unsupplied placeholder visible rather than printing undefined', () => {
  I18n.set('en');
  assert.equal(I18n.t('week.norm', {}), 'limit {dur}');
});

test('every language carries exactly the same keys', () => {
  const reference = Object.keys(I18n.DICTS.en).sort();
  for (const lang of I18n.langs()) {
    const keys = Object.keys(I18n.DICTS[lang]).sort();
    assert.deepEqual(keys, reference, 'language ' + lang + ' differs from en');
  }
});

test('every language names itself for the switcher', () => {
  for (const lang of I18n.langs()) {
    assert.equal(typeof I18n.DICTS[lang].$name, 'string');
    assert.ok(I18n.DICTS[lang].$name.length > 0);
  }
});

test('every key marked up in the template exists in the dictionary', () => {
  const keys = new Set();
  const pattern = /data-i18n(?:-title|-aria)?="([^"]+)"/g;
  let match;
  while ((match = pattern.exec(TEMPLATE)) !== null) keys.add(match[1]);

  assert.ok(keys.size > 61, 'the data-i18n markup scan found too few keys — it has drifted');
  for (const key of keys) {
    assert.ok(key in I18n.DICTS.en, 'template key missing from the dictionary: ' + key);
  }
});

test('no key in the dictionary is dead', () => {
  for (const key of Object.keys(I18n.DICTS.en)) {
    if (key.charAt(0) === '$') continue;   // read by the switcher, never by t()
    assert.ok(ALL_SOURCE.includes("'" + key + "'") || ALL_SOURCE.includes('"' + key + '"'),
      'dictionary key used nowhere: ' + key);
  }
});

test('no Russian text is hard-coded outside the dictionary', () => {
  for (const name of SOURCES) {
    const offenders = fs.readFileSync(path.join(DASHBOARD, name), 'utf8')
      .split('\n')
      .map(function (line, index) { return [index + 1, line]; })
      .filter(function (pair) { return /[А-Яа-яЁё]/.test(pair[1]); });
    assert.deepEqual(offenders, [], name + ' still carries Russian outside i18n.js');
  }
});

test('every key passed to t() as a literal exists in the dictionary', () => {
  const pattern = /I18n\.t\(\s*'([^']+)'/gi;
  let match;
  let seen = 0;
  while ((match = pattern.exec(ALL_SOURCE)) !== null) {
    seen++;
    assert.ok(match[1] in I18n.DICTS.en, 't() called with an unknown key: ' + match[1]);
  }
  assert.ok(seen > 20, 'the scan found almost no t() calls — the regex has drifted');
});
