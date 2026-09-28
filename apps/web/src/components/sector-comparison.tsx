'use client';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, percent } from '@/lib/api';
import { Badge, Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';

type Row = { industry: string; brandCount: number; grossMargin: number | null; returnShare: number | null; targetAchievement: number | null };
type Payload = { year: number; month: number; period: string; label: string; items: Row[] };

export function SectorComparisonCard() {
  const now = new Date();
  const [period, setPeriod] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`);
  const query = useQuery({ queryKey: ['sector-comparison', period], queryFn: () => { const [y, m] = period.split('-').map(Number); return api<Payload>(`/api/reports/sector-comparison?year=${y}&month=${m}`); }, enabled: period.length === 7, refetchOnWindowFocus: false });
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="font-semibold">Sektör karşılaştırması</h2>
      <label className="text-sm">Dönem
        <input className="input ml-2 mt-0" type="month" value={period} onChange={e => setPeriod(e.target.value)} />
      </label>
    </div>
    <p className="mt-2 text-sm text-[#6d7175]">Seçili ayda onaylı veya kilitli dönemi olan markaları sektörlerine göre karşılaştırır. Yalnız bilgi verir; hiçbir markanın sonucunu değiştirmez.</p>
    {period.length !== 7 ? <p role="status" className="mt-3 text-sm text-[#6d7175]">Karşılaştırmayı görmek için geçerli bir ay seçin.</p>
      : query.isPending ? <LoadingState label="Karşılaştırma hazırlanıyor…" /> : query.isError
      ? <div className="mt-3"><ErrorState message={`Karşılaştırma alınamadı. ${query.error.message}`} /><button className="mt-2 rounded-lg border px-4 py-2 text-sm font-semibold" onClick={() => void query.refetch()}>Yeniden dene</button></div>
      : !query.data.items.length ? <div className="mt-3"><EmptyState message={`${query.data.label} dönemi için onaylı veya kilitli sonuç kaydı bulunmuyor; sektör karşılaştırması bu ay için boş.`} /></div>
      : <div className="mt-3 overflow-x-auto rounded-lg border">
        <table className="w-full min-w-[640px] text-sm">
          <caption className="sr-only">{query.data.label} dönemi sektör karşılaştırması</caption>
          <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr>
            <th className="px-4 py-2">Sektör</th><th className="px-4 py-2">Marka sayısı</th><th className="px-4 py-2">Brüt kâr marjı</th><th className="px-4 py-2">İade oranı</th><th className="px-4 py-2">Hedef gerçekleşme</th>
          </tr></thead>
          <tbody>{query.data.items.map(row => <tr key={row.industry} className="border-t">
            <td className="px-4 py-2 font-medium">{row.industry}{row.industry === 'Belirtilmemiş' && <Badge tone="gray"> sektör girilmemiş</Badge>}</td>
            <td className="px-4 py-2">{row.brandCount}</td>
            <td className="px-4 py-2">{row.grossMargin === null ? '—' : percent(row.grossMargin)}</td>
            <td className="px-4 py-2">{row.returnShare === null ? '—' : percent(row.returnShare)}</td>
            <td className="px-4 py-2">{row.targetAchievement === null ? '—' : percent(row.targetAchievement)}</td>
          </tr>)}</tbody>
        </table>
      </div>}
    <p className="mt-3 text-xs text-[#6d7175]">Oranlar yalnız aynı para birimi ölçümleriyle hesaplanır; hedefi veya hedef para birimi eşleşmeyen markalarda hedef gerçekleşme boş görünür.</p>
  </Card>;
}
