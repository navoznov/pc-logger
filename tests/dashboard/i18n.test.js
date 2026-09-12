const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const { I18n } = require('../../src/PcLogger.Core/dashboard/i18n.js');

const TEMPLATE = fs.readFileSync(
  path.join(__dirname, '../../src/PcLogger.Core/dashboard/dashboard.template.html'), 'utf8');

test('falls back to English and then to the key itself', () => {
  I18n.set('ru');
  assert.equal(I18n.t('legend.break'), 'перерыв');
  assert.equal(I18n.t('no.such.key'), 'no.such.key');
});

test('an unknown language falls back to English', () => {
  I18n.set('de');
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

  assert.ok(keys.size > 0, 'the template carries no data-i18n markup at all');
  for (const key of keys) {
    assert.ok(key in I18n.DICTS.en, 'template key missing from the dictionary: ' + key);
  }
});
