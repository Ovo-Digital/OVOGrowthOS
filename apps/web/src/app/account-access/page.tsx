'use client';
import Link from 'next/link';
import { FormEvent, useEffect, useState } from 'react';
import { API_URL } from '@/lib/api';

export default function AccountAccess() {
  const [token, setToken] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [done, setDone] = useState(false);
  useEffect(() => {
    const value = new URLSearchParams(window.location.hash.slice(1)).get('token') ?? '';
    // Fragment never reaches the web server; remove it from the address bar without persisting it.
    window.history.replaceState(null, '', window.location.pathname);
    // One-time hydration of external browser-only state, not state derived from props.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (value) setToken(value);
  }, []);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (busy) return;
    const form = e.currentTarget; const fields = new FormData(form);
    if (token && fields.get('password') !== fields.get('confirmation')) { setError('İki şifre aynı olmalıdır.'); return; }
    setBusy(true); setError(''); setNotice('');
    try {
      const response = await fetch(`${API_URL}/api/auth/${token ? 'complete-account' : 'forgot-password'}`, { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(token ? {token,password:fields.get('password')} : {email:fields.get('email')}) });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(response.status === 429 ? 'Çok sayıda deneme yapıldı. Bir dakika bekleyip yeniden deneyin.' : data.error || data.title || 'İşlem tamamlanamadı.');
      setNotice(data.message); form.reset();
      if (token) { setToken(''); setDone(true); localStorage.removeItem('ovo_token'); localStorage.removeItem('ovo_user'); }
    } catch (e) { setError(e instanceof TypeError ? 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.' : e instanceof Error ? e.message : 'İşlem tamamlanamadı.'); }
    finally { setBusy(false); }
  }
  return <main className="grid min-h-screen place-items-center bg-[#101112] p-4"><section className="card w-full max-w-md space-y-4 p-6">
    <h1 className="text-xl font-bold">{done ? 'Şifreniz hazır' : token ? 'Şifrenizi belirleyin' : 'Şifremi unuttum / yeni bağlantı'}</h1>
    <p className="text-sm">{token ? 'Davet veya şifre yenileme bağlantınız tek kullanımlıktır. Şifrenizi kimseyle paylaşmayın.' : 'Hesabınızın kayıtlı e-posta adresini kullanın. E-posta hizmeti henüz açılmadıysa veya hesabınız kapalıysa yöneticinizle görüşün.'}</p>
    {!done && <form onSubmit={submit} className="space-y-3"><fieldset disabled={busy} className="space-y-3">
      {token ? <><label className="block text-sm">Yeni şifre<input name="password" type="password" required minLength={10} maxLength={256} autoComplete="new-password" className="input mt-1" /></label><label className="block text-sm">Yeni şifre tekrar<input name="confirmation" type="password" required minLength={10} maxLength={256} autoComplete="new-password" className="input mt-1" /></label><p className="text-xs">10–256 karakter kullanın. Kayıttan sonra önceki oturumlar kapanır.</p></> : <label className="block text-sm">E-posta adresi<input name="email" type="email" required maxLength={320} autoComplete="email" className="input mt-1" /></label>}
      <button className="w-full rounded-lg bg-[#303030] px-4 py-2 text-white disabled:opacity-50">{busy ? 'İşleniyor…' : token ? 'Şifremi kaydet' : 'Bağlantı iste'}</button>
    </fieldset></form>}
    {error && <p role="alert" className="text-sm text-red-700">{error}</p>}{notice && <p role="status" className="text-sm">{notice}</p>}
    {token && <button disabled={busy} className="text-sm underline" onClick={() => { setToken(''); setError(''); }}>Bağlantım çalışmıyor, yenisini iste</button>}
    <Link className="block text-sm underline" href="/login">Giriş ekranına dön</Link>
  </section></main>;
}
