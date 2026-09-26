'use client';
import { FormEvent, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, money, percent, type SessionUser } from '@/lib/api';
import { notify } from '@/components/feedback';
import { Badge, Card } from '@/components/ui/core';
import { turkce, turkceTarih } from '@/lib/turkish';

const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';
const area = 'input mt-1 w-full';
type Item = { id: string; title: string; description: string; createdBy: string; createdAt: string };
type RemovedItem = { id: string; title: string; description: string; removedAt: string; removedBy: string; removeReason: string };
type ScopeRequest = { id: string; title: string; description: string; status: string; statusLabel: string; requestedBy: string; requestedAt: string; decidedBy: string; decidedAt: string | null; decisionNote: string; scopeItemId: string | null };
type ScopeState = { dealId: string; status: string; editable: boolean; items: Item[]; removed: RemovedItem[]; requests: ScopeRequest[]; counts: { active: number; pending: number; approved: number; rejected: number } };

export function DealScopePanel({ dealId }: { dealId: string }) {
  const cache = useQueryClient();
  const [mode, setMode] = useState<'none' | 'add' | 'request'>('none');
  const [removing, setRemoving] = useState<string | null>(null);
  const [deciding, setDeciding] = useState<{ id: string; decision: 'approved' | 'rejected' } | null>(null);
  const query = useQuery({ queryKey: ['deal-scope', dealId], queryFn: () => api<ScopeState>(`/api/deals/${dealId}/scope`), refetchOnWindowFocus: false });
  const refresh = () => { void cache.invalidateQueries({ queryKey: ['deal-scope', dealId] }); void cache.invalidateQueries({ queryKey: ['renewal-summary', dealId] }); };
  const onDone = (message: string) => { setMode('none'); setRemoving(null); setDeciding(null); notify(message); refresh(); };
  const addItem = useMutation({ mutationFn: (body: object) => api(`/api/deals/${dealId}/scope/items`, { method: 'POST', body: JSON.stringify(body) }), onSuccess: () => onDone('Kapsam kalemi eklendi.') });
  const removeItem = useMutation({ mutationFn: (input: { itemId: string; reason: string }) => api(`/api/deals/${dealId}/scope/items/${input.itemId}/remove`, { method: 'POST', body: JSON.stringify({ reason: input.reason }) }), onSuccess: () => onDone('Kapsam kalemi çıkarıldı. Çıkarılan kalem kaybolmaz, geçmişte kalır.') });
  const addRequest = useMutation({ mutationFn: (body: object) => api(`/api/deals/${dealId}/scope/requests`, { method: 'POST', body: JSON.stringify(body) }), onSuccess: () => onDone('Paket dışı talep gönderildi. Yönetici kararı bekleniyor.') });
  const decide = useMutation({ mutationFn: (input: { requestId: string; decision: string; note: string }) => api(`/api/deals/${dealId}/scope/requests/${input.requestId}/decision`, { method: 'POST', body: JSON.stringify({ decision: input.decision, note: input.note }) }), onSuccess: () => onDone('Talep kararı kaydedildi.') });

  if (query.isPending) return <Card className="mt-4 p-5">Hizmet kapsamı yükleniyor…</Card>;
  if (query.isError) return <Card className="mt-4 p-5"><p role="alert">{query.error.message} <button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></p></Card>;
  const s = query.data;
  return <Card className="mt-4 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">Hizmet kapsamı</h2>
      <div className="flex flex-wrap gap-2"><Badge tone="neutral">{s.counts.active} kapsam kalemi</Badge>{s.counts.pending > 0 && <Badge tone="yellow">{s.counts.pending} bekleyen paket dışı talep</Badge>}</div></div>
    <p className="mt-2 text-sm text-[#6d7175]">Burada anlaşmanın içine giren işler yazılır. Anlaşmanın dışında istenen işler için paket dışı talep açılır; onaylanan talep otomatik olarak kapsam kalemine dönüşür.</p>
    {!s.editable && <p className="mt-3 rounded-lg border p-3 text-sm">Kapsam yalnız anlaşma açıkken değiştirilebilir. Bu anlaşma şu anda <strong>{turkce(s.status)}</strong> durumunda.</p>}
    {s.editable && <div className="mt-4 flex flex-wrap gap-3">
      <button className={button} onClick={() => { setMode(mode === 'add' ? 'none' : 'add'); setRemoving(null); setDeciding(null); }} aria-expanded={mode === 'add'}>Kapsam kalemi ekle</button>
      <button className={button} onClick={() => { setMode(mode === 'request' ? 'none' : 'request'); setRemoving(null); setDeciding(null); }} aria-expanded={mode === 'request'}>Paket dışı talep aç</button>
    </div>}
    {mode === 'add' && <ScopeForm pending={addItem.isPending} submit={(title, description) => addItem.mutate({ id: crypto.randomUUID(), title, description })} close={() => setMode('none')} addLabel="Kapsam kalemini kaydet" />}
    {mode === 'request' && <ScopeForm pending={addRequest.isPending} submit={(title, description) => addRequest.mutate({ id: crypto.randomUUID(), title, description })} close={() => setMode('none')} addLabel="Talebi gönder" hint="Paket dışı talep, bu işin ücretinin ayrıca konuşulması gerektiğini gösterir." />}

    <ul className="mt-4 space-y-3">{s.items.map(item => <li key={item.id} className="rounded-lg border p-3">
      <div className="flex flex-wrap items-start justify-between gap-2"><strong>{item.title}</strong>{s.editable && removing !== item.id && <button className="underline" onClick={() => { setRemoving(item.id); setDeciding(null); }}>Kapsamdan çıkar</button>}</div>
      {item.description && <p className="mt-1 whitespace-pre-wrap break-words text-sm text-[#6d7175]">{item.description}</p>}
      <p className="mt-2 text-xs text-[#6d7175]">Ekleyen: {item.createdBy} · {turkceTarih(item.createdAt)}</p>
      {removing === item.id && <form className="mt-3" onSubmit={e => { e.preventDefault(); const reason = String(new FormData(e.currentTarget).get('reason') ?? '').trim(); if (reason) removeItem.mutate({ itemId: item.id, reason }); }}>
        <label className="block text-sm">Çıkarma nedeni<textarea name="reason" className={area} required minLength={1} maxLength={1000} placeholder="Örneğin: bu iş anlaşmanın dışında kaldı." /></label>
        <div className="mt-2 flex gap-3"><button className={button} disabled={removeItem.isPending}>Kapsamdan çıkar</button><button className={button} type="button" onClick={() => setRemoving(null)}>Vazgeç</button></div>
      </form>}
    </li>)}</ul>
    {s.items.length === 0 && <p className="mt-4 text-sm">Bu anlaşma için henüz kapsam kalemi yazılmadı.</p>}

    <h3 className="mt-5 font-semibold">Paket dışı talepler</h3>
    <ul className="mt-3 space-y-3">{s.requests.map(r => <li key={r.id} className="rounded-lg border p-3">
      <div className="flex flex-wrap items-start justify-between gap-2"><strong>{r.title}</strong><Badge tone={r.status === 'Approved' ? 'green' : r.status === 'Rejected' ? 'red' : 'yellow'}>{r.statusLabel}</Badge></div>
      {r.description && <p className="mt-1 whitespace-pre-wrap break-words text-sm text-[#6d7175]">{r.description}</p>}
      <p className="mt-2 text-xs text-[#6d7175]">İsteyen: {r.requestedBy} · {turkceTarih(r.requestedAt)}{r.decidedAt && <> · Karar: {r.decidedBy} · {turkceTarih(r.decidedAt)}</>}</p>
      {r.decisionNote && <p className="mt-1 text-sm">Karar notu: {r.decisionNote}</p>}
      {s.editable && r.status === 'Pending' && deciding?.id === r.id && <form className="mt-3" onSubmit={e => { e.preventDefault(); const note = String(new FormData(e.currentTarget).get('note') ?? '').trim(); if (note) decide.mutate({ requestId: r.id, decision: deciding.decision, note }); }}>
        <label className="block text-sm">Karar notu<textarea name="note" className={area} required minLength={1} maxLength={1000} placeholder={deciding.decision === 'approved' ? 'Bu iş kapsamın içine giriyor; nasıl karşılanacak?' : 'Bu iş paket dışında; neden karşılanmıyor?'} /></label>
        <div className="mt-2 flex gap-3"><button className={button} disabled={decide.isPending}>{deciding.decision === 'approved' ? 'Talebi onayla' : 'Talebi reddet'}</button><button className={button} type="button" onClick={() => setDeciding(null)}>Vazgeç</button></div>
      </form>}
      {s.editable && r.status === 'Pending' && deciding?.id !== r.id && <div className="mt-2 flex gap-3"><button className="underline" onClick={() => { setDeciding({ id: r.id, decision: 'approved' }); setRemoving(null); }}>Onayla</button><button className="underline" onClick={() => { setDeciding({ id: r.id, decision: 'rejected' }); setRemoving(null); }}>Reddet</button></div>}
    </li>)}</ul>
    {s.requests.length === 0 && <p className="mt-3 text-sm">Paket dışı talep yok.</p>}
    {s.removed.length > 0 && <details className="mt-5"><summary className="cursor-pointer text-sm font-semibold">Çıkarılan kapsam kalemleri ({s.removed.length})</summary>
      <ul className="mt-3 space-y-2 text-sm">{s.removed.map(x => <li key={x.id} className="rounded-lg border p-3"><strong>{x.title}</strong><p className="mt-1 text-[#6d7175]">Neden: {x.removeReason}</p><p className="mt-1 text-xs">Çıkaran: {x.removedBy} · {turkceTarih(x.removedAt)}</p></li>)}</ul></details>}
  </Card>;
}

function ScopeForm({ submit, close, pending, addLabel, hint }: { submit: (title: string, description: string) => void; close: () => void; pending: boolean; addLabel: string; hint?: string }) {
  return <form className="mt-3 rounded-lg border p-3" onSubmit={(e: FormEvent<HTMLFormElement>) => { e.preventDefault(); const f = new FormData(e.currentTarget); submit(String(f.get('title') ?? '').trim(), String(f.get('description') ?? '').trim()); }}>
    {hint && <p className="mb-2 text-sm text-[#6d7175]">{hint}</p>}
    <label className="block text-sm">Başlık<input name="title" className={area} required maxLength={200} /></label>
    <label className="mt-2 block text-sm">Açıklama<textarea name="description" className={area} rows={3} maxLength={2000} placeholder="Bu iş neyi kapsıyor, neleri içermiyor?" /></label>
    <div className="mt-3 flex gap-3"><button className={button} disabled={pending}>{addLabel}</button><button className={button} type="button" onClick={close}>Vazgeç</button></div>
  </form>;
}

type Month = { year: number; month: number; status: string | null; netRevenue: number | null; target: number | null; receivable: number | null; paid: number | null; outstanding: number | null; overdueDays: number };
type RenewalState = {
  deal: { id: string; name: string; status: string; dealType: string; contractMonths: number; startDate: string | null; endDate: string | null; currency: string; monthlyRetainer: number; minimumMonthlyFee: number; revenueShareRate: number; estimatedMonthlyInternalCost: number };
  renewal: { renewalOfDealId: string | null; successorId: string | null; successorName: string | null; successorStatus: string | null; taskId: string | null; taskTitle: string | null; taskDueOn: string | null; taskAssignee: string | null; taskCompleted: boolean };
  scope: { activeItems: number; pendingRequests: number; rejectedRequests: number };
  months: Month[];
  collections: { receivable: number; paid: number; outstanding: number; overdue: number };
  costs: { recorded: number; hours: number; direct: number; team: number; confirmedPeriods: number };
  effort: { taskCount: number; plannedHours: number; actualHours: number; difference: number };
  notes: string[];
};

export function RenewalSummaryPanel({ dealId }: { dealId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const isManager = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const query = useQuery({ queryKey: ['renewal-summary', dealId], queryFn: () => api<RenewalState>(`/api/deals/${dealId}/renewal-summary`), enabled: isManager === true, retry: false, refetchOnWindowFocus: false });
  return <Card className="mt-4 p-5">
    <h2 className="font-semibold">Yenileme toplantısı özeti</h2>
    <p className="mt-2 text-sm text-[#6d7175]">Anlaşmanın bitişini konuşmadan önce bakılacak tek ekrandır. Bu özet yalnızca bilgi verir; hiçbir anlaşmayı yenilemez ve hiçbir ücreti değiştirmez.</p>
    {!isManager && me.data && <p className="mt-3 rounded-lg border p-3 text-sm">Yenileme özeti yalnız yönetici ve iş ortağı hesaplarında açılır. Görüntülemek için bu iki rolden biriyle giriş yapın.</p>}
    {isManager && query.isPending && <p className="mt-3" role="status">Özet hazırlanıyor…</p>}
    {isManager && query.isError && <p className="mt-3" role="alert">{query.error.message} <button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></p>}
    {isManager && query.data && <>
      <div className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
        <Summary l="Anlaşma" v={query.data.deal.name} />
        <Summary l="Durum" v={turkce(query.data.deal.status)} />
        <Summary l="Süre" v={`${query.data.deal.contractMonths} ay`} />
        <Summary l="Başlangıç" v={query.data.deal.startDate ?? '—'} />
        <Summary l="Bitiş" v={query.data.deal.endDate ?? '—'} />
        <Summary l="Aylık sabit ücret" v={money(query.data.deal.monthlyRetainer, query.data.deal.currency)} />
        <Summary l="Aylık güvence" v={money(query.data.deal.minimumMonthlyFee, query.data.deal.currency)} />
        <Summary l="Gelir payı" v={percent(query.data.deal.revenueShareRate)} />
        <Summary l="Tahmini iç maliyet" v={money(query.data.deal.estimatedMonthlyInternalCost, query.data.deal.currency)} />
      </div>
      <div className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
        <Summary l="Kapsam kalemi" v={String(query.data.scope.activeItems)} />
        <Summary l="Bekleyen paket dışı talep" v={String(query.data.scope.pendingRequests)} />
        <Summary l="Reddedilen paket dışı talep" v={String(query.data.scope.rejectedRequests)} />
      </div>
      <div className="mt-4 rounded-lg border p-3 text-sm">
        <strong>Yenileme görevi</strong>
        <p className="mt-1">{query.data.renewal.taskId ? <>{query.data.renewal.taskTitle} · Sorumlu: {query.data.renewal.taskAssignee ?? 'Belirlenmedi'} · Son tarih: {query.data.renewal.taskDueOn ?? 'Belirlenmedi'} · {query.data.renewal.taskCompleted ? 'Tamamlandı' : 'Açık'}</> : 'Bu anlaşma için hazırlanmış bir yenileme görevi yok.'}</p>
        <p className="mt-1">{query.data.renewal.successorId ? <>Devam anlaşması: {query.data.renewal.successorName} ({turkce(query.data.renewal.successorStatus)})</> : 'Bu anlaşmadan türetilmiş bir devam anlaşması yok.'}</p>
      </div>
      <h3 className="mt-5 font-semibold">Son aylar</h3>
      <div className="mt-2 overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-left text-[#6d7175]"><th className="py-1 pr-3">Dönem</th><th className="py-1 pr-3">Hedef</th><th className="py-1 pr-3">Net ciro</th><th className="py-1 pr-3">Alacak</th><th className="py-1 pr-3">Tahsil edilen</th><th className="py-1">Gecikme (gün)</th></tr></thead>
        <tbody>{query.data.months.map(m => <tr key={`${m.year}-${m.month}`} className="border-t"><td className="py-1 pr-3">{m.month}/{m.year}</td><td className="py-1 pr-3">{m.target === null ? '—' : money(m.target, query.data.deal.currency)}</td><td className="py-1 pr-3">{m.netRevenue === null ? '—' : money(m.netRevenue, query.data.deal.currency)}</td><td className="py-1 pr-3">{m.receivable === null ? '—' : money(m.receivable, query.data.deal.currency)}</td><td className="py-1 pr-3">{m.paid === null ? '—' : money(m.paid, query.data.deal.currency)}</td><td className="py-1">{m.overdueDays || 0}</td></tr>)}</tbody></table></div>
      <div className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
        <Summary l="Toplam alacak" v={money(query.data.collections.receivable, query.data.deal.currency)} />
        <Summary l="Tahsil edilen" v={money(query.data.collections.paid, query.data.deal.currency)} />
        <Summary l="Kalan alacak" v={money(query.data.collections.outstanding, query.data.deal.currency)} />
        <Summary l="Kayıtlı hizmet maliyeti" v={money(query.data.costs.recorded, query.data.deal.currency)} />
        <Summary l="Maliyete giren saat" v={`${query.data.costs.hours} saat`} />
        <Summary l="Kapanan maliyet dönemi" v={`${query.data.costs.confirmedPeriods}`} />
        <Summary l="Görev sayısı" v={`${query.data.effort.taskCount}`} />
        <Summary l="Planlanan saat" v={`${query.data.effort.plannedHours} saat`} />
        <Summary l="Gerçekleşen saat" v={`${query.data.effort.actualHours} saat`} />
      </div>
      <p className="mt-3 text-sm">Planlanan ve gerçekleşen saat yan yana yazılır; biri diğerinden hesaplanmaz. Aradaki fark {query.data.effort.difference} saattir.</p>
      <ul className="mt-4 list-disc space-y-1 pl-5 text-sm text-[#6d7175]">{query.data.notes.map(n => <li key={n}>{n}</li>)}</ul>
    </>}
  </Card>;
}

function Summary({ l, v }: { l: string; v: string }) { return <div><div className="text-[#6d7175]">{l}</div><div className="font-semibold">{v}</div></div>; }
