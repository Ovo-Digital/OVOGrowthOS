'use client';
import { useQuery } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Badge, Card, ErrorState, LoadingState } from '@/components/ui/core';

type Factor = { code: string; label: string; effect: number; detail: string };
type Health = { brandId: string; brandName: string; score: number; band: string; factors: Factor[]; qualityPeriod: string; targetPeriod: string | null };
const tone: Record<string, 'green' | 'yellow' | 'red'> = { 'Güçlü': 'green', 'İzlenmeli': 'yellow', 'Riskli': 'red', 'Kritik': 'red' };

export function BrandHealthCard({ brandId }: { brandId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const internal = ['Admin', 'Partner', 'Analyst'].includes(me.data?.role ?? '');
  const query = useQuery({ queryKey: ['brand-health', brandId], queryFn: () => api<Health>(`/api/brands/${brandId}/health`), enabled: internal, refetchOnWindowFocus: false });
  if (me.isPending || !internal) return null;
  if (query.isPending) return <Card className="mt-5 p-5"><LoadingState label="Sağlık skoru hesaplanıyor…" /></Card>;
  if (query.isError) return <Card className="mt-5 p-5"><ErrorState message={`Sağlık skoru alınamadı. ${query.error.message}`} /></Card>;
  const h = query.data;
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="font-semibold">Marka sağlık skoru</h2>
      <Badge tone={tone[h.band] ?? 'neutral'}>{h.band}</Badge>
    </div>
    <div className="mt-3 flex flex-wrap items-baseline gap-3">
      <span className="text-3xl font-bold tracking-tight">{h.score}</span>
      <span className="text-sm text-[#6d7175]">100 üzerinden · Güçlü 85 ve üzeri, İzlenmeli 70–84, Riskli 50–69, Kritik 50 altı</span>
    </div>
    <p className="mt-1 text-xs text-[#6d7175]">Veri kalitesi dönemi: {h.qualityPeriod}{h.targetPeriod ? ` · Hedef dönemi: ${h.targetPeriod}` : ' · Bu marka için hedef kaydı yok'}</p>
    <div className="mt-4 overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[560px] text-sm">
        <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">Etki</th><th className="px-4 py-2">Kalem</th><th className="px-4 py-2">Açıklama</th></tr></thead>
        <tbody>{h.factors.map(f => <tr key={f.code} className="border-t">
          <td className={`px-4 py-2 font-semibold ${f.effect < 0 ? 'text-[#d72c0d]' : 'text-[#008060]'}`}>{f.effect > 0 ? '+' : ''}{f.effect} puan</td>
          <td className="px-4 py-2">{f.label}</td>
          <td className="px-4 py-2 text-[#6d7175]">{f.detail}</td>
        </tr>)}</tbody>
      </table>
    </div>
    <p className="mt-3 text-xs text-[#6d7175]">Skor; veri kalitesi, hedef sapması, tahsilat gecikmesi ve anlaşma durumundan hesaplanır. Yalnız bilgi verir, hakediş veya dönem kararını etkilemez.</p>
  </Card>;
}
