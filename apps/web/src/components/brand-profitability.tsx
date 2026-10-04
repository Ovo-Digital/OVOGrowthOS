'use client';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';

type Row = { brandId: string; brandName: string; currency: string; periods: number; totalFee: number; totalRecordedCost: number; confirmedPeriods: number; unconfirmedPeriods: number; contribution: number; margin: number | null };
type Report = { currency: string; from: string | null; to: string | null; currencies: string[]; rows: Row[] };
const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';

export function BrandProfitabilityCard() {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const [currency, setCurrency] = useState('TRY');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [applied, setApplied] = useState({ currency: 'TRY', from: '', to: '' });
  const allowed = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const query = useQuery({
    queryKey: ['brand-profitability', applied],
    queryFn: () => api<Report>(`/api/reports/brand-profitability?currency=${encodeURIComponent(applied.currency)}${applied.from ? `&from=${applied.from}` : ''}${applied.to ? `&to=${applied.to}` : ''}`),
    enabled: allowed,
  });
  if (me.isPending) return null;
  if (!allowed) return null;
  const data = query.data;
  return <Card className="mb-4 p-5"><h2 className="font-semibold">Hangi marka gerçekten kazandırıyor?</h2>
    <p className="my-3 text-sm">Kapanmış dönemlerdeki OVO hakedişlerinden, kontrolü tamamlanmış gerçek hizmet giderleri düşülür. Kontrolü bitmemiş giderler katkıyı şişirmez; kaç dönemin beklediği ayrıca yazılır. Yalnız bilgi verir; kayıt değiştirmez.</p>
    <div className="flex flex-wrap items-end gap-3">
      <label className="text-sm">Para birimi<select className="input mt-1" value={currency} onChange={e => setCurrency(e.target.value)}>{(data?.currencies.length ? data.currencies : ['TRY']).map(x => <option key={x}>{x}</option>)}</select></label>
      <label className="text-sm">Başlangıç (isteğe bağlı)<input className="input mt-1" value={from} placeholder="2026-01" onChange={e => setFrom(e.target.value)} /></label>
      <label className="text-sm">Bitiş (isteğe bağlı)<input className="input mt-1" value={to} placeholder="2026-12" onChange={e => setTo(e.target.value)} /></label>
      <button className={button} disabled={query.isFetching} onClick={() => setApplied({ currency, from: from.trim(), to: to.trim() })}>Sıralamayı göster</button>
    </div>
    <div className="mt-4">{query.isPending ? <LoadingState label="Kârlılık hesaplanıyor…" /> : query.isError ? <ErrorState message={query.error.message} /> : data && (
      data.rows.length === 0 ? <EmptyState message="Bu aralıkta kapanmış dönem bulunamadı." /> :
        <div className="overflow-x-auto rounded-lg border"><table className="w-full min-w-[720px] text-sm">
          <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">#</th><th className="px-4 py-2">Marka</th><th className="px-4 py-2 text-right">Dönem</th><th className="px-4 py-2 text-right">OVO hakedişi</th><th className="px-4 py-2 text-right">Gerçekleşen gider</th><th className="px-4 py-2 text-right">Kalan katkı</th><th className="px-4 py-2 text-right">Katkı oranı</th><th className="px-4 py-2">Kontrol durumu</th></tr></thead>
          <tbody>{data.rows.map((r, i) => <tr key={r.brandId} className="border-t">
            <td className="px-4 py-2">{i + 1}</td>
            <td className="px-4 py-2 font-medium">{r.brandName}</td>
            <td className="px-4 py-2 text-right">{r.periods}</td>
            <td className="px-4 py-2 text-right">{moneyPrecise(r.totalFee, r.currency)}</td>
            <td className="px-4 py-2 text-right">{moneyPrecise(r.totalRecordedCost, r.currency)}</td>
            <td className="px-4 py-2 text-right font-semibold">{moneyPrecise(r.contribution, r.currency)}</td>
            <td className="px-4 py-2 text-right">{r.margin === null ? 'Hesaplanamıyor' : new Intl.NumberFormat('tr-TR', { style: 'percent', maximumFractionDigits: 1 }).format(r.margin)}</td>
            <td className="px-4 py-2 text-sm">{r.unconfirmedPeriods > 0 ? `${r.unconfirmedPeriods} dönemin gider kontrolü bekliyor` : `${r.confirmedPeriods} dönem kontrollü`}</td>
          </tr>)}</tbody>
        </table></div>
    )}</div>
    <p className="mt-3 text-xs text-[#6d7175]">Hakediş sıfırken oran hesaplanamaz. İç maliyet tahminleri değil, kontrolü tamamlanmış gerçek giderler kullanılır. Bu kart iç ekip içindir; müşteri portalında gösterilmez.</p>
  </Card>;
}
