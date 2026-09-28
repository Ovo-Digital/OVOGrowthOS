'use client';
import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Card, ErrorState, LoadingState } from '@/components/ui/core';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';
import { turkceTarih } from '@/lib/turkish';

type ApiSettings = { revision: number; platform: string; storeUrl: string; apiUser: string; passwordStored: boolean; configured: boolean; updatedAt: string | null; lastTestAt: string | null };
const button = 'rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50';

export function BrandApiSettingsCard({ brandId }: { brandId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const query = useQuery({ queryKey: ['brand-api-settings', brandId], queryFn: () => api<ApiSettings>(`/api/brands/${brandId}/api-settings`), enabled: me.data?.role === 'Admin', refetchOnWindowFocus: false });
  const [notice, setNotice] = useState('');
  if (me.isPending) return <Card className="mt-5 p-5"><LoadingState label="Hesap yetkisi kontrol ediliyor…" /></Card>;
  if (me.isError) return null;
  if (me.data.role !== 'Admin') return <Card className="mt-5 p-5 text-sm text-[#6d7175]">Mağaza API ayarlarını yalnız yönetici görebilir.</Card>;
  return <Card className="mt-5 p-5">
    <h2 className="font-semibold">Mağaza API ayarları</h2>
    <p className="mt-2 text-sm text-[#6d7175]">Markanın mağazasından sipariş listesini güvenle okumak için gerekli bağlantı bilgileri. Şifre sunucuda şifreli saklanır ve kayıttan sonra burada gösterilmez.</p>
    {notice && <p role="status" className="mt-3 rounded-lg border bg-white p-3 text-sm">{notice}</p>}
    {query.isPending ? <LoadingState label="Ayarlar yükleniyor…" /> : query.isError ? <ErrorState message={`Ayarlar alınamadı. ${query.error.message}`} /> :
      <SettingsForm key={query.data.revision} brandId={brandId} initial={query.data} report={setNotice} reload={async () => { await query.refetch(); }} />}
    <div className="mt-4 space-y-2 border-t pt-4 text-xs text-[#6d7175]">
      <p>Kaydetmek bağlantıyı doğrulamaz. Doğrulama düğmesi mağazadan geçici bir erişim jetonu ister ve sonucu size bildirir; bir dakika içinde ikinci kez denenemez.</p>
      <p>Sipariş aktarımı şimdilik yalnız hazırlık ve kontrol içindir; hiçbir sipariş veya tutar otomatik değiştirilmez, hakediş ve dönem kapanışına etkisi yoktur.</p>
    </div>
  </Card>;
}

function SettingsForm({ brandId, initial: s, report, reload }: { brandId: string; initial: ApiSettings; report: (text: string) => void; reload: () => Promise<void> }) {
  const [platform, setPlatform] = useState(s.platform);
  const [storeUrl, setStoreUrl] = useState(s.storeUrl);
  const [apiUser, setApiUser] = useState(s.apiUser);
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [confirmTest, setConfirmTest] = useState(false);
  const shopify = platform === 'Shopify';
  const dirty = storeUrl !== s.storeUrl || apiUser !== s.apiUser || !!password || platform !== s.platform;
  useUnsavedChanges(dirty);
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); report('');
    try {
      const result = await api<{ message: string }>(`/api/brands/${brandId}/api-settings`, { method: 'PUT', body: JSON.stringify({ revision: s.revision, platform, storeUrl, apiUser: shopify ? '' : apiUser, password: password || null }) });
      setPassword(''); report(result.message); await reload();
    } catch (e) { setPassword(''); setError(e instanceof Error ? e.message : 'API ayarları kaydedilemedi.'); } finally { setBusy(false); }
  }
  async function test() {
    if (busy || dirty || !confirmTest) return;
    setBusy(true); setError(''); report('');
    try { const result = await api<{ message: string }>(`/api/brands/${brandId}/api-settings/test`, { method: 'POST', body: JSON.stringify({ revision: s.revision }) }); report(result.message); }
    catch (e) { report(e instanceof Error ? e.message : 'Bağlantı doğrulanamadı; daha önce kaydettiğiniz bilgileri kontrol edip tekrar deneyin.'); }
    finally { setConfirmTest(false); await reload(); setBusy(false); }
  }
  return <>
    <p className="mt-3 text-sm"><strong>Durum:</strong> {s.configured ? 'Bilgiler kayıtlı ve eksiksiz; bağlantı doğrulamasını deneyebilirsiniz.' : 'Bilgiler eksik veya kayıtlı şifre kullanılamıyor.'}
      {s.lastTestAt ? ` Son doğrulama: ${turkceTarih(s.lastTestAt)}.` : ' Henüz bağlantı doğrulanmadı.'}</p>
    <form onSubmit={save} className="mt-3 space-y-4">
      <fieldset disabled={busy} className="grid gap-4 sm:grid-cols-2">
        <label className="sm:col-span-2">Mağaza platformu<select className="input mt-1" value={platform} onChange={e => setPlatform(e.target.value)}><option value="GrandNode">GrandNode</option><option value="Shopify">Shopify</option></select></label>
        <label>Mağaza adresi<input className="input mt-1" type="url" required maxLength={300} placeholder={shopify ? 'https://ornek.myshopify.com' : 'https://admin.ornek.com'} value={storeUrl} onChange={e => setStoreUrl(e.target.value)} /></label>
        {!shopify && <label>API kullanıcısı e-postası<input className="input mt-1" type="email" required maxLength={320} autoComplete="off" value={apiUser} onChange={e => setApiUser(e.target.value)} /></label>}
        <label className="sm:col-span-2">{shopify ? 'Admin API jetonu' : 'API kullanıcısı şifresi'}<input className="input mt-1" type="password" autoComplete="new-password" maxLength={400} value={password} onChange={e => setPassword(e.target.value)} placeholder={s.passwordStored ? (shopify ? 'Kayıtlı jeton var; boş bırakırsanız korunur' : 'Kayıtlı şifre var; değiştirmeyecekseniz boş bırakın') : (shopify ? 'Admin API jetonunu girin' : 'API kullanıcısı şifresini girin')} /></label>
        {error && <p role="alert" className="sm:col-span-2 text-sm text-red-700">{error}</p>}
        <button className={button} type="submit">{busy ? 'İşlem sürüyor…' : 'API ayarlarını kaydet'}</button>
      </fieldset>
      <p className="text-xs text-[#6d7175]">{shopify
        ? 'Shopify mağazalarında API kullanıcısı e-postası gerekmez. Yönetimden Ayarlar → Uygulamalar ve satış kanalları bölümünden sipariş okuma yetkili bir özel uygulama jetonu alınır. Mağaza adresi https ile başlamalı ve .myshopify.com ile bitmelidir.'
        : 'GrandNode mağazalarında panelin Ayarlar → API Kullanıcılar bölümünde bu e-postayla etkin bir API kullanıcısı bulunmalıdır. Mağaza adresi https ile başlamalıdır.'}</p>
    </form>
    <div className="mt-4 space-y-3 border-t pt-4 text-sm">
      <h3 className="font-semibold">Bağlantıyı doğrula</h3>
      <p>Doğrulama, kayıtlı bilgilerle mağazadan geçici bir erişim jetonu ister; şifreniz ekrana veya kayıtlara yazılmaz.</p>
      <label className="flex items-start gap-2"><input type="checkbox" checked={confirmTest} disabled={busy || dirty} onChange={e => setConfirmTest(e.target.checked)} />Kaydedilmiş ayarlarla mağaza bağlantısının doğrulanmasını onaylıyorum.</label>
      <button className={button} disabled={busy || dirty || !confirmTest || !s.configured} onClick={() => void test()}>Bağlantıyı doğrula</button>
      {dirty && <p>Doğrulamadan önce değişiklikleri kaydedin.</p>}
    </div>
  </>;
}
