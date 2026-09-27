import assert from 'node:assert/strict';
import test from 'node:test';
import { readdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) files.push(...await walk(path));
    else if (/\.(ts|tsx)$/.test(entry.name)) files.push(path);
  }
  return files;
}

test('Arayüz tarayıcının prompt/confirm diyaloglarını kullanmaz', async () => {
  const src = fileURLToPath(new URL('../src', import.meta.url));
  const offenders = [];
  for (const file of await walk(src)) {
    const text = (await readFile(file, 'utf8')).split('await confirm(').join('').split('await prompt(').join('');
    if (/(?:window\.)?(?:confirm|prompt)\s*\(/.test(text)) offenders.push(file.slice(src.length + 1));
  }
  assert.deepEqual(offenders, []);
});

test('Listeli ekranlar ortak yükleme, boş ve hata durumlarını kullanıyor', async () => {
  const src = fileURLToPath(new URL('../src', import.meta.url));
  const pages = ['brands', 'evaluations', 'deals', 'performance', 'activity', 'notifications', 'commissions', 'mail-deliveries', 'data-quality', 'users'];
  for (const page of pages) {
    const text = await readFile(join(src, 'app', page, 'page.tsx'), 'utf8');
    for (const state of ['LoadingState', 'EmptyState', 'ErrorState'])
      assert.ok(text.includes(`${state}`), `${page}/page.tsx içinde ${state} yok`);
    assert.ok(/from ['"]@\/components\/ui\/core['"]/.test(text), `${page}/page.tsx ortak bileşenleri içe aktarmıyor`);
  }
});
