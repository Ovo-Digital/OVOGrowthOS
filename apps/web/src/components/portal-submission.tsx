'use client';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card, EmptyState, LoadingState } from '@/components/ui/core';
import { notify } from '@/components/feedback';
import { turkceTarih } from '@/lib/turkish';

type Submission = { id: string; year: number; month: number; grossSales: number | null; refunds: number | null; metaSpend: number | null; googleSpend: number | null; note: string; revision: number; submittedAt: string; submittedBy: string };
type Draft = { year: number; month: number; grossSales: string; refunds: string; metaSpend: string; googleSpend: string; note: string };
const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';
const number = (value: number | null) => value === null ? '—' : new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 }).format(value);
const months = Array.from({ length: 24 }, (_, i) => { const d = new Date(); d.setDate(1); d.setMonth(d.getMonth() - i); return { year: d.getFullYear(), month: d.getMonth() + 1 }; });
const parse = (value: string) => { const t = value.trim(); if (!t) return null; const n = Number(t.replace(/\./g, '').replace(',', '.')); return Number.isFinite(n) && n >= 0 ? n : undefined; };

function Row({ s }: { s: Submission }) {
  return <li className="border-t py-3 first:border-t-0 first:pt-0 text-sm">
    <p className="font-medium">{s.month}/{s.year} <span className="text-[#6d7175]">· Sürüm {s.revision} · {turkceTarih(s.submittedAt)}</span></p>
    <p className="mt-1 text-[#6d7175]">Brüt satış {number(s.grossSales)} · İadeler {number(s.refunds)} · Meta {number(s.metaSpend)} · Google {number(s.googleSpend)}</p>
    {s.note && <p className="mt-1">Not: {s.note}</p>}
  </li>;
}

export function PortalSubmission() {
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['portal-submissions'], queryFn: () => api<Submission[]>('/api/portal/submissions') });
  const [draft, setDraft] = useState<Draft>({ year: months[0].year, month: months[0].month, grossSales: '', refunds: '', metaSpend: '', googleSpend: '', note: '' });
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    const grossSales = parse(draft.grossSales); const refunds = parse(draft.refunds);
    const metaSpend = parse(draft.metaSpend); const googleSpend = parse(draft.googleSpend);
    if (grossSales === undefined || refunds === undefined || metaSpend === undefined || googleSpend === undefined) {
      setError('Tutarlara sıfır veya pozitif bir sayı yazın; boş alan atlanır.'); return;
    }
    setBusy(true); setError('');
    try {
      await api('/api/portal/submissions', { method: 'PUT', body: JSON.stringify({ year: draft.year, month: draft.month, grossSales, refunds, metaSpend, googleSpend, note: draft.note }) });
      notify('Bildiriminiz kaydedildi. OVO ekibi bu değerleri görür; tutarlar hiçbir finansal kayda kendiliğinden yazılmaz.');
      setDraft(d => ({ ...d, grossSales: '', refunds: '', metaSpend: '', googleSpend: '', note: '' }));
      await qc.invalidateQueries({ queryKey: ['portal-submissions'] });
    } catch (e) { setError(e instanceof Error ? e.message : 'Bildirim gönderilemedi.'); } finally { setBusy(false); }
  }
  return <Card className="space-y-3 p-5">
    <h2 className="font-semibold">Dönem bilgisi bildirin</h2>
    <p className="text-sm">Bu ay için brüt satış, iade ve reklam harcamalarınızı bildirin. Değerler OVO ekibine bir bilgi olarak iletilir; hakedişi, aylık sonucu veya faturayı kendiliğinden değiştirmez. Ekip bu bilgiyi gerektiğinde kendi formunda ön doldurma olarak kullanır ve bunu ayrıca onaylar. Aynı dönemi yeniden gönderirseniz son bildiriminiz geçerlidir.</p>
    <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4" onSubmit={save}>
      <label className="block text-sm">Dönem ayı<select className="input mt-1" value={`${draft.year}-${draft.month}`} disabled={busy} onChange={e => { const [y, m] = e.target.value.split('-').map(Number); setDraft(d => ({ ...d, year: y, month: m })); }}>{months.map(m => <option key={`${m.year}-${m.month}`} value={`${m.year}-${m.month}`}>{m.month}/{m.year}</option>)}</select></label>
      <label className="block text-sm">Brüt satış<input className="input mt-1" inputMode="decimal" value={draft.grossSales} disabled={busy} onChange={e => setDraft(d => ({ ...d, grossSales: e.target.value }))} placeholder="125000" /></label>
      <label className="block text-sm">İadeler<input className="input mt-1" inputMode="decimal" value={draft.refunds} disabled={busy} onChange={e => setDraft(d => ({ ...d, refunds: e.target.value }))} placeholder="3500" /></label>
      <label className="block text-sm">Meta harcaması<input className="input mt-1" inputMode="decimal" value={draft.metaSpend} disabled={busy} onChange={e => setDraft(d => ({ ...d, metaSpend: e.target.value }))} placeholder="20000" /></label>
      <label className="block text-sm">Google harcaması<input className="input mt-1" inputMode="decimal" value={draft.googleSpend} disabled={busy} onChange={e => setDraft(d => ({ ...d, googleSpend: e.target.value }))} placeholder="8000" /></label>
      <label className="block text-sm sm:col-span-2 lg:col-span-3">Not<textarea className="input mt-1" rows={2} maxLength={1000} value={draft.note} disabled={busy} onChange={e => setDraft(d => ({ ...d, note: e.target.value }))} placeholder="Örneğin: Kampanya dönemi olduğu için iadeler yükseldi." /></label>
      <div className="flex items-end"><button className={button} disabled={busy}>{busy ? 'Gönderiliyor…' : 'Bildirimi gönder'}</button></div>
      {error && <p role="alert" className="text-sm text-red-700 sm:col-span-2 lg:col-span-4">{error}</p>}
    </form>
    <h3 className="font-semibold">Gönderdiğiniz bildirimler</h3>
    {list.isPending && <LoadingState label="Bildirimler yükleniyor…" />}
    {list.isError && <p role="alert" className="text-sm">{list.error.message}</p>}
    {list.isSuccess && !list.data.length && <EmptyState message="Henüz bildirim göndermediniz. Dönem kapanırken sorulursa buradan iletebilirsiniz." />}
    {list.isSuccess && !!list.data.length && <ul>{list.data.map(s => <Row key={s.id} s={s} />)}</ul>}
  </Card>;
}

export function PortalSubmissionList({ brandId }: { brandId: string }) {
  const list = useQuery({ queryKey: ['portal-submissions', brandId], queryFn: () => api<Submission[]>(`/api/portal-management/brands/${brandId}/submissions`) });
  return <Card className="space-y-3 p-5">
    <h2 className="font-semibold">Müşteri bildirimleri</h2>
    <p className="text-sm">Marka yetkililerinin bildirdiği dönem değerleridir. Bunlar yalnız bilgidir; aylık sonuç, hakediş veya fatura üzerinde kendiliğinden değişiklik yapmaz. Aylık sonuç ekranındaki dönemle eşleşen bildirimi, gerekçeli onayınızla forma ön doldurabilirsiniz.</p>
    {list.isPending && <LoadingState label="Bildirimler yükleniyor…" />}
    {list.isError && <p role="alert" className="text-sm">{list.error.message}</p>}
    {list.isSuccess && !list.data.length && <EmptyState message="Henüz müşteri bildirimi yok." />}
    {list.isSuccess && !!list.data.length && <ul>{list.data.map(s => <Row key={s.id} s={s} />)}</ul>}
  </Card>;
}
