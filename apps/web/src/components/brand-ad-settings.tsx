'use client';
import { useEffect, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { Card, ErrorState, LoadingState } from '@/components/ui/core';
import { useDialog } from '@/components/ui/modal';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';
import { turkceTarih } from '@/lib/turkish';

type AdSettings = { revision: number; platform: string; accountId: string; clientId: string; secretStored: boolean; clientSecretStored: boolean; developerTokenStored: boolean; configured: boolean; updatedAt: string | null; lastTestAt: string | null };
type AdSpend = { platform: string; period: string; amount: number; currency: string; source: string };
const button = 'rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50';

export function BrandAdSettingsCard({ brandId }: { brandId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const [notice, setNotice] = useState('');
  if (me.isPending) return null;
  if (me.isError || me.data.role !== 'Admin') return null;
  return <Card className="mt-5 p-5">
    <h2 className="font-semibold">Reklam platformu ayarları</h2>
    <p className="mt-2 text-sm text-[#6d7175]">Meta ve Google reklam hesaplarının okunması için gerekli bağlantı bilgileri. Jetonlar sunucuda şifreli saklanır ve kayıttan sonra burada gösterilmez. Bu ayarlar yalnız reklam harcamasının okunması ve raporlarda kullanılması içindir; hiçbir bütçe veya kampanya değişikliği yapılmaz.</p>
    {notice && <p role="status" className="mt-3 rounded-lg border bg-white p-3 text-sm">{notice}</p>}
    <AdSettingsForm brandId={brandId} report={setNotice} />
    <div className="mt-4 space-y-2 border-t pt-4 text-xs text-[#6d7175]">
      <p>Kaydetmek bağlantı doğrulamaz. Doğrulama düğmesi reklam platformundan hesabı okur ve sonucu size bildirir; bir dakika içinde ikinci kez denenemez.</p>
      <p>Meta için reklam hesabı numarası ve erişim jetonu; Google için reklam hesabı numarası, müşteri numarası, yenileme jetonu, istemci sırrı ve geliştirici jetonu gerekir.</p>
    </div>
  </Card>;
}

function AdSettingsForm({ brandId, report }: { brandId: string; report: (text: string) => void }) {
  const { confirm } = useDialog();
  const qc = useQueryClient();
  const [platform, setPlatform] = useState('Meta');
  const [dirty, setDirty] = useState(false);
  const query = useQuery({ queryKey: ['brand-ad-settings', brandId, platform], queryFn: () => api<AdSettings>(`/api/brands/${brandId}/ad-settings?platform=${platform}`), refetchOnWindowFocus: false });
  async function switchPlatform(next: string) {
    if (next === platform) return;
    if (dirty && !(await confirm({ title: 'Yazılan bilgiler gitsin mi?', message: 'Kaydedilmemiş jeton veya numara girişi var. Platformu değiştirdiğinizde bu bilgiler silinir.' }))) return;
    setDirty(false); report(''); setPlatform(next);
  }
  async function reload() { await query.refetch(); await qc.invalidateQueries({ queryKey: ['brand-ad-settings', brandId] }); }
  return <div className="mt-3">
    <div className="flex flex-wrap gap-2" role="tablist" aria-label="Reklam platformu">
      {['Meta', 'Google'].map(p => <button key={p} type="button" className={`${button} ${platform === p ? 'border-[#303030] bg-[#f7f7f8] font-semibold' : ''}`} aria-pressed={platform === p} onClick={() => void switchPlatform(p)}>{p === 'Meta' ? 'Meta reklamları' : 'Google Ads'}</button>)}
    </div>
    {query.isPending ? <LoadingState label="Reklam ayarları yükleniyor…" /> : query.isError
      ? <ErrorState message={`Reklam ayarları alınamadı. ${query.error.message}`} />
      : <AdSettingsFields key={`${platform}:${query.data.revision}`} brandId={brandId} platform={platform} initial={query.data} report={report} reload={reload} onDirty={setDirty} />}
    <AdSpendReader brandId={brandId} platform={platform} />
  </div>;
}

function AdSettingsFields({ brandId, platform, initial: s, report, reload, onDirty }: { brandId: string; platform: string; initial: AdSettings; report: (text: string) => void; reload: () => Promise<void>; onDirty: (dirty: boolean) => void }) {
  const [accountId, setAccountId] = useState(s.accountId);
  const [clientId, setClientId] = useState(s.clientId);
  const [secret, setSecret] = useState('');
  const [clientSecret, setClientSecret] = useState('');
  const [developerToken, setDeveloperToken] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [confirmTest, setConfirmTest] = useState(false);
  const google = platform === 'Google';
  const dirty = accountId !== s.accountId || clientId !== s.clientId || !!secret || !!clientSecret || !!developerToken;
  useUnsavedChanges(dirty);
  useEffect(() => { onDirty(dirty); }, [dirty, onDirty]);
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); report('');
    try {
      const result = await api<{ message: string }>(`/api/brands/${brandId}/ad-settings`, { method: 'PUT', body: JSON.stringify({ revision: s.revision, platform, accountId, clientId, secret: secret || null, clientSecret: clientSecret || null, developerToken: developerToken || null }) });
      setSecret(''); setClientSecret(''); setDeveloperToken(''); report(result.message); await reload();
    } catch (e) { setSecret(''); setClientSecret(''); setDeveloperToken(''); setError(e instanceof Error ? e.message : 'Reklam ayarları kaydedilemedi.'); } finally { setBusy(false); }
  }
  async function test() {
    if (busy || dirty || !confirmTest) return;
    setBusy(true); setError(''); report('');
    try { const result = await api<{ accepted: boolean; message: string }>(`/api/brands/${brandId}/ad-settings/test`, { method: 'POST', body: JSON.stringify({ revision: s.revision, platform }) }); report(result.message); await reload(); }
    catch (e) { report(e instanceof Error ? e.message : 'Bağlantı doğrulanamadı; kayıtlı bilgileri kontrol edip tekrar deneyin.'); }
    finally { setConfirmTest(false); setBusy(false); }
  }
  return <>
    <p className="mt-3 text-sm"><strong>Durum:</strong> {s.configured ? 'Bilgiler kayıtlı ve eksiksiz; bağlantı doğrulamasını deneyebilirsiniz.' : 'Bilgiler eksik veya kayıtlı jetonlar kullanılamıyor.'}
      {s.lastTestAt ? ` Son doğrulama: ${turkceTarih(s.lastTestAt)}.` : ' Henüz bağlantı doğrulanmadı.'}</p>
    <form onSubmit={save} className="mt-3 space-y-4">
      <fieldset disabled={busy} className="grid gap-4 sm:grid-cols-2">
        <label>Reklam hesabı numarası<input className="input mt-1" required maxLength={64} autoComplete="off" value={accountId} onChange={e => setAccountId(e.target.value)} placeholder={google ? 'ör. 123-456-7890' : 'ör. 1234567890'} /></label>
        {google && <label>Müşteri numarası<input className="input mt-1" required maxLength={64} autoComplete="off" value={clientId} onChange={e => setClientId(e.target.value)} placeholder="ör. 1234567890" /></label>}
        <label className="sm:col-span-2">{google ? 'Yenileme jetonu' : 'Erişim jetonu'}<input className="input mt-1" type="password" autoComplete="new-password" maxLength={400} value={secret} onChange={e => setSecret(e.target.value)} placeholder={s.secretStored ? 'Kayıtlı jeton var; boş bırakırsanız korunur' : 'Jetonu girin'} /></label>
        {google && <label>İstemci sırrı<input className="input mt-1" type="password" autoComplete="new-password" maxLength={400} value={clientSecret} onChange={e => setClientSecret(e.target.value)} placeholder={s.clientSecretStored ? 'Kayıtlı sırrı var; boş bırakırsanız korunur' : 'İstemci sırrını girin'} /></label>}
        {google && <label>Geliştirici jetonu<input className="input mt-1" type="password" autoComplete="new-password" maxLength={400} value={developerToken} onChange={e => setDeveloperToken(e.target.value)} placeholder={s.developerTokenStored ? 'Kayıtlı jeton var; boş bırakırsanız korunur' : 'Geliştirici jetonunu girin'} /></label>}
        {error && <p role="alert" className="sm:col-span-2 text-sm text-red-700">{error}</p>}
        <button className={button} type="submit">{busy ? 'İşlem sürüyor…' : 'Reklam ayarlarını kaydet'}</button>
      </fieldset>
    </form>
    <div className="mt-4 space-y-3 border-t pt-4 text-sm">
      <h3 className="font-semibold">Bağlantıyı doğrula</h3>
      <p>Doğrulama, kayıtlı bilgilerle reklam hesabından güncel bilgiyi okur; jetonlar ekrana veya kayıtlara yazılmaz.</p>
      <label className="flex items-start gap-2"><input type="checkbox" checked={confirmTest} disabled={busy || dirty} onChange={e => setConfirmTest(e.target.checked)} />Kaydedilmiş ayarlarla reklam hesabı bağlantısının doğrulanmasını onaylıyorum.</label>
      <button className={button} disabled={busy || dirty || !confirmTest || !s.configured} onClick={() => void test()}>Bağlantıyı doğrula</button>
      {dirty && <p>Doğrulamadan önce değişiklikleri kaydedin.</p>}
    </div>
  </>;
}

function AdSpendReader({ brandId, platform }: { brandId: string; platform: string }) {
  const now = new Date();
  const [period, setPeriod] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<AdSpend | null>(null);
  const [error, setError] = useState('');
  async function read() {
    if (busy) return;
    setBusy(true); setError(''); setResult(null);
    try { setResult(await api<AdSpend>(`/api/brands/${brandId}/ad-spend?platform=${platform}&period=${period}`)); }
    catch (e) { setError(e instanceof Error ? e.message : 'Reklam harcaması okunamadı.'); }
    finally { setBusy(false); }
  }
  return <div className="mt-4 space-y-3 border-t pt-4 text-sm">
    <h3 className="font-semibold">Reklam harcamasını oku</h3>
    <p>Kayıtlı bağlantı bilgileriyle seçili ayın reklam harcamasını reklam platformundan okur. Okunan tutar yalnız bilgi amaçlıdır; aylık sonuç kaydına otomatik yazılmaz.</p>
    <div className="flex flex-wrap items-end gap-3">
      <label>Ay<input className="input ml-2 mt-1" type="month" value={period} onChange={e => setPeriod(e.target.value)} /></label>
      <button className={button} disabled={busy || period.length !== 7} onClick={() => void read()}>{busy ? 'Okunuyor…' : 'Harcamayı getir'}</button>
    </div>
    {error && <p role="alert" className="text-red-700">{error}</p>}
    {result && <p role="status">{result.period} dönemi {platform === 'Meta' ? 'Meta' : 'Google Ads'} harcaması: <strong>{moneyPrecise(result.amount, result.currency)}</strong> · Kaynak: {result.source}</p>}
  </div>;
}
