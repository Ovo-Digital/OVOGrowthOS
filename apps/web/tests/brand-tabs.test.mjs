import assert from 'node:assert/strict';
import test from 'node:test';
import { brandTabs, isBrandTab, resolveBrandTab } from '../src/lib/brand-tabs.ts';

test('Marka sekmeleri tek ve Türkçe etiketli', () => {
  const ids = brandTabs.map(tab => tab.id);
  assert.deepEqual(ids, ['ozet', 'gorusme', 'performans', 'baglantilar']);
  assert.equal(new Set(ids).size, ids.length);
  for (const tab of brandTabs) assert.ok(tab.label.trim().length > 0, `${tab.id} etiketsiz`);
  assert.ok(isBrandTab('performans'));
  assert.equal(isBrandTab('yok'), false);
  assert.equal(isBrandTab(null), false);
});

test('Açılışta adres bilgisi yoksa Genel bakış sekmesi seçilir', () => {
  assert.deepEqual(resolveBrandTab('', null), { tab: 'ozet', anchor: null });
  assert.deepEqual(resolveBrandTab('', 'yok'), { tab: 'ozet', anchor: null });
});

test('Adres çubuğundaki bolum değeri geçerli sekme olursa o sekme açılır', () => {
  assert.deepEqual(resolveBrandTab('', 'performans'), { tab: 'performans', anchor: null });
  assert.deepEqual(resolveBrandTab('', 'baglantilar'), { tab: 'baglantilar', anchor: null });
});

test('Sekme adı çapa olarak da kullanılabilir', () => {
  assert.deepEqual(resolveBrandTab('#gorusme', null), { tab: 'gorusme', anchor: null });
});

test('Eski #team-work bağlantıları Görüşme ve ekip sekmesini açar ve çapayı korur', () => {
  assert.deepEqual(resolveBrandTab('#team-work', null), { tab: 'gorusme', anchor: 'team-work' });
  assert.deepEqual(resolveBrandTab('#team-work', 'performans'), { tab: 'gorusme', anchor: 'team-work' });
});

test('Bilinmeyen çapa sekmeyi değiştirmez', () => {
  assert.deepEqual(resolveBrandTab('#bilinmeyen', 'performans'), { tab: 'performans', anchor: null });
});
