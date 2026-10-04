'use client';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, percent } from '@/lib/api';
import { Card, ErrorState, LoadingState } from '@/components/ui/core';
import { leadStages } from '@/components/brand-team';

type Breakdown = { key: string; label: string; count: number; share: number };
type FunnelRow = { stage: string; entered: number; converted: number; lost: number; open: number; conversionRate: number | null };
type Analysis = { windowMonths: number; totalLosses: number; byStage: Breakdown[]; bySource: Breakdown[]; byMonth: Breakdown[]; funnel: FunnelRow[]; notes: string[] };

const windows = [3, 6, 12];

export function LossFunnelAnalysis() {
  const [months, setMonths] = useState(6);
  const query = useQuery({ queryKey: ['loss-analysis', months], queryFn: () => api<Analysis>(`/api/pipeline/loss-analysis?months=${months}`), refetchOnWindowFocus: false });
  return <Card className="mt-4 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 className="font-semibold">Kayıp ve huni analizi</h2>
        <p className="mt-1 text-xs text-[#6d7175]">Kayıpların nerede biriktiğini ve her aşamaya giren markaların ne kadarının anlaşmaya döndüğünü gösterir.</p>
      </div>
      <div className="flex gap-2">{windows.map(value => <button key={value} type="button" onClick={() => setMonths(value)}
        className={`rounded-lg border px-3 py-1.5 text-xs font-semibold ${months === value ? 'bg-[#303030] text-white' : 'bg-white'}`}>Son {value} ay</button>)}</div>
    </div>
    {query.isPending ? <LoadingState label="Analiz hesaplanıyor…" />
      : query.isError ? <ErrorState message={`Analiz alınamadı. ${query.error.message}`} />
      : <>
      <div className="mt-4 overflow-x-auto rounded-lg border">
        <table className="w-full min-w-[640px] text-sm">
          <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr>
            <th className="px-4 py-2">Aşama</th><th className="px-4 py-2">Aşamaya giren</th><th className="px-4 py-2">Anlaşmaya dönen</th>
            <th className="px-4 py-2">Kayıp</th><th className="px-4 py-2">Şu an açık</th><th className="px-4 py-2">Dönüşüm</th>
          </tr></thead>
          <tbody>{query.data.funnel.map(row => <tr key={row.stage} className="border-t">
            <td className="px-4 py-2 font-semibold">{leadStages[row.stage] ?? row.stage}</td>
            <td className="px-4 py-2">{row.entered}</td>
            <td className="px-4 py-2">{row.converted}</td>
            <td className="px-4 py-2">{row.lost}</td>
            <td className="px-4 py-2">{row.open}</td>
            <td className="px-4 py-2 font-semibold">{row.conversionRate === null ? 'Ölçülmedi' : percent(row.conversionRate / 100)}</td>
          </tr>)}</tbody>
        </table>
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <BreakdownList title={`Kayıp nedenine göre (${query.data.totalLosses} kayıp)`} rows={query.data.byStage} empty="Seçili dönemde kayıp kaydı yok." />
        <BreakdownList title="Kaynak kanalına göre" rows={query.data.bySource} empty="Seçili dönemde kayıp kaydı yok." />
        <BreakdownList title="Aylara göre" rows={query.data.byMonth} empty="Seçili dönemde kayıp kaydı yok." highlightZero />
      </div>
      <ul className="mt-4 list-disc space-y-1 pl-5 text-xs text-[#6d7175]">{query.data.notes.map(note => <li key={note}>{note}</li>)}</ul>
    </>}
  </Card>;
}

function BreakdownList({ title, rows, empty, highlightZero }: { title: string; rows: Breakdown[]; empty: string; highlightZero?: boolean }) {
  const visible = highlightZero ? rows : rows.filter(x => x.count > 0);
  return <div>
    <h3 className="text-sm font-semibold">{title}</h3>
    {visible.length === 0 ? <p className="mt-2 text-sm text-[#6d7175]">{empty}</p>
      : <ul className="mt-2 space-y-1 text-sm">{visible.map(row => <li key={row.key} className="flex items-center justify-between gap-2">
          <span className="truncate">{row.label}</span>
          <span className="shrink-0 font-semibold">{row.count} marka{row.count > 0 ? ` · ${percent(row.share / 100)}` : ''}</span>
        </li>)}</ul>}
  </div>;
}
