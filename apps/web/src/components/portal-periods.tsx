'use client';
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Badge, Card, EmptyState, LoadingState } from '@/components/ui/core';
import { useDialog } from '@/components/ui/modal';
import { notify } from '@/components/feedback';
import { turkceTarih } from '@/lib/turkish';

type Approval = { approved: boolean; reason: string; userEmail: string; createdAt: string };
type PeriodItem = { year: number; month: number; label: string; statusText: string; approval: Approval | null };
const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';

export function PortalPeriods() {
  const qc = useQueryClient();
  const { confirm } = useDialog();
  const [rejecting, setRejecting] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const query = useQuery({ queryKey: ['portal-periods'], queryFn: () => api<{ items: PeriodItem[] }>('/api/portal/periods'), retry: false });
  if (query.isError) return null;
  async function decide(item: PeriodItem, approved: boolean, text?: string) {
    if (busy) return;
    setBusy(true); setError('');
    try {
      const result = await api<{ message: string }>(`/api/portal/periods/${item.year}/${item.month}/decision`, { method: 'POST', body: JSON.stringify({ approved, reason: text ?? null }) });
      notify(result.message); setRejecting(''); setReason('');
      await qc.invalidateQueries({ queryKey: ['portal-periods'] });
    } catch (e) { setError(e instanceof Error ? e.message : 'Dönem kararı kaydedilemedi.'); }
    finally { setBusy(false); }
  }
  if (query.isPending) return <Card className="mt-5 p-5"><LoadingState label="Dönemler yükleniyor…" /></Card>;
  const items = query.data.items;
  return <Card className="mt-5 p-5 print:hidden">
    <h2 className="font-semibold">Dönem onayları</h2>
    <p className="mt-2 text-sm text-[#6d7175]">OVO ekibi tarafından onaylanmış dönemlerde rakamları inceleyip onaylayabilir veya gerekçesiyle reddedebilirsiniz. Onay veya ret yalnız sizin kaydınızı oluşturur; tutarları değiştirmez. Kararınızı istediğiniz zaman değiştirebilirsiniz; karar OVO ekibine bildirilir.</p>
    {error && <p role="alert" className="mt-3 text-sm text-red-700">{error}</p>}
    {!items.length ? <EmptyState message="Henüz onaya açılmış dönem yok. Dönemler ekip onayından geçtiğinde burada görünür." /> :
      <ul className="mt-3">{items.map(item => {
        const key = `${item.year}-${item.month}`;
        return <li key={key} className="border-t py-3 first:border-t-0 first:pt-0">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.label}</span><Badge tone="neutral">{item.statusText}</Badge></div>
            <div className="flex flex-wrap gap-2">
              <button className={button} disabled={busy} onClick={async () => { if (await confirm({ title: 'Dönem onaylansın mı?', message: `${item.label} dönemini onaylıyorsunuz. Onayınız OVO ekibine bildirilir; kararınızı daha sonra değiştirebilirsiniz.` })) await decide(item, true); }}>Onayla</button>
              <button className={button} disabled={busy} onClick={() => { setRejecting(rejecting === key ? '' : key); setReason(''); setError(''); }}>{rejecting === key ? 'Vazgeç' : 'Reddet'}</button>
            </div>
          </div>
          {item.approval && <p className="mt-1 text-sm text-[#6d7175]">
            {item.approval.approved ? <Badge tone="green">Onaylandı</Badge> : <Badge tone="red">Reddedildi</Badge>}
            {' · '}{item.approval.userEmail} · {turkceTarih(item.approval.createdAt)}
            {!item.approval.approved && item.approval.reason && <> · Gerekçe: {item.approval.reason}</>}
          </p>}
          {rejecting === key && <form className="mt-3 max-w-xl space-y-2" onSubmit={e => { e.preventDefault(); void decide(item, false, reason); }}>
            <label className="block text-sm">Red gerekçesi<textarea required maxLength={1000} className="input mt-1" rows={3} value={reason} onChange={e => setReason(e.target.value)} placeholder="Örneğin: İade tutarı raporda farklı görünüyor." disabled={busy} /></label>
            <p className="text-xs text-[#6d7175]">Gerekçe OVO ekibine iletilir; en fazla 1000 karakter yazabilirsiniz.</p>
            <button className={button} disabled={busy || !reason.trim()}>{busy ? 'Kaydediliyor…' : 'Reddi gönder'}</button>
          </form>}
        </li>;
      })}</ul>}
  </Card>;
}
