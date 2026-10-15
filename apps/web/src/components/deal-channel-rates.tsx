'use client';
import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, money, percent, type SessionUser } from '@/lib/api';
import { notify } from '@/components/feedback';
import { Card } from '@/components/ui/core';
import type { SalesChannel } from '@/components/sales-channels';
import { FieldHint } from '@/components/field-hint';

type ChannelRate = { id: string; dealId: string; salesChannelId: string; channelName: string; revenueShareRate: number; baselineRevenue: number; totalRevenueShare: boolean; updatedAt: string };
const flatModels = ['FlatRevenueShare', 'RetainerPlusRevenueShare', 'MinimumFeePlusRevenueShare'];
const incrementalModels = ['IncrementalRevenueShare', 'RetainerPlusIncrementalRevenueShare'];

export function DealChannelRatesCard({ dealId, brandId, dealType, status, defaultRate, defaultIncrementalRate = 0 }: { dealId: string; brandId: string; dealType: string; status: string; defaultRate: number; defaultIncrementalRate?: number }) {
  const qc = useQueryClient();
  const [busy, setBusy] = useState(false);
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const channels = useQuery({ queryKey: ['sales-channels', brandId], queryFn: () => api<SalesChannel[]>(`/api/brands/${brandId}/sales-channels`) });
  const rates = useQuery({ queryKey: ['deal-channel-rates', dealId], queryFn: () => api<ChannelRate[]>(`/api/deals/${dealId}/channel-rates`) });
  const allowed = (me.data?.role === 'Admin' || me.data?.role === 'Partner') && !['Expired', 'Terminated', 'Rejected'].includes(status);
  const incremental = incrementalModels.includes(dealType);
  const generalIncremental = defaultIncrementalRate;
  const supported = flatModels.includes(dealType) || incremental;
  const active = (channels.data ?? []).filter(c => c.isActive);
  const byChannel = new Map((rates.data ?? []).map(r => [r.salesChannelId, r]));
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (busy) return;
    const f = new FormData(e.currentTarget);
    const list: { salesChannelId: string; revenueShareRate: number; baselineRevenue: number; totalRevenueShare: boolean }[] = [];
    try {
      for (const c of active) {
        const rawRate = String(f.get(`rate-${c.id}`) ?? '').trim().replace('%', '').replace(',', '.');
        const rawBase = String(f.get(`baseline-${c.id}`) ?? '').trim().replaceAll('.', '').replace(',', '.');
        const totalShare = incremental && f.get(`total-${c.id}`) === 'on';
        if (!rawRate && !rawBase && !totalShare) continue;
        const value = rawRate ? Number(rawRate) : generalIncremental * 100;
        if (!Number.isFinite(value) || value < 0 || value > 100) throw new Error(`"${c.name}" için %0–%100 arasında bir oran yazın.`);
        const base = rawBase ? Number(rawBase) : 0;
        if (!Number.isFinite(base) || base < 0) throw new Error(`"${c.name}" için baz ciro negatif olamaz.`);
        list.push({ salesChannelId: c.id, revenueShareRate: Number((value / 100).toFixed(4)), baselineRevenue: base, totalRevenueShare: totalShare });
      }
      setBusy(true);
      await api(`/api/deals/${dealId}/channel-rates`, { method: 'PUT', body: JSON.stringify({ rates: list }) });
      notify('Kanal oranları kaydedildi.'); await qc.invalidateQueries({ queryKey: ['deal-channel-rates', dealId] });
    } catch (err) { notify(err instanceof Error ? err.message : 'Kanal oranları kaydedilemedi.'); }
    finally { setBusy(false); }
  }
  return <Card className="p-5"><h2 className="font-semibold">Kanal bazında gelir payı</h2>
    {!supported && <p className="mt-2 text-sm">Bu anlaşma modelinde kanal başına farklı oran kullanılmaz; oranlar yalnızca sabit gelir payı, asgari ücret ve büyüme payı içeren modellerde geçerlidir. Ciro yine de kanal kırılımlı girilebilir, hakediş toplam üzerinden hesaplanır.</p>}
    {supported && <>
      <p className="mt-2 text-sm">{incremental
        ? 'Her kanalın bazı aşan büyümesi kanalın payıyla hesaplanır. "Bazı aşınca toplamdan al" işaretli kanalda baz aşılınca pay büyümeden değil toplam cirodan alınır; baz aşılamazsa kanal hakediş üretmez. Boş bırakılan kanal anlaşmanın genel büyüme payıyla, bazı yazılmayan kanal baz 0 ile hesaplanır.'
        : `Boş bırakılan kanal anlaşmanın genel oranıyla (${percent(defaultRate)}) hesaplanır.`} Oran değişikliği yalnız ileride hesaplanacak dönemleri etkiler; kapanmış dönemler değişmez.</p>
      {channels.isPending || rates.isPending ? <p role="status" className="mt-3 text-sm">Oranlar yükleniyor…</p> :
        active.length === 0 ? <p className="mt-3 text-sm">Bu markada henüz etkin kanal yok. Önce <Link className="underline" href={`/brands/${brandId}`}>marka sayfasından</Link> satış kanalı ekleyin.</p> :
          <form onSubmit={save} className="mt-3 space-y-2">{active.map(c => {
            const current = byChannel.get(c.id);
            return <div key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
              <span><strong>{c.name}</strong> <span className="text-[#6d7175]">· Geçerli: {incremental
                ? `${current?.baselineRevenue ? `${money(current.baselineRevenue)} üstü ` : ''}%${((current?.revenueShareRate ?? generalIncremental) * 100).toLocaleString('tr-TR', { maximumFractionDigits: 2 })}${current?.totalRevenueShare ? ' (toplamdan)' : ''}${current === undefined ? ' (genel pay)' : ''}`
                : `${percent(current?.revenueShareRate ?? defaultRate)}${current === undefined ? ' (genel oran)' : ''}`}</span></span>
              {incremental ? <span className="flex flex-wrap items-end gap-2">
                <label>Baz ciro<FieldHint text="Pay alınmayacak mevcut ciro. Boş bırakılırsa baz 0 alınır." /><input name={`baseline-${c.id}`} className="input ml-2 w-32" inputMode="decimal" autoComplete="off" disabled={!allowed} defaultValue={current?.baselineRevenue ? String(current.baselineRevenue) : ''} placeholder="Örn. 7000000" /></label>
                <label>Büyüme payı (%)<FieldHint text="Bazı aşan büyümeden alınacak pay. Boş bırakılırsa anlaşmanın genel büyüme payı kullanılır." /><input name={`rate-${c.id}`} className="input ml-2 w-24" inputMode="decimal" autoComplete="off" disabled={!allowed} defaultValue={current ? String(current.revenueShareRate * 100).replace('.', ',') : ''} /></label>
                <label className="flex items-center gap-1.5 text-xs">Bazı aşınca toplamdan al<FieldHint text="İşaretli kanalda baz aşılınca pay büyümeden değil toplam cirodan alınır." /><input name={`total-${c.id}`} type="checkbox" disabled={!allowed} defaultChecked={current?.totalRevenueShare === true} /></label>
              </span> :
              <label>Oran (%)<FieldHint text="Bu kanalın cirosundan alınacak pay. Boş bırakılırsa anlaşmanın genel oranı kullanılır." /><input name={`rate-${c.id}`} className="input ml-2 w-28" inputMode="decimal" autoComplete="off" disabled={!allowed} defaultValue={current === undefined ? '' : String(current.revenueShareRate * 100).replace('.', ',')} placeholder={String(defaultRate * 100).replace('.', ',')} /></label>}
            </div>;
          })}
          {allowed && <button disabled={busy} className="rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50">{busy ? 'Kaydediliyor…' : 'Oranları kaydet'}</button>}
          {!allowed && <p className="text-sm">Oranlar kapanmış anlaşmalarda değiştirilemez.</p>}</form>}
    </>}
  </Card>;
}
