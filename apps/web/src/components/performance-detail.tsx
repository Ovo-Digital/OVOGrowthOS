'use client';
import Link from 'next/link';
import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, percent, type SessionUser } from '@/lib/api';
import { periodNumber, type PeriodSnapshot } from '@/lib/period-input';
import { turkce } from '@/lib/turkish';
import { Badge, Card, MetricCard, PageHeader } from '@/components/ui/core';
import { ServiceCosts } from '@/components/operating-costs';
import { PerformanceForm } from '@/components/performance-form';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Period = PeriodSnapshot & { status: string; preparedBy: string; reviewedBy: string; submittedAt: string | null; approvedAt: string | null;
  netRevenue: number; contributionBeforeMarketing: number; totalAdSpend: number; contributionBeforeOvo: number; ovoFee: number;
  brandContributionProfit: number; ovoGrossProfit: number; ovoMargin: number; mer: number; commissionBreakdownJson: string;
  adjustments: { id: string; amount: number; reason: string }[] };
type Action = 'submit' | 'approve' | 'lock' | 'return' | 'unlock' | 'adjustments';
const labels: Record<Action, string> = { submit: 'İncelemeye gönder', approve: 'Onayla', lock: 'Dönemi kilitle', return: 'Gerekçeyle taslağa gönder', unlock: 'Dönem kilidini aç', adjustments: 'Hakediş düzeltmesi ekle' };
const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';

export function PerformanceDetail({ id }: { id: string }) {
  const qc = useQueryClient(); const [editing, setEditing] = useState(false); const [pending, setPending] = useState<{ action: Action; period: Period } | null>(null);
  const [notice, setNotice] = useState('');
  const query = useQuery({ queryKey: ['performance', id], queryFn: () => api<Period>(`/api/performance/${id}`) });
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  async function saved() {
    setEditing(false); setPending(null); setNotice('İşlem kaydedildi. Kaydın güncel aşamasını kontrol edin.');
    await Promise.all(['performance', 'dashboard', 'commissions', 'tasks', 'work-approvals', 'brand-report', 'portal-admin', 'portal-preview', 'service-costs', 'collection', 'audit'].map(key => qc.invalidateQueries({ queryKey: [key] })));
  }
  if (query.isError || me.isError) return <Card className="p-5"><p role="alert">{query.error?.message ?? me.error?.message}</p><button className={button} onClick={() => { void query.refetch(); void me.refetch(); }}>Yeniden dene</button></Card>;
  if (!query.data || !me.data) return <p role="status">Dönem ve hesap bilgileri yükleniyor…</p>;
  const p = query.data; const operations = ['Admin', 'Partner'].includes(me.data.role); const admin = me.data.role === 'Admin';
  const closed = ['Locked', 'Invoiced', 'Paid'].includes(p.status); const ownReview = p.preparedBy.toLowerCase() === me.data.email.toLowerCase();
  const money = (value: number) => moneyPrecise(value, p.deal.currency);
  const next: Action | null = p.status === 'Draft' ? 'submit' : p.status === 'UnderReview' ? 'approve' : p.status === 'Approved' ? 'lock' : null;
  const show = (action: Action) => { setNotice(''); setPending({ action, period: p }); };
  return <><PageHeader title={`${p.brand.name} · ${p.month}/${p.year}`} description={`${p.deal.currency} · ${closed ? 'Kapanmış dönem' : 'Henüz kapanmamış dönem'}`}/>
    <div className="mb-4 flex flex-wrap items-center gap-3"><Badge>{turkce(p.status)}</Badge><Link className="text-sm underline" href={`/commissions/${id}`}>Hakediş dökümü ve tahsilat takibi</Link><Link className="text-sm underline" href={`/activity?entityType=MonthlyPerformance&entityId=${id}`}>İşlem geçmişi</Link><Link className="text-sm underline" href="/guide#13-aylik-donem-kapatma-sureci">Kapanış kullanım rehberi</Link></div>
    {notice && <p role="status" className="mb-4 rounded-lg bg-green-50 p-3">{notice}</p>}
    {editing ? <PerformanceForm initial={p} onSaved={() => void saved()} onCancel={() => setEditing(false)}/> : <>
      {operations && !pending && <Card className="mb-4 space-y-3 p-4"><div className="flex flex-wrap gap-2">
        {p.status === 'Draft' && <><button className={button} onClick={() => setEditing(true)}>Aylık verileri düzenle</button><button className={button} onClick={() => show('adjustments')}>{labels.adjustments}</button></>}
        {next && <button className={button} disabled={next === 'approve' && ownReview} onClick={() => show(next)}>{labels[next]}</button>}
        {['UnderReview', 'Approved'].includes(p.status) && <button className={button} onClick={() => show('return')}>{labels.return}</button>}
        {p.status === 'Locked' && admin && <button className={button} onClick={() => show('unlock')}>{labels.unlock}</button>}</div>
        {p.status === 'UnderReview' && ownReview && <p className="text-sm">Bu dönemi siz incelemeye gönderdiniz. Onayı başka bir yönetici veya iş ortağı kendi hesabından vermelidir.</p>}
        {['UnderReview', 'Approved'].includes(p.status) && <p className="text-sm">Rakam değişecekse önce gerekçeyle taslağa gönderin. Eski onay kaldırılır; tekrar inceleme gerekir.</p>}
        {closed && <p className="text-sm">Kapalı rakamlar düzenlenemez. Faturalanmış ve ödenmiş dönemler yeniden açılamaz.</p>}
      </Card>}
      {pending && <PeriodAction period={pending.period} action={pending.action} onSaved={() => void saved()} onCancel={() => setPending(null)}/>}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4"><MetricCard label="NET CİRO" value={money(p.netRevenue)}/><MetricCard label="OVO HAKEDİŞİ" value={money(p.ovoFee)}/><MetricCard label="OVO BRÜT KÂR MARJI" value={p.ovoFee === 0 ? 'Hesaplanamıyor' : percent(p.ovoMargin)}/><MetricCard label="MER" value={p.totalAdSpend === 0 ? 'Hesaplanamıyor' : `${p.mer.toFixed(2)}x`}/></div>
      <div className="mt-4 grid gap-4 xl:grid-cols-[1.4fr_1fr]"><Card className="p-5"><h2 className="font-semibold">Gelir ve gider özeti</h2><dl className="mt-4">{[
        ['Brüt satış', p.grossSales], ['− KDV', -p.vat], ['− İadeler', -p.refunds], ['− İptaller', -p.cancellations], ['− Ters ibrazlar', -p.chargebacks],
        ['− Müşterinin ödediği kargo', -p.customerPaidShipping], ['− Hediye kartı yüklemeleri', -p.giftCardTopups], ['= Net ciro', p.netRevenue],
        ['− Ürün maliyeti', -p.cogs], ['− Ödeme sistemi giderleri', -p.paymentFees], ['− Sipariş hazırlama', -p.fulfillmentCosts], ['− Kargo desteği', -p.shippingSubsidy],
        ['− Diğer değişken giderler', -p.otherVariableCosts], ['= Reklam öncesi katkı', p.contributionBeforeMarketing], ['− Reklam harcaması', -p.totalAdSpend],
        ['= OVO öncesi katkı', p.contributionBeforeOvo], ['− OVO hakedişi', -p.ovoFee], ['= Markanın katkı kârı', p.brandContributionProfit],
      ].map(([label, value]) => <div className={`flex flex-wrap justify-between gap-2 py-2 text-sm ${String(label).startsWith('=') ? 'border-t font-bold' : 'text-[#6d7175]'}`} key={label}><dt>{label}</dt><dd>{money(Number(value))}</dd></div>)}</dl></Card>
      <div className="space-y-4"><Card className="p-5"><h2 className="font-semibold">Hakediş özeti</h2><CommissionSummary json={p.commissionBreakdownJson} currency={p.deal.currency}/></Card>
        <Card className="p-5"><h2 className="font-semibold">Elle yapılan düzeltmeler</h2>{p.adjustments.length ? p.adjustments.map(a => <div key={a.id} className="mt-3 flex flex-wrap justify-between gap-2 text-sm"><span className="break-words">{a.reason}</span><strong>{money(a.amount)}</strong></div>) : <p className="mt-3 text-sm">Henüz hakediş düzeltmesi yok.</p>}</Card></div></div>
      <ServiceCosts id={id}/></>}
  </>;
}

function PeriodAction({ period: p, action, onSaved, onCancel }: { period: Period; action: Action; onSaved: () => void; onCancel: () => void }) {
  const [reason, setReason] = useState(''); const [amount, setAmount] = useState(''); const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const working = useRef(false);
  useUnsavedChanges(Boolean(reason || amount));
  const needsReason = ['return', 'unlock', 'adjustments'].includes(action);
  return <Card className="mb-4 p-5"><form className="space-y-3" onSubmit={async e => {
    e.preventDefault(); if (working.current || !confirmed) return;
    working.current = true; setBusy(true); setError('');
    try {
      const body = action === 'adjustments' ? { reason, amount: periodNumber(amount, false, true) } : { reason };
      await api(`/api/performance/${p.id}/${action}`, { method: 'POST', headers: { 'If-Match': `"${p.updatedAt}"` }, body: JSON.stringify(body) });
      onSaved();
    } catch (e) { setError(e instanceof Error ? e.message : 'İşlem tamamlanamadı.'); }
    finally { working.current = false; setBusy(false); }
  }}><h2 className="font-semibold">{labels[action]}</h2><p>{p.brand.name} · {p.month}/{p.year} · Mevcut hakediş: {moneyPrecise(p.ovoFee, p.deal.currency)}</p>
    <p className="text-sm">{action === 'return' || action === 'unlock' ? 'Kayıt taslağa dönecek. Önceki onay kaldırılacak ve farklı kişiyle yeniden onay süreci gerekecek. Yayımlanmış rapor sürümleri değişmez.' : action === 'adjustments' ? 'Yalnız hakediş düzeltmesi eklenir; brüt satış veya iade verisi değişmez. Azaltma için eksi tutar yazın.' : action === 'lock' ? 'Onaylanmış rakamlar kapanmış döneme alınacak. Bu işlem para tahsil etmez veya müşteriye rapor yayımlamaz.' : 'Kaydın rakamlarını kaynak belgelerle kontrol edin. İşlem sizin hesabınızla geçmişe kaydedilecek.'}</p>
    {action === 'adjustments' && <label className="block text-sm">Düzeltme tutarı ({p.deal.currency})<input className="input mt-1" inputMode="decimal" required value={amount} disabled={busy} onChange={e => { setAmount(e.target.value); setConfirmed(false); }}/></label>}
    {needsReason && <label className="block text-sm">İşlem gerekçesi<textarea className="input mt-1" required minLength={action === 'adjustments' ? 5 : 1} maxLength={action === 'adjustments' ? 500 : 1000} value={reason} rows={3} disabled={busy} onChange={e => { setReason(e.target.value); setConfirmed(false); }}/></label>}
    <label className="flex items-start gap-2 text-sm"><input type="checkbox" required checked={confirmed} disabled={busy} onChange={e => setConfirmed(e.target.checked)}/>Markayı, dönemi ve işlem sonucunu kontrol ettim.</label>
    {error && <p role="alert" className="text-red-700">{error} Güncel kaydı görmek için işlemi kapatıp sayfayı yenileyebilirsiniz.</p>}
    <div className="flex flex-wrap gap-3"><button className={button} disabled={busy || !confirmed}>{busy ? 'Kaydediliyor…' : 'Onayladığım işlemi uygula'}</button><button type="button" className={button} disabled={busy} onClick={onCancel}>Vazgeç</button></div>
  </form></Card>;
}

function CommissionSummary({ json, currency }: { json: string; currency: string }) {
  const b = JSON.parse(json) as { baseRetainer: number; calculatedShare: number; minimumFee: number; adjustments: number; finalFee: number; effectiveRate: number };
  const money = (n: number) => moneyPrecise(n, currency);
  return <dl className="mt-3 space-y-2 text-sm">{[['Sabit aylık ücret', money(b.baseRetainer)], ['Hesaplanan ciro payı', money(b.calculatedShare)], ['Asgari ücret', money(b.minimumFee)], ['Düzeltmeler', money(b.adjustments)], ['Son OVO hakedişi', money(b.finalFee)], ['Gerçekleşen oran', percent(b.effectiveRate)]].map(([label, value]) => <div className="flex flex-wrap justify-between gap-2 border-b py-2" key={label}><dt>{label}</dt><dd className="font-semibold">{value}</dd></div>)}</dl>;
}
