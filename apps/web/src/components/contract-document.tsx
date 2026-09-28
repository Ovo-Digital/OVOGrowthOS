'use client';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise, percent } from '@/lib/api';
import { turkce } from '@/lib/turkish';

type Condition = { id: string; code: string; title: string; description: string; required: boolean; status: string; resolutionReason: string };
type Deal = {
  id: string; name: string; status: string; dealType: string; currency: string; brand: { name: string };
  contractMonths: number; baselineRevenue: number; baselinePeriodStart?: string | null; baselinePeriodEnd?: string | null;
  baselineCalculationMethod: string; monthlyRetainer: number; minimumMonthlyFee: number; revenueShareRate: number;
  incrementalRate: number; profitShareRate: number; commissionTiersJson: string; setupInvestment: number;
  startDate?: string | null; endDate?: string | null; statusReason: string; conditions: Condition[];
};
type ScopeState = {
  items: { id: string; title: string; description: string }[];
  requests: { id: string; title: string; description: string; status: string; statusLabel: string; requestedBy: string; decidedBy: string; decidedAt: string | null; decisionNote: string }[];
};
type Tier = { lowerBound: number; upperBound: number | null; rate: number };

const conditionLabels: Record<string, string> = { Pending: 'Bekliyor', Satisfied: 'Karşılandı', Waived: 'Muaf bırakıldı' };
const baseMethods: Record<string, string> = { Manual: 'Manuel', Trailing3MonthAverage: 'Son 3 ay ortalaması', Trailing6MonthAverage: 'Son 6 ay ortalaması', Trailing12MonthAverage: 'Son 12 ay ortalaması' };

export function ContractDocument({ dealId }: { dealId: string }) {
  const [open, setOpen] = useState(false);
  const dealQ = useQuery({ queryKey: ['deal', dealId], queryFn: () => api<Deal>('/api/deals/' + dealId), enabled: open });
  const scopeQ = useQuery({ queryKey: ['deal-scope', dealId], queryFn: () => api<ScopeState>(`/api/deals/${dealId}/scope`), enabled: open, refetchOnWindowFocus: false });

  if (!open) return <button className="mb-4 rounded-lg border px-4 py-2 text-sm font-semibold" onClick={() => setOpen(true)}>Sözleşme ve ek protokol belgesi (PDF)</button>;
  const loading = dealQ.isPending || scopeQ.isPending;
  const failed = dealQ.isError || scopeQ.isError;
  const d = dealQ.data; const s = scopeQ.data;
  const tiers = (() => { try { return JSON.parse(d?.commissionTiersJson || '[]') as Tier[]; } catch { return []; } })();
  return <>
    <div className="report-controls mb-4 flex flex-wrap gap-3">
      <button className="rounded-lg border px-4 py-2 text-sm font-semibold" onClick={() => setOpen(false)}>Belgeyi kapat</button>
      <button className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={loading || failed} onClick={() => window.print()}>PDF’ye kaydet / yazdır</button>
      {failed && <p role="alert" className="text-sm">{dealQ.error?.message || scopeQ.error?.message} <button className="underline" onClick={() => { void dealQ.refetch(); void scopeQ.refetch(); }}>Yeniden dene</button></p>}
      {!failed && loading && <p className="text-sm" role="status">Belge hazırlanıyor…</p>}
    </div>
    {d && s && <article className="contract-document rounded-xl border bg-white p-5 sm:p-8">
      <header className="report-heading mb-5 border-b pb-4">
        <p className="text-sm font-bold">OVO Growth OS</p>
        <h1 className="mt-2 text-2xl font-bold">İş Birliği Sözleşmesi Özeti</h1>
        <p className="mt-2 text-sm">{d.brand.name} · {d.name} · {turkce(d.status)}</p>
        <p className="mt-1 text-xs">Belge tarihi: {new Date().toLocaleDateString('tr-TR', { timeZone: 'Europe/Istanbul' })} (Türkiye saati)</p>
      </header>
      <p className="mb-4 rounded-lg border p-3 text-sm">Bu belge sistemdeki anlaşma kaydından üretilmiş bilgi özetidir; imza yerine geçmez ve yasal sözleşme metninin yerini almaz. Tutarlar KDV hariçtir. Gizli maliyet ve kâr beklentileri bu belgede yer almaz.</p>

      <h2 className="text-lg font-semibold">Taraflar ve dönem</h2>
      <dl className="mt-3 space-y-2 text-sm">{[
        ['Anlaşma sahibi marka', d.brand.name],
        ['Anlaşma modeli', turkce(d.dealType)],
        ['Dönem', `${d.startDate ?? 'Belirlenmedi'} – ${d.endDate ?? 'Belirlenmedi'}`],
        ['Süre', `${d.contractMonths} ay`],
        ['Para birimi', d.currency],
        ['Durum', `${turkce(d.status)}${d.statusReason ? ` — ${d.statusReason}` : ''}`]
      ].map(([label, value]) => <div className="flex flex-wrap justify-between gap-2 border-b pb-2" key={label}><dt className="text-[#6d7175]">{label}</dt><dd className="font-semibold">{value}</dd></div>)}</dl>

      <h2 className="mt-6 text-lg font-semibold">Ticari koşullar</h2>
      <dl className="mt-3 space-y-2 text-sm">{[
        ['Aylık sabit ücret', moneyPrecise(d.monthlyRetainer, d.currency)],
        ['Asgari aylık ücret', moneyPrecise(d.minimumMonthlyFee, d.currency)],
        ['Kurulum yatırımı', moneyPrecise(d.setupInvestment, d.currency)],
        ['Gelir payı', percent(d.revenueShareRate)],
        ['Büyüme payı', percent(d.incrementalRate)],
        ['Kâr payı', percent(d.profitShareRate)],
        ['Baz ciro', `${moneyPrecise(d.baselineRevenue, d.currency)} (${baseMethods[d.baselineCalculationMethod] ?? turkce(d.baselineCalculationMethod)}${d.baselinePeriodStart && d.baselinePeriodEnd ? `, ${d.baselinePeriodStart} – ${d.baselinePeriodEnd}` : ''})`]
      ].map(([label, value]) => <div className="flex flex-wrap justify-between gap-2 border-b pb-2" key={label}><dt className="text-[#6d7175]">{label}</dt><dd className="font-semibold">{value}</dd></div>)}</dl>
      <p className="mt-3 text-sm"><span className="text-[#6d7175]">Kademeli hakediş oranları:</span> {tiers.length === 0 ? 'Kayıtlı kademeli oran yok.' : <ul className="mt-1 list-disc space-y-1 pl-5">{tiers.map((t, i) => <li key={i}>{t.upperBound === null ? `${moneyPrecise(t.lowerBound, d.currency)} ve üzeri` : `${moneyPrecise(t.lowerBound, d.currency)} – ${moneyPrecise(t.upperBound, d.currency)}`} arası: {percent(t.rate)}</li>)}</ul>}</p>

      <h2 className="mt-6 text-lg font-semibold">Hizmet kapsamı</h2>
      {s.items.length === 0 ? <p className="mt-3 text-sm">Bu anlaşma için henüz kapsam kalemi yazılmadı.</p> :
        <ul className="mt-3 list-disc space-y-2 pl-5 text-sm">{s.items.map(item => <li key={item.id}><strong>{item.title}</strong>{item.description && <p className="mt-1 whitespace-pre-wrap text-[#6d7175]">{item.description}</p>}</li>)}</ul>}

      <h2 className="mt-6 text-lg font-semibold">Ek protokoller (paket dışı talepler)</h2>
      {s.requests.filter(r => r.status !== 'Rejected').length === 0 ? <p className="mt-3 text-sm">Kayıtlı ek protokol talebi yok.</p> :
        <ul className="mt-3 space-y-2 text-sm">{s.requests.filter(r => r.status !== 'Rejected').map(r => <li className="rounded-lg border p-3" key={r.id}>
          <strong>{r.title}</strong> — {r.statusLabel}
          {r.description && <p className="mt-1 whitespace-pre-wrap text-[#6d7175]">{r.description}</p>}
          <p className="mt-1 text-xs text-[#6d7175]">İsteyen: {r.requestedBy}{r.decidedAt && ` · Karar: ${r.decidedBy}`}</p>
          {r.decisionNote && <p className="mt-1 text-sm">Karar notu: {r.decisionNote}</p>}
        </li>)}
        <li className="text-xs text-[#6d7175]">Reddedilen talepler bu belgeye dahil edilmez.</li></ul>}

      <h2 className="mt-6 text-lg font-semibold">Anlaşma koşulları</h2>
      {d.conditions.length === 0 ? <p className="mt-3 text-sm">Kayıtlı koşul yok.</p> :
        <ul className="mt-3 space-y-2 text-sm">{d.conditions.map(c => <li className="rounded-lg border p-3" key={c.id}>
          <strong>{c.title}</strong> — {conditionLabels[c.status] ?? turkce(c.status)}{c.required && ' (zorunlu)'}
          {c.description && <p className="mt-1 text-[#6d7175]">{c.description}</p>}
          {c.resolutionReason && <p className="mt-1 text-sm">Açıklama: {c.resolutionReason}</p>}
        </li>)}</ul>}

      <footer className="mt-6 border-t pt-3 text-xs">Bu belge OVO Growth OS’ten {new Date().toLocaleDateString('tr-TR', { timeZone: 'Europe/Istanbul' })} tarihinde üretilmiştir. Belge, sistemde açık olan anlaşma kaydının anlık özetini yansıtır; güncel kayıtlarla çelişirse sistemdeki kayıt esastır.</footer>
    </article>}
    <style>{`@media print { @page { size: A4; margin: 14mm; } body:has(.contract-document) aside, body:has(.contract-document) header:not(.report-heading), .report-controls { display: none !important; } body:has(.contract-document) .lg\\:ml-\\[240px\\] { margin-left: 0 !important; } body:has(.contract-document) main { max-width: none !important; padding: 0 !important; } .contract-document { border: 0 !important; padding: 0 !important; font-size: 10pt; } .report-heading, li, dl, tr { break-inside: avoid; } a { color: inherit !important; text-decoration: none !important; } }`}</style>
  </>;
}
