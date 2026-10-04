'use client';
import { useQuery } from '@tanstack/react-query';
import { api, money, type SessionUser } from '@/lib/api';
import { Badge, Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';

type Point = { year: number; month: number; adSpend: number; netRevenue: number; mer: number | null };
type Efficiency = { brandId: string; brandName: string; bandCode: string; bandLabel: string; bandDetail: string; latestMer: number | null; breakEvenMer: number | null; periodLabel: string; trend: Point[]; note: string };

const tone: Record<string, 'green' | 'yellow' | 'red' | 'neutral'> = { strong: 'green', watch: 'yellow', risk: 'red', unknown: 'neutral' };
const mer = (value: number | null) => value === null ? 'Hesaplanamıyor' : `${value.toFixed(2).replace('.', ',')}x`;

export function AdEfficiencyCard({ brandId, currency }: { brandId: string; currency: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const internal = ['Admin', 'Partner', 'Analyst'].includes(me.data?.role ?? '');
  const query = useQuery({ queryKey: ['ad-efficiency', brandId], queryFn: () => api<Efficiency>(`/api/brands/${brandId}/ad-efficiency`), enabled: internal, refetchOnWindowFocus: false });
  if (me.isPending || !internal) return null;
  if (query.isPending) return <Card className="mt-5 p-5"><LoadingState label="Reklam verimliliği hesaplanıyor…" /></Card>;
  if (query.isError) return <Card className="mt-5 p-5"><ErrorState message={`Reklam verimliliği alınamadı. ${query.error.message}`} /></Card>;
  const e = query.data;
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="font-semibold">Reklam verimliliği (MER)</h2>
      <Badge tone={tone[e.bandCode] ?? 'neutral'}>{e.bandLabel}</Badge>
    </div>
    <div className="mt-3 flex flex-wrap items-baseline gap-3">
      <span className="text-3xl font-bold tracking-tight">{mer(e.latestMer)}</span>
      <span className="text-sm text-[#6d7175]">{e.periodLabel} dönemi · başa baş hedefi {mer(e.breakEvenMer)}</span>
    </div>
    <p className="mt-1 text-sm text-[#6d7175]">{e.bandDetail}</p>
    {e.trend.length === 0
      ? <EmptyState message="Kilitlenmiş bir dönem verisi yok; MER yalnız kilitlenen aylardan hesaplanır." />
      : <div className="mt-4 overflow-x-auto rounded-lg border">
          <table className="w-full min-w-[520px] text-sm">
            <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">Dönem</th><th className="px-4 py-2">Reklam harcaması</th><th className="px-4 py-2">Net ciro</th><th className="px-4 py-2">MER</th></tr></thead>
            <tbody>{e.trend.map(p => <tr key={`${p.year}-${p.month}`} className="border-t">
              <td className="px-4 py-2 font-semibold">{String(p.month).padStart(2, '0')}/{p.year}</td>
              <td className="px-4 py-2">{money(p.adSpend, currency)}</td>
              <td className="px-4 py-2">{money(p.netRevenue, currency)}</td>
              <td className="px-4 py-2 font-semibold">{mer(p.mer)}</td>
            </tr>)}</tbody>
          </table>
        </div>}
    <p className="mt-3 text-xs text-[#6d7175]">{e.note}</p>
  </Card>;
}
