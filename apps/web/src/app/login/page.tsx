'use client';
import Image from 'next/image';
import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { API_URL } from '@/lib/api';

export default function Login() {
  const router = useRouter();
  const qc = useQueryClient();
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  const [challenge, setChallenge] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const form = new FormData(event.currentTarget);
    setPending(true); setError('');
    try {
      const response = await fetch(`${API_URL}/api/auth/${challenge ? 'second-factor' : 'login'}`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(challenge ? { challenge, code: form.get('code') } : { email: String(form.get('email')).trim(), password: form.get('password') }) });
      if (!response.ok) throw new Error(response.status === 429 ? 'Çok sayıda giriş denemesi yapıldı. Bir dakika bekleyip yeniden deneyin.' : challenge ? 'Kod doğrulanamadı veya giriş süresi doldu. Güncel kodu ya da kullanılmamış kurtarma kodunu deneyin; beş hatalı denemede beş dakika bekleyin.' : response.status === 401 ? 'E-posta adresi veya şifre hatalı. Hesabınızın etkin olduğundan emin olun.' : 'Giriş tamamlanamadı. Biraz sonra yeniden deneyin.');
      const data = await response.json();
      if (data.requiresSecondFactor) { setChallenge(data.challenge); return; }
      qc.clear();
      localStorage.setItem('ovo_token', data.token);
      localStorage.setItem('ovo_user', JSON.stringify(data.user));
      router.replace(data.user.role === 'BrandClient' ? '/portal' : '/');
    } catch (cause) { setError(cause instanceof TypeError ? 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin.' : cause instanceof Error ? cause.message : 'Giriş tamamlanamadı.'); }
    finally { setPending(false); }
  }
  return <main className="grid min-h-screen place-items-center bg-[#101112] p-6"><form onSubmit={submit} className="card w-full max-w-[400px] p-7">
    <div className="grid h-14 w-14 place-items-center rounded-2xl bg-[#101112] p-1.5"><Image src="/ovo-logo.svg" alt="OVO" width={48} height={48} priority /></div>
    <h1 className="mt-5 text-2xl font-bold">OVO Growth OS&apos;a giriş yapın</h1><p className="mt-1 text-sm text-[#6d7175]">Çalışan veya marka yetkilisi hesabınızla devam edin.</p>
    {!challenge ? <><label className="mt-6 block text-sm font-semibold">E-posta adresi<input name="email" type="email" className="input mt-1.5" required maxLength={320} autoComplete="username" disabled={pending} /></label>
    <label className="mt-4 block text-sm font-semibold">Şifre<input name="password" type="password" className="input mt-1.5" required maxLength={256} autoComplete="current-password" disabled={pending} /></label></> : <><p className="mt-4 text-sm">Doğrulama uygulamanızdaki 6 haneli kodu veya tek kullanımlık kurtarma kodunuzdan birini yazın. Bu giriş adımı 5 dakika geçerlidir.</p><label className="mt-4 block text-sm font-semibold">Doğrulama veya kurtarma kodu<input name="code" className="input mt-1.5" required maxLength={64} autoComplete="one-time-code" disabled={pending} /></label><button type="button" className="mt-3 underline" disabled={pending} onClick={() => { setChallenge(''); setError(''); }}>E-posta ve şifre adımına dön</button></>}
    {error && <p role="alert" className="mt-3 text-sm text-[#d72c0d]">{error}</p>}
    <button disabled={pending} className="mt-6 w-full rounded-lg bg-[#303030] py-2.5 text-sm font-semibold text-white disabled:opacity-60">{pending ? 'Giriş yapılıyor…' : 'Giriş yap'}</button>
    <Link href="/account-access" className="mt-4 block text-sm underline">Şifremi unuttum / yeni davet bağlantısı</Link>
    <p className="mt-2 text-xs text-[#6d7175]">Hesabınız kapalıysa veya e-posta gelmiyorsa OVO yöneticinizle görüşün.</p>
  </form></main>;
}
