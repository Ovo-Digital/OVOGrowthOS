import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { GET } from '../src/app/api/guide/route.ts';

test('Rehber içeriği sunucuda doğrulanmış iç ekip hesabı gerektirir', async t => {
  const upstream = t.mock.method(globalThis, 'fetch');
  const request = token => new Request('http://localhost/api/guide', { headers: token ? { authorization: `Bearer ${token}` } : {} });
  const denied = async (token, expected) => {
    const r = await GET(request(token));
    assert.equal(r.status, expected);
    assert.equal(r.headers.get('cache-control'), 'private, no-store');
    assert.equal(r.headers.get('vary'), 'Authorization');
    const body = await r.json();
    assert.equal('markdown' in body, false);
    assert.equal('updatedAt' in body, false);
  };
  await denied(null, 401);
  assert.equal(upstream.mock.callCount(), 0);
  upstream.mock.mockImplementation(async () => new Response(null, { status: 401 }));
  await denied('expired', 401);
  for (const role of ['BrandClient', 'Unknown', undefined]) {
    upstream.mock.mockImplementation(async () => Response.json({ role }));
    await denied('customer', 403);
  }
  upstream.mock.mockImplementation(async () => { throw new Error('network unavailable'); });
  await denied('unverified', 503);
  for (const role of ['Admin', 'Partner', 'Analyst']) {
    upstream.mock.mockImplementation(async (_url, options) => {
      assert.equal(options.headers.authorization, 'Bearer internal');
      assert.equal(options.cache, 'no-store');
      assert.equal(options.redirect, 'error');
      return Response.json({ role });
    });
    const response = await GET(request('internal'));
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.match(body.markdown, /OVO Growth OS/);
    assert.equal(body.markdown, await readFile(new URL('../../../OVO_GROWTH_OS_KULLANIM_REHBERI.md', import.meta.url), 'utf8'));
    assert.ok(Date.parse(body.updatedAt));
  }
});
