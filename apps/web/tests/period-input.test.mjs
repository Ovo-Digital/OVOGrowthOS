import assert from 'node:assert/strict';
import test from 'node:test';
import { periodNumber, periodGroups, periodLabels } from '../src/lib/period-input.ts';

test('Türkçe tutarlar, dört ondalık basamak ve gerçek sıfır kabul edilir', () => {
  for (const [raw, expected] of [['0', 0], ['1234,56', 1234.56], ['1.234,5678', 1234.5678], [' 12,5 ', 12.5]])
    assert.equal(periodNumber(raw), expected);
  assert.equal(periodNumber('-1.234,5678', false, true), -1234.5678);
});
test('Boş, belirsiz, hatalı, taşan veya negatif giriş sessizce değiştirilmez', () => {
  for (const raw of ['', ' ', '1.23', '1,234.56', '1,12345', '1e6', 'NaN', 'Infinity', '-1', '100000000000000', '99999999999999,9999', '12 TL'])
    assert.throws(() => periodNumber(raw), undefined, raw);
  assert.throws(() => periodNumber('2,5', true));
  assert.throws(() => periodNumber('2147483648', true));
  assert.equal(periodNumber('2147483647', true), 2147483647);
});
test('Düzenlenebilir tüm alanların kullanıcı etiketi var', () => {
  const fields = periodGroups.flatMap(group => group.fields);
  assert.equal(fields.length, 21);
  assert.equal(new Set(fields).size, fields.length);
  for (const field of fields) assert.ok(periodLabels[field]);
});
