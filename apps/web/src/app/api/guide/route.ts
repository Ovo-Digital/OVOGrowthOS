import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';

const headers = { 'Cache-Control': 'private, no-store', Vary: 'Authorization', 'X-Content-Type-Options': 'nosniff' };
const response = (body: unknown, status = 200) => Response.json(body, { status, headers });

export async function GET(request: Request) {
  const authorization = request.headers.get('authorization');
  if (!authorization?.startsWith('Bearer ')) return response({ error: 'Rehberi açmak için giriş yapın.' }, 401);
  try {
    const session = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080'}/api/auth/me`, {
      headers: { authorization }, cache: 'no-store', redirect: 'error', signal: AbortSignal.timeout(5000),
    });
    if (session.status === 401) return response({ error: 'Oturumunuz sona erdi. Yeniden giriş yapın.' }, 401);
    if (!session.ok) return response({ error: 'Hesap yetkisi doğrulanamadı. Daha sonra tekrar deneyin.' }, 503);
    const account = await session.json() as { role?: string };
    if (!account.role || !['Admin', 'Partner', 'Analyst'].includes(account.role))
      return response({ error: 'Bu rehber yalnız iç ekip hesaplarına açıktır.' }, 403);
    const name = 'OVO_GROWTH_OS_KULLANIM_REHBERI.md';
    // In a checkout the root file is authoritative; /app/name is the packaged Docker fallback.
    for (const file of [path.resolve(process.cwd(), '..', '..', name), path.resolve(process.cwd(), name)]) {
      try {
        const [markdown, info] = await Promise.all([readFile(file, 'utf8'), stat(file)]);
        return response({ markdown, updatedAt: info.mtime.toISOString() });
      } catch (error) {
        if (!(error instanceof Error && 'code' in error && error.code === 'ENOENT')) throw error;
      }
    }
    return response({ error: 'Rehber dosyası bu sürümde bulunamadı. Yöneticinize bildirin.' }, 503);
  } catch {
    return response({ error: 'Rehber güvenli biçimde yüklenemedi. Bağlantıyı kontrol edip tekrar deneyin.' }, 503);
  }
}
