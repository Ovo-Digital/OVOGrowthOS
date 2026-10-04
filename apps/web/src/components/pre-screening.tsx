'use client';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Badge, Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';

type Issue = { code: string; label: string };
type Item = { brandId: string; brandName: string; state: string; issueCount: number; issues: Issue[]; stageDays: number | null };
type Report = { total: number; ready: number; attention: number; items: Item[]; notes: string[] };

export function PreScreening() {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const internal = ['Admin', 'Partner', 'Analyst'].includes(me.data?.role ?? '');
  const query = useQuery({ queryKey: ['pre-screening'], queryFn: () => api<Report>('/api/leads/pre-screening'), enabled: internal, refetchOnWindowFocus: false });
  if (me.isPending || !internal) return null;
  if (query.isPending) return <Card className="mt-4 p-5"><LoadingState label="Ön eleme hazırlanıyor…" /></Card>;
  if (query.isError) return <Card className="mt-4 p-5"><ErrorState message={`Ön eleme alınamadı. ${query.error.message}`} /></Card>;
  const r = query.data;
  return <Card className="mt-4 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 className="font-semibold">Hızlı ön eleme</h2>
        <p className="mt-1 text-xs text-[#6d7175]">Açık adayların görüşme öncesi hangi bilgilerinin eksik olduğunu ve nerede takıldığını tek listede gösterir.</p>
      </div>
      <div className="flex flex-wrap gap-2">
        <Badge tone={r.attention > 0 ? 'yellow' : 'green'}>{r.attention} eksik bilgi</Badge>
        <Badge tone="green">{r.ready} hazır</Badge>
      </div>
    </div>
    {r.total === 0 ? <EmptyState message="Bu aşamalarda açık aday bulunmuyor." />
      : <ul className="mt-4 space-y-2">{r.items.map(item => <li key={item.brandId} className="rounded-lg border p-3">
          <div className="flex flex-wrap items-center gap-2">
            <Link href={`/brands/${item.brandId}`} className="text-sm font-semibold underline">{item.brandName}</Link>
            <Badge tone={item.state === 'ready' ? 'green' : 'yellow'}>{item.state === 'ready' ? 'Hazır' : 'Eksik bilgi var'}</Badge>
            {item.stageDays !== null && item.stageDays > 30 && <Badge tone="red">{item.stageDays} gündür bekliyor</Badge>}
          </div>
          {item.issues.length > 0 && <ul className="mt-1.5 list-disc space-y-0.5 pl-5 text-xs text-[#6d7175]">{item.issues.map(issue => <li key={issue.code}>{issue.label}</li>)}</ul>}
        </li>)}</ul>}
    <ul className="mt-4 list-disc space-y-1 pl-5 text-xs text-[#6d7175]">{r.notes.map(note => <li key={note}>{note}</li>)}</ul>
  </Card>;
}
