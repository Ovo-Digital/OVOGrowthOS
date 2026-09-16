import test from 'node:test';
import assert from 'node:assert/strict';
import { planningHours } from '../src/lib/period-input.ts';
import { planningWeek } from '../src/lib/planning-week.ts';

test('Haftalık saat bilinmeyen boş girişle gerçek sıfırı ayırır', () => {
  assert.equal(planningHours('0'), 0); assert.equal(planningHours('8,25'), 8.25); assert.equal(planningHours('168'), 168);
  for (const input of ['', '-1', '168,01', '1,001', '8.5']) assert.throws(() => planningHours(input));
});
test('Plan haftası pazartesiden başlar; yıl değişiminde de aynı hafta korunur', () => {
  assert.equal(planningWeek('2026-09-17'), '2026-09-14');
  assert.equal(planningWeek('2026-09-20'), '2026-09-14');
  assert.equal(planningWeek('2026-09-21'), '2026-09-21');
  assert.equal(planningWeek('2027-01-01'), '2026-12-28');
});
