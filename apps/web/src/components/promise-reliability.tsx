'use client';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';

type Period = { year: number; month: number; promisedOn: string; promisedAmount: number; remaining: number; outcome: string; overdueDays: number };
type Row = { brandId: string; brandName: string; kept: number; broken: number; waiting: number; score: number | null; avgOverdueDays: number | null; periods: Period[] };
type Report = { currencies: string[]; currency: string; rows: Row[] };

export function PromiseReliabilityCard({ currency }: { currency: string }) {
  const query = useQuery({ queryKey: ['promise-reliability', currency], queryFn: () => api<Report>(`/api/promise-reliability?currency=${encodeURIComponent(currency)}`), refetchOnWindowFocus: false });
  return <Card className="mb-4 p-5"><h2 className="font-semibold">Hangi marka sözünü tutuyor?</h2>
    <p className="my-3 text-sm">Kayıtlı ödeme sözleri kapanmış tutarla karşılaştırılır: kalanı kalmayan söz tutmuş, vadesi geçip kalanı olan söz tutmamış sayılır. Bekleyen sözler ve kaldırılan kayıtlar skora girmez. Skor tahmin veya garanti değildir.</p>
    {query.isPending ? <LoadingState label="Söz disiplini hesaplanıyor…" /> : query.isError ? <ErrorState message={query.error.message} /> :
      query.data.rows.length === 0 ? <EmptyState message="Bu para biriminde kayıtlı ödeme sözü yok." /> :
        <div className="overflow-x-auto rounded-lg border"><table className="w-full min-w-[640px] text-sm">
          <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">Marka</th><th className="px-4 py-2 text-right">Söz skoru</th><th className="px-4 py-2 text-right">Tutan</th><th className="px-4 py-2 text-right">Geciken</th><th className="px-4 py-2 text-right">Bekleyen</th><th className="px-4 py-2 text-right">Ortalama gecikme</th></tr></thead>
          <tbody>{query.data.rows.map(r => <tr key={r.brandId} className="border-t">
            <td className="px-4 py-2 font-medium">{r.brandName}</td>
            <td className="px-4 py-2 text-right font-semibold">{r.score === null ? 'Hesaplanamıyor' : new Intl.NumberFormat('tr-TR', { style: 'percent', maximumFractionDigits: 0 }).format(r.score)}</td>
            <td className="px-4 py-2 text-right">{r.kept}</td>
            <td className="px-4 py-2 text-right">{r.broken}</td>
            <td className="px-4 py-2 text-right">{r.waiting}</td>
            <td className="px-4 py-2 text-right">{r.avgOverdueDays === null ? '—' : `${r.avgOverdueDays} gün`}</td>
          </tr>)}</tbody>
        </table></div>}
    <p className="mt-3 text-xs text-[#6d7175]">Yenileme görüşmesinde söz disiplinini bu tabloyla anlatın; tek başına yenileme kararı vermeyin.</p>
  </Card>;
}
