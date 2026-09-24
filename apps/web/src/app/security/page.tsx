'use client';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Setup = { secret: string; recoveryCodes: string[]; expiresAt: string };
type Result = { token: string; user: SessionUser; recoveryCodes: string[] | null };

export default function Security() {
  const qc = useQueryClient();
  const [setup, setSetup] = useState<Setup | null>(null);
  const [codes, setCodes] = useState<string[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [saved, setSaved] = useState(false);
  const [disableConfirmed, setDisableConfirmed] = useState(false);
  useUnsavedChanges(setup !== null || codes !== null);
  const state = useQuery({ queryKey: ['account-security'], queryFn: () => api<{ enabled: boolean; recoveryCodesRemaining: number; canEnable: boolean }>('/api/auth/security') });
  async function act(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (busy) return;
    const element = e.currentTarget;
    const form = new FormData(element);
    const action = String(form.get('action'));
    setBusy(true); setMessage('');
    try {
      const body = JSON.stringify({ password: form.get('password'), code: form.get('code') ?? '', recoverySaved: saved });
      if (action === 'setup') { const data = await api<Setup>('/api/auth/security/setup', { method: 'POST', body }); setSetup(data); setCodes(null); setSaved(false); }
      else {
        const data = await api<Result>(`/api/auth/security/${action}`, { method: 'POST', body });
        localStorage.setItem('ovo_token', data.token); localStorage.setItem('ovo_user', JSON.stringify(data.user));
        setSetup(null); setCodes(data.recoveryCodes); setSaved(false); setDisableConfirmed(false);
        setMessage(action === 'enable' ? 'İki aşamalı giriş açıldı. Diğer oturumlarınız kapatıldı.' : action === 'disable' ? 'İki aşamalı giriş kapatıldı. Diğer oturumlarınız kapatıldı.' : 'Yeni kurtarma kodları hazır. Eski kodlar artık çalışmaz.');
        await qc.invalidateQueries({ queryKey: ['account-security'] }); await qc.invalidateQueries({ queryKey: ['session-user'] });
      }
      element.reset();
    } catch (e) { setMessage(e instanceof Error ? e.message : 'İşlem tamamlanamadı.'); }
    finally { setBusy(false); }
  }
  if (state.isPending) return <p role="status">Güvenlik bilgisi yükleniyor…</p>;
  if (state.isError) return <div role="alert">Bilgiler alınamadı. <button onClick={() => void state.refetch()} className="underline">Yeniden dene</button></div>;
  const password = <label className="block text-sm font-semibold">Mevcut şifreniz<input name="password" type="password" autoComplete="current-password" maxLength={256} required disabled={busy} className="input mt-1" /></label>;
  const proof = <label className="block text-sm font-semibold">Doğrulama veya kurtarma kodu<input name="code" autoComplete="one-time-code" maxLength={64} required disabled={busy} className="input mt-1" /></label>;
  return <div className="max-w-2xl space-y-5"><h1 className="text-2xl font-bold">Hesap güvenliği</h1>
    <p>İki aşamalı giriş {state.data.enabled ? 'açık' : 'kapalı'}. Telefonunuzdaki doğrulama uygulaması şifrenize ek bir kod üretir. Bu özellik Gmail kodu veya SMS kullanmaz; isteğe bağlıdır.</p>
    <p className="text-sm">Telefonunuzu kaybederseniz şifrenizle birlikte bir kurtarma kodunu kullanın. Şifre yenileme ikinci aşamayı kaldırmaz. Tek yöneticiyseniz kurtarma kodlarını telefondan ayrı, güvenli bir yerde saklamadan bu özelliği açmayın.</p>
    {message && <p role="status" className="card p-4">{message}</p>}
    {(setup || codes) && <section className="card space-y-3 p-5"><h2 className="font-semibold">Kurtarma kodlarınızı şimdi saklayın</h2><p className="text-sm">Her kod yalnız bir kez kullanılabilir. Bu listeyi tekrar göremezsiniz. Şifrenizle aynı yerde veya sohbet içinde paylaşmayın.</p><pre className="overflow-x-auto rounded bg-[#f6f6f7] p-3 text-xs">{(setup?.recoveryCodes ?? codes)?.join('\n')}</pre>{codes && <button className="btn-secondary" onClick={() => setCodes(null)}>Güvenle sakladım, kodları ekrandan kaldır</button>}</section>}
    {!state.data.enabled && !setup && state.data.canEnable && <form className="card space-y-4 p-5" onSubmit={act}><input name="action" type="hidden" value="setup" />{password}<button disabled={busy} className="btn-primary">İki aşamalı giriş kurulumunu başlat</button></form>}
    {!state.data.enabled && !state.data.canEnable && <p>Yeni kurulum yalnız yönetici hesaplarında açılır.</p>}
    {setup && <form className="card space-y-4 p-5" onSubmit={act}><input name="action" type="hidden" value="enable" /><h2 className="font-semibold">Doğrulama uygulamasına hesap ekleyin</h2><p className="text-sm">Uygulamanızda “Kurulum anahtarını gir” seçeneğini açın. Hesap adı olarak OVO Growth OS, tür olarak zamana dayalı kod seçin. Kurulum 10 dakika geçerlidir.</p><code className="block break-all rounded bg-[#f6f6f7] p-3">{setup.secret}</code>{password}<label className="block text-sm font-semibold">Uygulamadaki 6 haneli kod<input name="code" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required disabled={busy} className="input mt-1" /></label><label className="flex items-start gap-2 text-sm"><input type="checkbox" checked={saved} onChange={e => setSaved(e.target.checked)} required disabled={busy} />Kurtarma kodlarını telefondan ayrı güvenli bir yere kaydettim.</label><button disabled={busy || !saved} className="btn-primary">Doğrula ve iki aşamalı girişi aç</button><button type="button" disabled={busy} className="ml-3 underline" onClick={() => setSetup(null)}>Vazgeç / kurulumu yeniden başlat</button></form>}
    {state.data.enabled && <><p>Kalan kurtarma kodu: {state.data.recoveryCodesRemaining}. Aynı telefon kodu ikinci kez kullanılamaz; yeni kod çıkmasını bekleyin.</p><form className="card space-y-4 p-5" onSubmit={act}><input name="action" type="hidden" value="recovery-codes" /><h2 className="font-semibold">Kurtarma kodlarını yenile</h2><p className="text-sm">Yeni liste oluşunca eski kodların tamamı geçersiz olur.</p>{password}{proof}<button disabled={busy} className="btn-primary">Eski kodları iptal et ve yenilerini oluştur</button></form><form className="card space-y-4 p-5" onSubmit={act}><input name="action" type="hidden" value="disable" /><h2 className="font-semibold">İki aşamalı girişi kapat</h2>{password}{proof}<label className="flex items-start gap-2 text-sm"><input type="checkbox" checked={disableConfirmed} onChange={e => setDisableConfirmed(e.target.checked)} required disabled={busy} />Ek korumanın kalkacağını biliyorum; kapatmak istiyorum.</label><button disabled={busy || !disableConfirmed} className="btn-secondary">İki aşamalı girişi kapat</button></form></>}
  </div>;
}
