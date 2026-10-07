'use client';
import Link from 'next/link';
import { FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, percent, type SessionUser } from '@/lib/api';
import { notify } from '@/components/feedback';
import { Card } from '@/components/ui/core';
import type { SalesChannel } from '@/components/sales-channels';
import { FieldHint } from '@/components/field-hint';

type ChannelRate = { id: string; dealId: string; salesChannelId: string; channelName: string; revenueShareRate: number; updatedAt: string };
const supportedModels = ['FlatRevenueShare', 'RetainerPlusRevenueShare', 'MinimumFeePlusRevenueShare'];

export function DealChannelRatesCard({ dealId, brandId, dealType, status, defaultRate }: { dealId: string; brandId: string; dealType: string; status: string; defaultRate: number }) {
  const qc = useQueryClient();
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const channels = useQuery({ queryKey: ['sales-channels', brandId], queryFn: () => api<SalesChannel[]>(`/api/brands/${brandId}/sales-channels`) });
  const rates = useQuery({ queryKey: ['deal-channel-rates', dealId], queryFn: () => api<ChannelRate[]>(`/api/deals/${dealId}/channel-rates`) });
  const allowed = (me.data?.role === 'Admin' || me.data?.role === 'Partner') && !['Expired', 'Terminated', 'Rejected'].includes(status);
  const supported = supportedModels.includes(dealType);
  const active = (channels.data ?? []).filter(c => c.isActive);
  const byChannel = new Map((rates.data ?? []).map(r => [r.salesChannelId, r.revenueShareRate]));
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const list: { salesChannelId: string; revenueShareRate: number }[] = [];
    for (const c of active) {
      const raw = String(f.get(`rate-${c.id}`) ?? '').trim().replace('%', '').replace(',', '.');
      if (!raw) continue;
      const value = Number(raw);
      if (!Number.isFinite(value) || value < 0 || value > 100) throw new Error(`"${c.name}" için %0–%100 arasında bir oran yazın.`);
      list.push({ salesChannelId: c.id, revenueShareRate: Number((value / 100).toFixed(4)) });
    }
    try {
      await api(`/api/deals/${dealId}/channel-rates`, { method: 'PUT', body: JSON.stringify({ rates: list }) });
      notify('Kanal oranları kaydedildi.'); await qc.invalidateQueries({ queryKey: ['deal-channel-rates', dealId] });
    } catch (err) { notify(err instanceof Error ? err.message : 'Kanal oranları kaydedilemedi.'); }
  }
  return <Card className="p-5"><h2 className="font-semibold">Kanal bazında gelir payı</h2>
    {!supported && <p className="mt-2 text-sm">Bu anlaşma modelinde kanal başına farklı oran kullanılmaz; oranlar yalnızca sabit gelir payı içeren modellerde geçerlidir. Ciro yine de kanal kırılımlı girilebilir, hakediş toplam üzerinden hesaplanır.</p>}
    {supported && <>
      <p className="mt-2 text-sm">Boş bırakılan kanal anlaşmanın genel oranıyla ({percent(defaultRate)}) hesaplanır. Oran değişikliği yalnız ileride hesaplanacak dönemleri etkiler; kapanmış dönemler değişmez.</p>
      {channels.isPending || rates.isPending ? <p role="status" className="mt-3 text-sm">Oranlar yükleniyor…</p> :
        active.length === 0 ? <p className="mt-3 text-sm">Bu markada henüz etkin kanal yok. Önce <Link className="underline" href={`/brands/${brandId}`}>marka sayfasından</Link> satış kanalı ekleyin.</p> :
          <form onSubmit={save} className="mt-3 space-y-2">{active.map(c => {
            const current = byChannel.get(c.id);
            return <div key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
              <span><strong>{c.name}</strong> <span className="text-[#6d7175]">· Geçerli oran: {percent(current ?? defaultRate)}{current === undefined ? ' (genel oran)' : ''}</span></span>
              <label>Oran (%)<FieldHint text="Bu kanalın cirosundan alınacak pay. Boş bırakılırsa anlaşmanın genel oranı kullanılır." /><input name={`rate-${c.id}`} className="input ml-2 w-28" inputMode="decimal" autoComplete="off" disabled={!allowed} defaultValue={current === undefined ? '' : String(current * 100).replace('.', ',')} placeholder={String(defaultRate * 100).replace('.', ',')} /></label>
            </div>;
          })}
          {allowed && <button className="rounded-lg border px-4 py-2 text-sm font-semibold">Oranları kaydet</button>}
          {!allowed && <p className="text-sm">Oranlar kapanmış anlaşmalarda değiştirilemez.</p>}</form>}
    </>}
  </Card>;
}
