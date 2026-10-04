'use client';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise } from '@/lib/api';
import { Card, ErrorState, LoadingState } from '@/components/ui/core';
import { dayText } from '@/components/work-tasks';

type Week = { from: string; through: string; due: number; dueCount: number; promised: number; promiseCount: number };
type Projection = {
  today: string; horizon: string; currency: string; weeks: number; closedPeriods: number;
  rows: Week[]; dueTotal: number; promisedTotal: number; overdue: number; unknownDue: number;
  beyondHorizon: number; overduePromises: number; beyondHorizonPromises: number; notes: string[];
};

export function CashProjectionCard({ currency }: { currency?: string }) {
  const query = useQuery({
    queryKey: ['cash-projection', currency ?? ''],
    queryFn: () => api<{ currencies: string[]; projection: Projection }>(`/api/cash-projection${currency ? `?currency=${currency}` : ''}`),
    refetchOnWindowFocus: false
  });
  if (query.isPending) return <Card className="mb-4 p-5"><LoadingState label="Nakit girişi hesaplanıyor…" /></Card>;
  if (query.isError) return <Card className="mb-4 p-5"><ErrorState message={`Nakit girişi alınamadı. ${query.error.message}`} /></Card>;
  const p = query.data.projection;
  const amount = (value: number) => moneyPrecise(value, p.currency);
  if (p.closedPeriods === 0) return <Card className="mb-4 p-5"><h2 className="font-semibold">Önümüzdeki 13 hafta: beklenen nakit girişi</h2>
    <p className="mt-2 text-sm text-[#6d7175]">Bu para biriminde kapanmış dönem kaydı yok; tablo boş. Kayıt başlayınca tablo kendiliğinden dolar.</p></Card>;
  return <Card className="mb-4 p-5">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h2 className="font-semibold">Önümüzdeki 13 hafta: beklenen nakit girişi</h2>
        <p className="mt-1 text-xs text-[#6d7175]">Hesap tarihi {dayText(p.today)} · {p.closedPeriods} kapanmış dönem · {p.currency} · Son gün {dayText(p.horizon)}</p>
      </div>
      <span className="rounded-lg border px-2.5 py-1 text-xs font-semibold text-[#6d7175]">Beklenen, garanti değil</span>
    </div>
    <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {[['13 haftada vadesi gelen', p.dueTotal], ['13 haftadaki ödeme sözleri', p.promisedTotal], ['Gecikmiş alacak', p.overdue], ['Vadesi bilinmeyen alacak', p.unknownDue]].map(([label, value]) =>
        <div key={String(label)} className="rounded-lg border p-3"><p className="text-sm text-[#6d7175]">{label}</p><p className="mt-1 break-words font-semibold">{amount(Number(value))}</p></div>)}
    </div>
    <div className="mt-4 overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[560px] text-sm">
        <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr>
          <th className="px-4 py-2">Hafta</th><th className="px-4 py-2">Vadesi gelen</th><th className="px-4 py-2">Dönem</th>
          <th className="px-4 py-2">Ödeme sözü</th><th className="px-4 py-2">Söz</th>
        </tr></thead>
        <tbody>{p.rows.map(row => <tr key={row.from} className="border-t">
          <td className="px-4 py-2">{dayText(row.from)} – {dayText(row.through)}</td>
          <td className="px-4 py-2 font-semibold">{amount(row.due)}</td>
          <td className="px-4 py-2">{row.dueCount}</td>
          <td className="px-4 py-2 font-semibold">{amount(row.promised)}</td>
          <td className="px-4 py-2">{row.promiseCount}</td>
        </tr>)}</tbody>
      </table>
    </div>
    <div className="mt-3 grid gap-3 sm:grid-cols-2">
      <p className="text-sm text-[#6d7175]">Takvim dışında kalan gecikmiş alacak: <strong className="text-[#4a4d50]">{amount(p.overdue)}</strong> · vadesi bilinmeyen: <strong className="text-[#4a4d50]">{amount(p.unknownDue)}</strong> · 13 hafta sonrasındaki vadeler: <strong className="text-[#4a4d50]">{amount(p.beyondHorizon)}</strong></p>
      <p className="text-sm text-[#6d7175]">Tarihi geçmiş ödeme sözlerinden kalan: <strong className="text-[#4a4d50]">{amount(p.overduePromises)}</strong> · 13 hafta sonrasındaki sözler: <strong className="text-[#4a4d50]">{amount(p.beyondHorizonPromises)}</strong></p>
    </div>
    <ul className="mt-3 list-disc space-y-1 pl-5 text-xs text-[#6d7175]">{p.notes.map(note => <li key={note}>{note}</li>)}</ul>
  </Card>;
}
