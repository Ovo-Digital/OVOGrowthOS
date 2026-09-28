'use client';
import Link from 'next/link';
import { useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { periodGroups, periodLabels, periodNumber, type PeriodField, type PeriodInput, type PeriodSnapshot } from '@/lib/period-input';
import { Card } from '@/components/ui/core';
import { useDialog } from '@/components/ui/modal';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Deal = { id: string; brandId: string; name: string; currency: string; status: string; brand: { name: string } };
type Preview = { netRevenue: number; ovoFee: number; brandContributionProfit: number; totalAdSpend: number };
type Suggestion = { period: string; gross: { available: boolean; amount: number; currency: string; orderCount: number; lastSyncAt: string | null; panelGrossSales: number | null }; adSpend: { metaConfigured: boolean; googleConfigured: boolean } };
const button = 'rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50';

export function PerformanceForm({ initial, onSaved, onCancel }: { initial?: PeriodSnapshot; onSaved: (id: string) => void; onCancel?: () => void }) {
  // Keep the loaded version with the form; a background refresh must not bless old edits with a new token.
  const [baseline] = useState(initial);
  const form = useRef<HTMLFormElement>(null); const inFlight = useRef(false);
  const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const [hint, setHint] = useState('');
  const [review, setReview] = useState<{ input: PeriodInput; result: Preview; currency: string; name: string } | null>(null);
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const allowed = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const deals = useQuery({ queryKey: ['deals'], queryFn: () => api<Deal[]>('/api/deals'), enabled: allowed && !baseline });
  useUnsavedChanges(dirty);
  const { confirm } = useDialog();
  const headers = baseline ? { 'If-Match': `"${baseline.updatedAt}"` } : undefined;
  async function preview() {
    if (inFlight.current || !form.current?.reportValidity()) return;
    inFlight.current = true; setBusy(true); setError(''); setReview(null);
    try {
      const f = new FormData(form.current); const deal = deals.data?.find(d => d.id === f.get('dealId'));
      if (!baseline && !deal) throw new Error('Etkin anlaşmayı seçin.');
      const fields = Object.fromEntries(periodGroups.flatMap(g => g.fields).map(key => {
        try { return [key, periodNumber(String(f.get(key) ?? ''), ['orders', 'sessions', 'newCustomers', 'returningCustomers'].includes(key))]; }
        catch (e) { throw new Error(`${periodLabels[key]}: ${e instanceof Error ? e.message : 'Geçerli bir sayı yazın.'}`); }
      })) as Record<PeriodField, number>;
      const input: PeriodInput = { ...fields, brandId: baseline?.brandId ?? deal!.brandId, dealId: baseline?.dealId ?? deal!.id,
        year: baseline?.year ?? Number(f.get('year')), month: baseline?.month ?? Number(f.get('month')) };
      const result = await api<Preview>(baseline ? `/api/performance/${baseline.id}/preview` : '/api/performance/calculate', { method: 'POST', headers, body: JSON.stringify(input) });
      setReview({ input, result, currency: baseline?.deal.currency ?? deal!.currency, name: baseline?.brand.name ?? deal!.brand.name });
    } catch (e) { setError(e instanceof Error ? e.message : 'Ön izleme hazırlanamadı.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  async function suggest() {
    if (inFlight.current || busy || !form.current) return;
    const f = new FormData(form.current);
    let brandId = baseline?.brandId; let year = baseline?.year; let month = baseline?.month;
    if (!baseline) {
      const deal = deals.data?.find(d => d.id === String(f.get('dealId') ?? ''));
      if (!deal) { setError('Ciro önerisi için önce etkin anlaşmayı seçin.'); return; }
      brandId = deal.brandId; year = Number(f.get('year')); month = Number(f.get('month'));
    }
    if (!year || !month || month < 1 || month > 12) { setError('Önce geçerli bir yıl ve ay seçin.'); return; }
    inFlight.current = true; setBusy(true); setError(''); setHint(''); setReview(null);
    try {
      const s = await api<Suggestion>(`/api/brands/${brandId}/performance-suggestions?period=${year}-${String(month).padStart(2, '0')}`);
      if (!s.gross.available) {
        setHint(`Bu dönem için mağazadan henüz sipariş çekilmedi (${s.period}). Öneri için önce marka sayfasındaki Mağaza siparişleri bölümünden siparişleri getirin.`);
      } else {
        const field = form.current.elements.namedItem('grossSales');
        if (field instanceof HTMLInputElement) field.value = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 4 }).format(s.gross.amount);
        setDirty(true);
        setHint(`Öneri yazıldı: ${s.period} · ${s.gross.orderCount} sipariş · ${moneyPrecise(s.gross.amount, s.gross.currency || 'TRY')}`
          + `${s.gross.panelGrossSales !== null ? ` · Paneldaki brüt satış ${moneyPrecise(s.gross.panelGrossSales, s.gross.currency || 'TRY')}` : ''}`
          + ` · Meta reklam ayarı ${s.adSpend.metaConfigured ? 'kayıtlı' : 'kayıtlı değil'}, Google reklam ayarı ${s.adSpend.googleConfigured ? 'kayıtlı' : 'kayıtlı değil'}.`
          + ' Yazılan tutarı kontrol edip “Hesabı kontrol et” ile devam edin.');
      }
    } catch (e) { setError(e instanceof Error ? e.message : 'Ciro önerisi alınamadı.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  async function pullAds() {
    if (inFlight.current || busy || !form.current) return;
    const f = new FormData(form.current);
    let brandId = baseline?.brandId; let year = baseline?.year; let month = baseline?.month;
    if (!baseline) {
      const deal = deals.data?.find(d => d.id === String(f.get('dealId') ?? ''));
      if (!deal) { setError('Reklam harcaması için önce etkin anlaşmayı seçin.'); return; }
      brandId = deal.brandId; year = Number(f.get('year')); month = Number(f.get('month'));
    }
    if (!year || !month || month < 1 || month > 12) { setError('Önce geçerli bir yıl ve ay seçin.'); return; }
    inFlight.current = true; setBusy(true); setError(''); setHint(''); setReview(null);
    try {
      const period = `${year}-${String(month).padStart(2, '0')}`;
      const found: { platform: string; amount: number; currency: string }[] = [];
      for (const platform of ['meta', 'google']) {
        try {
          found.push(await api<{ platform: string; amount: number; currency: string }>(`/api/brands/${brandId}/ad-spend?platform=${platform}&period=${period}`));
        } catch (e) {
          const message = e instanceof Error ? e.message : '';
          if (message.includes('kayıtlı değil')) continue;
          throw new Error(message || 'Reklam harcaması okunamadı.');
        }
      }
      if (!found.length) { setHint('Meta veya Google bağlantıları kayıtlı değil. Harcamayı kaynak raporunuza göre elle girin.'); return; }
      for (const s of found) {
        const field = form.current.elements.namedItem(s.platform === 'meta' ? 'metaSpend' : 'googleSpend');
        if (field instanceof HTMLInputElement) field.value = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 4 }).format(s.amount);
      }
      setDirty(true);
      setHint('Getirildi: ' + found.map(s => `${s.platform === 'meta' ? 'Meta' : 'Google'} ${moneyPrecise(s.amount, s.currency)}`).join(' · ')
        + '. Yazılan tutarları kontrol edip “Hesabı kontrol et” ile devam edin; kayıt kendiliğinden yapılmaz.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Reklam harcaması getirilemedi.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  async function save() {
    if (inFlight.current || !review) return;
    inFlight.current = true; setBusy(true); setError('');
    try {
      const saved = await api<{ id: string }>(baseline ? `/api/performance/${baseline.id}` : '/api/performance', {
        method: baseline ? 'PUT' : 'POST', headers, body: JSON.stringify(review.input),
      });
      setDirty(false); onSaved(saved.id);
    } catch (e) { setError(e instanceof Error ? e.message : 'Kayıt tamamlanamadı.'); setReview(null); }
    finally { inFlight.current = false; setBusy(false); }
  }
  if (me.isPending) return <p role="status">Hesap yetkisi kontrol ediliyor…</p>;
  if (me.isError) return <p role="alert">{me.error.message}</p>;
  if (!allowed) return <Card className="p-5">Aylık sonuçları yalnız yönetici ve iş ortağı düzenleyebilir.</Card>;
  return <form ref={form} className="space-y-4" onSubmit={e => { e.preventDefault(); void preview(); }} onChange={() => { setDirty(true); setReview(null); setHint(''); }}>
    <Card className="p-5"><h2 className="font-semibold">{baseline ? 'Taslak dönemi düzenle' : 'Yeni aylık sonuç'}</h2>
      <p className="my-3 text-sm">Tutarları <strong>1.234,56</strong> biçiminde yazın; en fazla dört ondalık basamak kullanın. Hareket yoksa 0 yazın; bilinmeyen değeri sıfır saymayın. Kaydetmek dönemi onaylamaz.</p>
      <Link href="/guide#13-aylik-donem-kapatma-sureci" className="text-sm underline">Bu işlem nasıl yapılır?</Link>
      {baseline ? <p className="mt-3 font-medium">{baseline.brand.name} · {baseline.month}/{baseline.year} · {baseline.deal.currency}<span className="block text-sm font-normal">Marka, anlaşma ve ay değişmez. Mevcut hakediş düzeltmeleri korunur.</span></p> : <>
        {deals.isPending && <p role="status">Anlaşmalar yükleniyor…</p>}{deals.isError && <p role="alert">{deals.error.message}</p>}
        {deals.data && !deals.data.some(d => d.status === 'Active') && <p>Etkin anlaşma yok. Önce ilgili markanın anlaşmasını tamamlayın.</p>}
        <div className="mt-4 grid gap-3 sm:grid-cols-3"><label>Etkin anlaşma<select name="dealId" required className="input mt-1" defaultValue="" disabled={busy}><option value="">Anlaşma seçin</option>{deals.data?.filter(d => d.status === 'Active').map(d => <option key={d.id} value={d.id}>{d.brand.name} · {d.name} · {d.currency}</option>)}</select></label>
          <label>Yıl<input name="year" type="number" min={2020} max={2100} required className="input mt-1" defaultValue={new Date().getFullYear()} disabled={busy}/></label>
          <label>Ay<input name="month" type="number" min={1} max={12} required className="input mt-1" defaultValue={new Date().getMonth() + 1} disabled={busy}/></label></div></>}
    <div className="mt-3 border-t pt-3">
      <div className="flex flex-wrap gap-2">
        <button type="button" className={button} disabled={busy} onClick={() => void suggest()}>Mağaza verisinden brüt satış öner</button>
        <button type="button" className={button} disabled={busy} onClick={() => void pullAds()}>Reklam harcamasını getir</button>
      </div>
      <p className="mt-1 text-xs text-[#6d7175]">Mağazadan çekilmiş siparişlerden bu dönemin brüt satış tutarını alan yazar. “Reklam harcamasını getir” ise Meta/Google bağlantıları kayıtlıysa bu dönemin harcamasını ilgili alanlara yazar. Her ikisi de otomatik kaydetmez, yazılanı siz kontrol edersiniz.</p>
      {hint && <p role="status" className="mt-2 text-sm">{hint}</p>}
    </div>
    </Card>
    <fieldset disabled={busy} className="space-y-4">{periodGroups.map(g => <Card className="p-5" key={g.title}><h3 className="mb-4 font-semibold">{g.title}</h3><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{g.fields.map(key => <label key={key} className="text-sm">{periodLabels[key]}<input name={key} required inputMode="decimal" autoComplete="off" className="input mt-1" defaultValue={baseline ? String(baseline[key]).replace('.', ',') : ''}/></label>)}</div></Card>)}</fieldset>
    {error && <p role="alert" className="text-red-700">{error}</p>}
    <div className="flex flex-wrap gap-3"><button type="submit" disabled={busy} className={button}>{busy ? 'İşlem yapılıyor…' : 'Hesabı kontrol et'}</button>{onCancel && <button type="button" disabled={busy} className={button} onClick={async () => { if (dirty && !(await confirm({ title: 'Düzenlemeden çıksın mı?', message: 'Değişiklikler kaydedilmedi. Düzenlemeden çıkmak istiyor musunuz?' }))) return; onCancel(); }}>Düzenlemeden çık</button>}</div>
    {review && <Card className="space-y-3 border-green-700 p-5"><h3 className="font-semibold">Kaydetmeden önce son kontrol</h3><p>{review.name} · {review.input.month}/{review.input.year} · {review.currency}</p>
      <dl className="space-y-2">{[['Net ciro', review.result.netRevenue], ['Reklam gideri', review.result.totalAdSpend], ['OVO hakedişi', review.result.ovoFee], ['Markaya kalan katkı', review.result.brandContributionProfit]].map(([label, value]) => <div key={label} className="flex flex-wrap justify-between gap-2"><dt>{label}</dt><dd>{moneyPrecise(Number(value), review.currency)}</dd></div>)}</dl>
      <p className="text-sm">Bilgileri kaynak raporla kontrol ettiyseniz aşağıdaki düğmeyle taslağı kaydedin. Sonraki adım incelemeye göndermektir; onay ve kilit ayrıca yapılır.</p>
      <button type="button" className={button} disabled={busy} onClick={() => void save()}>Kontrol ettim, taslağı kaydet</button></Card>}
  </form>;
}
