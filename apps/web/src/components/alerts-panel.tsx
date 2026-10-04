'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Badge, Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';

type AlertItem = { code: string; severity: 'critical' | 'warning' | 'info'; title: string; detail: string; brandId: string | null; brandName: string | null; link: string };
type Alerts = { today: string; periodLabel: string; summary: { critical: number; warning: number; info: number; total: number }; items: AlertItem[]; hidden: number; notes: string[] };

const severity: Record<string, { label: string; tone: 'red' | 'yellow' | 'blue' }> = {
  critical: { label: 'Kritik', tone: 'red' },
  warning: { label: 'Uyarı', tone: 'yellow' },
  info: { label: 'Bilgi', tone: 'blue' }
};

export function AlertsPanel() {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const internal = ['Admin', 'Partner', 'Analyst'].includes(me.data?.role ?? '');
  const query = useQuery({ queryKey: ['alerts'], queryFn: () => api<Alerts>('/api/alerts'), enabled: internal, refetchInterval: 5 * 60 * 1000 });
  const [open, setOpen] = useState(false);
  if (me.isPending || !internal) return null;
  if (query.isPending) return <Card className="mb-4 p-5"><LoadingState label="Uyarılar kontrol ediliyor…" /></Card>;
  if (query.isError) return <Card className="mb-4 p-5"><ErrorState message={`Uyarılar alınamadı. ${query.error.message}`} /></Card>;
  const a = query.data;
  const shown = open ? a.items : a.items.slice(0, 8);
  return <Card className="mb-4 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="font-semibold">Dikkat gerekenler</h2>
      <div className="flex flex-wrap gap-2">
        {a.summary.total === 0 && <Badge tone="green">Bekleyen uyarı yok</Badge>}
        {a.summary.critical > 0 && <Badge tone="red">{a.summary.critical} kritik</Badge>}
        {a.summary.warning > 0 && <Badge tone="yellow">{a.summary.warning} uyarı</Badge>}
        {a.summary.info > 0 && <Badge tone="blue">{a.summary.info} bilgi</Badge>}
      </div>
    </div>
    <p className="mt-1 text-xs text-[#6d7175]">{a.periodLabel} dönemi ve güncel kayıtlar üzerinden otomatik kontrol edilir · {a.today}</p>
    {a.summary.total === 0
      ? <EmptyState message="Şu an bekleyen bir uyarı görünmüyor. Eksik kayıt, geciken alacak veya hedefin altındaki reklam verimliliği olsaydı burada listelenirdi." />
      : <ul className="mt-3 space-y-2">{shown.map((item, index) => <li key={`${item.code}-${index}`} className="rounded-lg border p-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={severity[item.severity]?.tone ?? 'neutral'}>{severity[item.severity]?.label ?? 'Bilgi'}</Badge>
            <span className="text-sm font-semibold">{item.title}</span>
            <Link href={item.link} className="ml-auto text-xs font-semibold underline">Aç</Link>
          </div>
          <p className="mt-1 text-sm text-[#6d7175]">{item.detail}</p>
        </li>)}</ul>}
    {a.items.length > 8 && <button type="button" onClick={() => setOpen(x => !x)} className="mt-3 text-xs font-semibold underline">
      {open ? 'Daha az göster' : `Diğer ${a.items.length - 8} uyarıyı göster`}
    </button>}
    {a.hidden > 0 && <p className="mt-2 text-xs text-[#6d7175]">{a.hidden} uyarı daha var; daraltmak için ilgili sayfaları kontrol edin.</p>}
    {a.notes.length > 0 && <p className="mt-3 text-xs text-[#6d7175]">{a.notes[0]}</p>}
  </Card>;
}
