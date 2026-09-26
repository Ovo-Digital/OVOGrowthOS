'use client';
import { useState, type FormEvent } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Card, PageHeader } from '@/components/ui/core';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Settings = { revision: number; source: string; enabled: boolean; host: string; port: number; secure: boolean;
  user: string; fromAddress: string; fromName: string; passwordStored: boolean; configured: boolean; ready: boolean; forceDisabled: boolean; testRecipient: string };
const button = 'rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50';

export default function EmailSettingsPage() {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const query = useQuery({ queryKey: ['mail-settings'], queryFn: () => api<Settings>('/api/account-mail/settings'), enabled: me.data?.role === 'Admin', refetchOnWindowFocus: false });
  const [notice, setNotice] = useState('');
  if (me.isPending) return <p role="status">Hesap yetkisi kontrol ediliyor…</p>;
  if (me.isError) return <p role="alert">Hesap bilgileri alınamadı.</p>;
  if (me.data.role !== 'Admin') return <Card className="p-5">E-posta ayarlarını yalnız yönetici değiştirebilir. <Link href="/settings" className="underline">Ayarlara dön</Link></Card>;
  return <>
    <PageHeader title="E-posta ayarları" description="OVO'nun bildirim ve hesap iletilerini göndereceği Gmail hesabını yönetin." action={<Link href="/settings" className={button}>Ayarlara dön</Link>} />
    {notice && <p role="status" className="mb-4 rounded-lg border bg-white p-4">{notice}</p>}
    {query.isPending ? <p role="status">Ayarlar yükleniyor…</p> : query.isError ? <Card className="p-5"><p role="alert">Ayarlar alınamadı. {query.error.message}</p><button className={button} onClick={() => void query.refetch()}>Yeniden dene</button></Card> :
      <SettingsForm key={query.data.revision} initial={query.data} report={setNotice} reload={async () => { await query.refetch(); }} />}
    <Card className="mt-5 space-y-3 p-5 text-sm">
      <h2 className="font-semibold">Hangi iletiler gönderilir?</h2>
      <p>Görev, yeni paylaşılan rapor ve konuşma bildirimleri için hem bu hizmet hem alıcının Bildirimler bölümündeki ilgili tercihi açık olmalıdır. Rapor e-postalarında ayrıca Müşteri portalı yönetimindeki marka izni açık olmalıdır; bu izin başlangıçta kapalıdır. Marka adres defterine toplu gönderim yapılmaz; rapor bildirimi yalnız o markaya erişimi olan etkin müşteri hesaplarına gider.</p>
      <p>Davet ve şifre yenileme, istenen hesap işlemleridir; görev/rapor tercihinden bağımsızdır. Müşteriye aylık raporu belirli bir günde otomatik gönderme ve PDF eki henüz yoktur. Mevcut rapor bildirimi, ayrıca yayımladığınız rapora giriş gerektiren bir bağlantı verir.</p>
      <p>Kaydetmek tek başına deneme iletisi göndermez. Genel gönderimi açarsanız uygun bekleyen ve yeni iletiler işlenebilir. Kapattığınızda sonraki gönderimler durur; başlamış veya gönderilmiş ileti geri alınamaz. Deneme düğmesi yalnız sizin adresinize açık onayla tek ileti gönderir.</p>
      <Link href="/mail-deliveries" className="underline">Hesap daveti ve şifre gönderimlerini gör</Link>
    </Card>
  </>;
}

function SettingsForm({ initial: s, report, reload }: { initial: Settings; report: (text: string) => void; reload: () => Promise<void> }) {
  const [enabled, setEnabled] = useState(s.enabled); const [host, setHost] = useState(s.host); const [port, setPort] = useState(s.port);
  const [user, setUser] = useState(s.user); const [fromAddress, setFromAddress] = useState(s.fromAddress); const [fromName, setFromName] = useState(s.fromName);
  const [password, setPassword] = useState(''); const [removePassword, setRemovePassword] = useState(false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [confirmTest, setConfirmTest] = useState(false);
  const dirty = enabled !== s.enabled || host !== s.host || port !== s.port || user !== s.user || fromAddress !== s.fromAddress || fromName !== s.fromName || !!password || removePassword;
  useUnsavedChanges(dirty);
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); report('');
    try {
      const result = await api<{ message: string }>('/api/account-mail/settings', { method: 'PUT', body: JSON.stringify({ enabled, host, port, secure: port === 465, user, fromAddress, fromName, password: password || null, removePassword, revision: s.revision }) });
      setPassword(''); report(result.message); await reload();
    } catch (e) { setPassword(''); setError(e instanceof Error ? e.message : 'Ayarlar kaydedilemedi.'); } finally { setBusy(false); }
  }
  async function test() {
    if (busy || dirty || !confirmTest) return;
    setBusy(true); setError(''); report('');
    try { const result = await api<{ message: string }>('/api/account-mail/settings/test', { method: 'POST', body: JSON.stringify({ revision: s.revision, confirm: true }) }); report(result.message); }
    catch (e) { report(e instanceof Error ? e.message : 'Deneme sonucu alınamadı; tekrar göndermeden önce gelen kutunuzu kontrol edin.'); }
    finally { setConfirmTest(false); await reload(); setBusy(false); }
  }
  return <Card className="max-w-4xl space-y-5 p-5">
    <div className="space-y-2 text-sm"><p><strong>Durum:</strong> {s.forceDisabled ? 'Sunucu bütün gönderimleri durdurmuş.' : s.ready ? 'Gönderim açık; teslimi deneme ile doğrulayın.' : s.configured ? 'Bilgiler hazır, genel gönderim kapalı.' : 'Bilgiler eksik veya kayıtlı şifre kullanılamıyor.'}</p>
      <p>{s.source === 'Environment' ? 'Henüz panel kaydı yok; mevcut sunucu ayarları kullanılıyor. İlk kayıttan sonra panel ayarları öncelikli olur. Sunucudaki şifre buraya taşınmaz; uygulama şifresini yeniden girin.' : 'Panelde kaydedilen bilgiler kullanılıyor. Şifre alanını boş bırakmak kayıtlı şifreyi korur.'}</p>
      <p>İlk sürüm Gmail içindir. Başka SMTP sunucuları desteklenmez. 465 SSL/TLS ve 587 STARTTLS seçeneklerinin ikisi de şifreli bağlantı kullanır.</p></div>
    <form onSubmit={save} className="space-y-4">
      <fieldset disabled={busy} className="grid gap-4 sm:grid-cols-2">
        <label className="sm:col-span-2 flex items-center gap-2"><input type="checkbox" checked={enabled} disabled={removePassword} onChange={e => setEnabled(e.target.checked)} />Genel e-posta gönderimi açık</label>
        <label>SMTP sunucusu<input className="input mt-1" required maxLength={253} value={host} onChange={e => setHost(e.target.value)} /></label>
        <label>Port ve bağlantı güvenliği<select className="input mt-1" value={port} onChange={e => setPort(Number(e.target.value))}><option value={465}>465 · SSL/TLS (Gmail önerilen)</option><option value={587}>587 · STARTTLS</option></select></label>
        <label>Gmail kullanıcı adresi<input className="input mt-1" type="email" required={enabled} maxLength={320} autoComplete="off" value={user} onChange={e => setUser(e.target.value)} /></label>
        <label>Gönderici e-posta adresi<input className="input mt-1" type="email" required={enabled} maxLength={320} value={fromAddress} onChange={e => setFromAddress(e.target.value)} /><span className="text-xs">Gmail hesabınız veya o hesapta doğrulanmış gönderici adresi.</span></label>
        <label>Gönderici adı<input className="input mt-1" maxLength={160} value={fromName} onChange={e => setFromName(e.target.value)} /></label>
        <label>Gmail uygulama şifresi<input className="input mt-1" type="password" autoComplete="new-password" maxLength={64} disabled={removePassword} value={password} onChange={e => setPassword(e.target.value)} placeholder={s.passwordStored ? 'Kayıtlı şifre var; değiştirmeyecekseniz boş bırakın' : 'Google uygulama şifresini girin'} /></label>
        <p className="sm:col-span-2 text-sm">Normal Gmail giriş şifrenizi değil, Google hesabınızda oluşturduğunuz 16 karakterli uygulama şifresini kullanın. Google hesabınızda iki adımlı doğrulama gerekir; bazı hesaplarda uygulama şifresi seçeneği bulunmayabilir. <a href="https://support.google.com/accounts/answer/185833?hl=tr" target="_blank" rel="noreferrer" className="underline">Google yardımını aç</a>. Şifre kayıttan sonra gösterilmez ve bu formda tekrar doldurulmaz.</p>
        {s.passwordStored && <label className="sm:col-span-2 flex items-center gap-2"><input type="checkbox" checked={removePassword} onChange={e => { setRemovePassword(e.target.checked); if (e.target.checked) { setEnabled(false); setPassword(''); } }} />Kayıtlı uygulama şifresini kaldır ve genel gönderimi kapat</label>}
        {error && <p role="alert" className="sm:col-span-2 text-red-700">{error}</p>}
        <button className={button} type="submit">{busy ? 'İşlem sürüyor…' : 'E-posta ayarlarını kaydet'}</button>
      </fieldset>
    </form>
    <div className="space-y-3 border-t pt-4 text-sm"><h2 className="font-semibold">Kendi adresime deneme gönder</h2>
      <p>Önce bilgileri genel gönderim kapalıyken kaydedebilirsiniz. Deneme yalnız <strong className="break-all">{s.testRecipient}</strong> adresine gider; hiçbir müşteriye gönderilmez. Bir dakika içinde ikinci deneme yapılamaz.</p>
      <label className="flex items-start gap-2"><input type="checkbox" checked={confirmTest} disabled={busy || dirty} onChange={e => setConfirmTest(e.target.checked)} />Kaydedilmiş ayarlarla kendi adresime bir deneme e-postası gönderilmesini onaylıyorum.</label>
      <button className={button} disabled={busy || dirty || !confirmTest || !s.configured || s.source !== 'Panel' || s.forceDisabled} onClick={() => void test()}>Deneme e-postası gönder</button>
      {dirty && <p>Denemeden önce değişiklikleri kaydedin.</p>}
    </div>
  </Card>;
}
