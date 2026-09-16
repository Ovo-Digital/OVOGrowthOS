import test from 'node:test';
import assert from 'node:assert/strict';
import { targetMargin } from '../src/lib/period-input.ts';

test('Hedef marjı yüzde olarak girilir; oran olarak gönderilir', () => {
  assert.equal(targetMargin('30'), .3);
  assert.equal(targetMargin('45,25'), .4525);
  assert.equal(targetMargin('0'), 0);
  assert.equal(targetMargin('100'), 1);
  for (const value of ['', '-1', '100,01', '1,001', '0.3', 'bilinmiyor']) assert.throws(() => targetMargin(value));
});
