'use client';
import Link from 'next/link';
import { useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise, percent, type SessionUser } from '@/lib/api';
import { channelSalesFields, periodGroups, periodLabels, periodNumber, type ChannelInput, type ChannelLine, type ChannelSalesField, type PeriodField, type PeriodInput, type PeriodSnapshot } from '@/lib/period-input';
import { Card } from '@/components/ui/core';
import { useDialog } from '@/components/ui/modal';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';
import type { SalesChannel } from '@/components/sales-channels';

type Deal = { id: string; brandId: string; name: string; currency: string; status: string; brand: { name: string } };
type Preview = { netRevenue: number; ovoFee: number; brandContributionProfit: number; totalAdSpend: number; commissionBreakdownJson: string;
  channels: { salesChannelId: string; commissionableRevenue: number; ovoFeeShare: number }[] };
type BreakdownChannel = { salesChannelId: string; channelName: string; commissionableRevenue: number; rate: number; fee: number };
type Suggestion = { period: string; gross: { available: boolean; amount: number; currency: string; orderCount: number; lastSyncAt: string | null; panelGrossSales: number | null }; adSpend: { metaConfigured: boolean; googleConfigured: boolean } };
type Submission = { id: string; year: number; month: number; grossSales: number | null; refunds: number | null; metaSpend: number | null; googleSpend: number | null; note: string; revision: number; submittedAt: string; submittedBy: string };
const button = 'rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50';
const salesFields = [...channelSalesFields];
const trNumber = (value: number) => String(value).replace('.', ',');

export function PerformanceForm({ initial, onSaved, onCancel }: { initial?: PeriodSnapshot; onSaved: (id: string) => void; onCancel?: () => void }) {
  // Keep the loaded version with the form; a background refresh must not bless old edits with a new token.
  const [baseline] = useState(initial);
  const form = useRef<HTMLFormElement>(null); const inFlight = useRef(false); const keyRef = useRef(baseline?.channels?.length ?? 1);
  const [modeTouched, setModeTouched] = useState(false);
  const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const [hint, setHint] = useState('');
  const [mode, setMode] = useState<'single' | 'channels'>(baseline?.channels?.length ? 'channels' : 'single');
  const [rows, setRows] = useState<{ key: number; initial?: ChannelLine }[]>(
    baseline?.channels?.length ? baseline.channels.map((c, i) => ({ key: i, initial: c })) : [{ key: 0 }]);
  const [dealId, setDealId] = useState('');
  const [review, setReview] = useState<{ input: PeriodInput; result: Preview; currency: string; name: string } | null>(null);
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const allowed = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const deals = useQuery({ queryKey: ['deals'], queryFn: () => api<Deal[]>('/api/deals'), enabled: allowed && !baseline });
  const brandId = baseline?.brandId ?? deals.data?.find(d => d.id === dealId)?.brandId ?? '';
  const brandChannels = useQuery({ queryKey: ['sales-channels', brandId], queryFn: () => api<SalesChannel[]>(`/api/brands/${brandId}/sales-channels`), enabled: allowed && brandId !== '' });
  const activeChannels = (brandChannels.data ?? []).filter(c => c.isActive);
  const channelName = (id: string) => activeChannels.find(c => c.id === id)?.name ?? baseline?.channels.find(c => c.salesChannelId === id)?.salesChannel?.name ?? 'Seçili kanal';
  // Yeni kayıtta kanal varsa varsayılan görünüm kanallıdır; kullanıcı seçimi her zaman geçerlidir.
  const showMode = !baseline && !modeTouched && mode === 'single' && activeChannels.length > 0 ? 'channels' as const : mode;
  useUnsavedChanges(dirty);
  const { confirm } = useDialog();
  const headers = baseline ? { 'If-Match': `"${baseline.updatedAt}"` } : undefined;
  const channelEntry = showMode === 'channels' && (activeChannels.length > 0 || (baseline?.channels?.length ?? 0) > 0);
  function parseHeader(f: FormData, skipSales: boolean) {
    return Object.fromEntries(periodGroups.flatMap(g => g.fields).map(key => {
      // Kanal modunda satış alanları ekranda yoktur; toplamları sunucu satırlardan türetir.
      if (skipSales && (salesFields as readonly string[]).includes(key)) return [key, 0];
      try { return [key, periodNumber(String(f.get(key) ?? ''), ['orders', 'sessions', 'newCustomers', 'returningCustomers'].includes(key))]; }
      catch (e) { throw new Error(`${periodLabels[key]}: ${e instanceof Error ? e.message : 'Geçerli bir sayı yazın.'}`); }
    })) as Record<PeriodField, number>;
  }
  function parseChannels(f: FormData): ChannelInput[] {
    if (rows.length === 0) throw new Error('En az bir kanal satırı ekleyin.');
    if (rows.length > 20) throw new Error('Bir dönemde en fazla 20 kanal satırı olabilir.');
    const seen = new Set<string>();
    return rows.map(({ key }) => {
      const id = String(f.get(`channel-${key}`) ?? '');
      if (!id) throw new Error('Her satırda satış kanalını seçin.');
      if (seen.has(id)) throw new Error(`"${channelName(id)}" bir dönemde yalnız bir kez yer alabilir.`);
      seen.add(id);
      const values = {} as Record<ChannelSalesField, number>;
      for (const field of salesFields) {
        try { values[field] = periodNumber(String(f.get(`channel-${key}-${field}`) ?? '')); }
        catch (e) { throw new Error(`${channelName(id)} · ${periodLabels[field]}: ${e instanceof Error ? e.message : 'Geçerli bir sayı yazın.'}`); }
      }
      if (values.vat + values.refunds + values.cancellations + values.chargebacks + values.customerPaidShipping + values.giftCardTopups > values.grossSales)
        throw new Error(`"${channelName(id)}" satırında kesintilerin toplamı brüt satışı geçemez.`);
      return { salesChannelId: id, ...values };
    });
  }
  async function preview() {
    if (inFlight.current || !form.current?.reportValidity()) return;
    inFlight.current = true; setBusy(true); setError(''); setReview(null);
    try {
      const f = new FormData(form.current); const deal = deals.data?.find(d => d.id === f.get('dealId'));
      if (!baseline && !deal) throw new Error('Etkin anlaşmayı seçin.');
      const fields = parseHeader(f, channelEntry);
      const input: PeriodInput = { ...fields, brandId: baseline?.brandId ?? deal!.brandId, dealId: baseline?.dealId ?? deal!.id,
        year: baseline?.year ?? Number(f.get('year')), month: baseline?.month ?? Number(f.get('month')) };
      if (channelEntry) {
        const lines = parseChannels(f);
        input.channels = lines;
        for (const field of salesFields) input[field] = lines.reduce((sum, line) => sum + line[field], 0);
      }
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
  async function applySubmission() {
    if (inFlight.current || busy || !form.current) return;
    const f = new FormData(form.current);
    let brandId = baseline?.brandId; let year = baseline?.year; let month = baseline?.month;
    let currency = baseline?.deal.currency ?? 'TRY';
    if (!baseline) {
      const deal = deals.data?.find(d => d.id === String(f.get('dealId') ?? ''));
      if (!deal) { setError('Müşteri bildirimi için önce etkin anlaşmayı seçin.'); return; }
      brandId = deal.brandId; year = Number(f.get('year')); month = Number(f.get('month')); currency = deal.currency;
    }
    if (!year || !month || month < 1 || month > 12) { setError('Önce geçerli bir yıl ve ay seçin.'); return; }
    inFlight.current = true; setBusy(true); setError(''); setHint(''); setReview(null);
    try {
      const list = await api<Submission[]>(`/api/portal-management/brands/${brandId}/submissions`);
      const found = list.find(s => s.year === year && s.month === month);
      if (!found) { setHint(`${month}/${year} dönemi için müşteri bildirimi yok. Bildirim geldiğinde bu düğme onu forma yazar.`); return; }
      const ok = await confirm({ title: 'Müşteri bildirimiyle doldur',
        message: `${month}/${year} dönemi için marka yetkilisinin bildirdiği değerler forma yazılacak ve onayınız işlem geçmişine kaydedilecek. Bu değerler hiçbir finansal kaydı kendiliğinden değiştirmez; kontrol edip “Hesabı kontrol et” ile devam edersiniz.`,
        confirmLabel: 'Formu doldur' });
      if (!ok) return;
      const applied = await api<Submission>(`/api/portal-management/brands/${brandId}/submissions/${found.id}/apply`,
        { method: 'POST', body: JSON.stringify({ reason: 'Müşteri bildirimi ön doldurma onayı' }) });
      const written: string[] = [];
      const write = (name: PeriodField, value: number | null, label: string) => {
        if (value === null) return;
        const el = form.current?.elements.namedItem(name);
        if (el instanceof HTMLInputElement) el.value = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 4 }).format(value);
        written.push(`${label} ${moneyPrecise(value, currency)}`);
      };
      write('grossSales', applied.grossSales, 'Brüt satış');
      write('refunds', applied.refunds, 'İadeler');
      write('metaSpend', applied.metaSpend, 'Meta harcaması');
      write('googleSpend', applied.googleSpend, 'Google harcaması');
      setDirty(true);
      setHint('Müşteri bildiriminden yazıldı: ' + (written.join(' · ') || 'alanların tamamı boş') + '.'
        + ' Yazılan tutarları kontrol edip “Hesabı kontrol et” ile devam edin; kayıt kendiliğinden yapılmaz.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Müşteri bildirimi kullanılamadı.'); }
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
  const showChannels = channelEntry;
  const channelBreakdown = (json: string): BreakdownChannel[] => {
    try { return (JSON.parse(json) as { channels?: BreakdownChannel[] }).channels ?? []; } catch { return []; }
  };
  return <form ref={form} className="space-y-4" onSubmit={e => { e.preventDefault(); void preview(); }} onChange={() => { setDirty(true); setReview(null); setHint(''); }}>
    <Card className="p-5"><h2 className="font-semibold">{baseline ? 'Taslak dönemi düzenle' : 'Yeni aylık sonuç'}</h2>
      <p className="my-3 text-sm">Tutarları <strong>1.234,56</strong> biçiminde yazın; en fazla dört ondalık basamak kullanın. Hareket yoksa 0 yazın; bilinmeyen değeri sıfır saymayın. Kaydetmek dönemi onaylamaz.</p>
      <Link href="/guide#13-aylik-donem-kapatma-sureci" className="text-sm underline">Bu işlem nasıl yapılır?</Link>
      {activeChannels.length > 0 && <fieldset className="mt-3 flex flex-wrap gap-4 text-sm"><legend className="font-semibold">Ciro girişi</legend>
        <label><input type="radio" name="ciro-girisi" checked={showMode === 'single'} onChange={() => { setModeTouched(true); setMode('single'); setReview(null); }} /> Tek toplam</label>
        <label><input type="radio" name="ciro-girisi" checked={showMode === 'channels'} onChange={() => { setModeTouched(true); setMode('channels'); setReview(null); }} /> Kanal kırılımlı ({activeChannels.length} kanal)</label>
      </fieldset>}
      {baseline ? <p className="mt-3 font-medium">{baseline.brand.name} · {baseline.month}/{baseline.year} · {baseline.deal.currency}<span className="block text-sm font-normal">Marka, anlaşma ve ay değişmez. Mevcut hakediş düzeltmeleri korunur.</span></p> : <>
        {deals.isPending && <p role="status">Anlaşmalar yükleniyor…</p>}{deals.isError && <p role="alert">{deals.error.message}</p>}
        {deals.data && !deals.data.some(d => d.status === 'Active') && <p>Etkin anlaşma yok. Önce ilgili markanın anlaşmasını tamamlayın.</p>}
        <div className="mt-4 grid gap-3 sm:grid-cols-3"><label>Etkin anlaşma<select name="dealId" required className="input mt-1" defaultValue="" disabled={busy} onChange={e => setDealId(e.target.value)}><option value="">Anlaşma seçin</option>{deals.data?.filter(d => d.status === 'Active').map(d => <option key={d.id} value={d.id}>{d.brand.name} · {d.name} · {d.currency}</option>)}</select></label>
          <label>Yıl<input name="year" type="number" min={2020} max={2100} required className="input mt-1" defaultValue={new Date().getFullYear()} disabled={busy}/></label>
          <label>Ay<input name="month" type="number" min={1} max={12} required className="input mt-1" defaultValue={new Date().getMonth() + 1} disabled={busy}/></label></div></>}
    {mode === 'single' && <div className="mt-3 border-t pt-3">
      <div className="flex flex-wrap gap-2">
        <button type="button" className={button} disabled={busy} onClick={() => void suggest()}>Mağaza verisinden brüt satış öner</button>
        <button type="button" className={button} disabled={busy} onClick={() => void pullAds()}>Reklam harcamasını getir</button>
        <button type="button" className={button} disabled={busy} onClick={() => void applySubmission()}>Müşteri bildirimini kullan</button>
      </div>
      <p className="mt-1 text-xs text-[#6d7175]">Mağazadan çekilmiş siparişlerden bu dönemin brüt satış tutarını alan yazar. “Reklam harcamasını getir” ise Meta/Google bağlantıları kayıtlıysa bu dönemin harcamasını ilgili alanlara yazar. “Müşteri bildirimini kullan” marka yetkilisinin bildirdiği brüt satış, iade ve reklam tutarlarını onayınızla forma yazar ve onayı kayda geçirir. Üçü de otomatik kaydetmez, yazılanı siz kontrol edersiniz.</p>
      {hint && <p role="status" className="mt-2 text-sm">{hint}</p>}
    </div>}
    {showChannels && <p className="mt-3 border-t pt-3 text-sm">Her kanalın satış ve kesintilerini ayrı girin; üst toplamlar satırların toplamı olur. Mağaza önerisi ve müşteri bildirimi tek toplam içindir, kanallı girişte kapalıdır. Kanal oranları anlaşma sayfasından tanımlanır.</p>}
    </Card>
    {showChannels && <Card className="p-5"><div className="mb-4 flex flex-wrap items-center justify-between gap-2"><h3 className="font-semibold">Kanal satırları</h3><button type="button" className={button} disabled={busy || rows.length >= 20} onClick={() => { setRows(r => [...r, { key: keyRef.current++ }]); setReview(null); }}>Kanal satırı ekle</button></div>
      <div className="space-y-4">{rows.map(({ key, initial: row }) => <fieldset key={key} className="rounded-lg border p-4"><div className="flex flex-wrap items-center justify-between gap-2">
        <label className="grow text-sm">Satış kanalı<select name={`channel-${key}`} required className="input mt-1" disabled={busy} defaultValue={row?.salesChannelId ?? ''}>
          <option value="">Kanal seçin</option>{activeChannels.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          {row && !activeChannels.some(c => c.id === row.salesChannelId) && <option value={row.salesChannelId}>{row.salesChannel?.name ?? 'Pasif kanal'} (pasif)</option>}
        </select></label>
        {rows.length > 1 && <button type="button" className="text-sm underline" disabled={busy} onClick={() => { setRows(r => r.filter(x => x.key !== key)); setReview(null); }}>Satırı kaldır</button>}</div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{salesFields.map(field => <label key={field} className="text-sm">{periodLabels[field]}<input name={`channel-${key}-${field}`} required inputMode="decimal" autoComplete="off" className="input mt-1" disabled={busy} defaultValue={row ? trNumber(row[field]) : ''}/></label>)}</div>
      </fieldset>)}</div></Card>}
    <fieldset disabled={busy} className="space-y-4">{periodGroups.filter(g => showChannels ? g.title !== 'Satış ve kesintiler' : true).map(g => <Card className="p-5" key={g.title}><h3 className="mb-4 font-semibold">{g.title}</h3><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{g.fields.map(key => <label key={key} className="text-sm">{periodLabels[key]}<input name={key} required inputMode="decimal" autoComplete="off" className="input mt-1" defaultValue={baseline ? String(baseline[key]).replace('.', ',') : ''}/></label>)}</div></Card>)}</fieldset>
    {error && <p role="alert" className="text-red-700">{error}</p>}
    <div className="flex flex-wrap gap-3"><button type="submit" disabled={busy} className={button}>{busy ? 'İşlem yapılıyor…' : 'Hesabı kontrol et'}</button>{onCancel && <button type="button" disabled={busy} className={button} onClick={async () => { if (dirty && !(await confirm({ title: 'Düzenlemeden çıksın mı?', message: 'Değişiklikler kaydedilmedi. Düzenlemeden çıkmak istiyor musunuz?' }))) return; onCancel(); }}>Düzenlemeden çık</button>}</div>
    {review && <Card className="space-y-3 border-green-700 p-5"><h3 className="font-semibold">Kaydetmeden önce son kontrol</h3><p>{review.name} · {review.input.month}/{review.input.year} · {review.currency}</p>
      <dl className="space-y-2">{[['Net ciro', review.result.netRevenue], ['Reklam gideri', review.result.totalAdSpend], ['OVO hakedişi', review.result.ovoFee], ['Markaya kalan katkı', review.result.brandContributionProfit]].map(([label, value]) => <div key={label} className="flex flex-wrap justify-between gap-2"><dt>{label}</dt><dd>{moneyPrecise(Number(value), review.currency)}</dd></div>)}</dl>
      {channelBreakdown(review.result.commissionBreakdownJson).length > 0 && <div><h4 className="mt-3 font-semibold">Kanal kırılımı</h4><dl className="mt-1 space-y-2">{channelBreakdown(review.result.commissionBreakdownJson).map(line => <div key={line.salesChannelId} className="flex flex-wrap justify-between gap-2 text-sm"><dt>{line.channelName} · {moneyPrecise(line.commissionableRevenue, review.currency)} × {percent(line.rate)}</dt><dd>{moneyPrecise(line.fee, review.currency)}</dd></div>)}</dl></div>}
      <p className="text-sm">Bilgileri kaynak raporla kontrol ettiyseniz aşağıdaki düğmeyle taslağı kaydedin. Sonraki adım incelemeye göndermektir; onay ve kilit ayrıca yapılır.</p>
      <button type="button" className={button} disabled={busy} onClick={() => void save()}>Kontrol ettim, taslağı kaydet</button></Card>}
  </form>;
}
