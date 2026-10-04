'use client';
import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, percent } from '@/lib/api';
import { Badge, Card } from '@/components/ui/core';

type Totals = { taskCount: number; plannedHours: number; actualHours: number; voidedHours: number; differenceHours: number; completionRatio: number | null; status: string };
type BrandRow = { brandId: string; brand: string; taskCount: number; plannedHours: number; actualHours: number; voidedHours: number; differenceHours: number; completionRatio: number | null; status: string };
type TaskRow = { brandId: string; brand: string; task: string; plannedHours: number; actualHours: number; differenceHours: number };
type Report = { weekFrom: string | null; weekTo: string | null; brandId: string | null; totals: Totals; items: BrandRow[]; tasks: TaskRow[] };
type Range = { from: string; to: string; brandId: string };

const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';
const number = (v: number) => new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 }).format(v);
const hours = (v: number) => `${number(v)} saat`;
const dateText = (v: string | null) => v ? v.split('-').reverse().join('.') : '—';
const toneFor = (v: number) => v === 0 ? 'neutral' : Math.abs(v) >= 5 ? 'red' : 'yellow';

export function HourDeviationReport() {
  const [range, setRange] = useState<Range>({ from: '', to: '', brandId: '' });
  const [error, setError] = useState('');
  const brands = useQuery({ queryKey: ['brands', 'options'], queryFn: () => api<{ items: { id: string; name: string }[] }>('/api/brands'), refetchOnWindowFocus: false });
  const query = useQuery({
    queryKey: ['hour-deviation', range.from, range.to, range.brandId],
    queryFn: () => {
      const params = new URLSearchParams();
      if (range.from) params.set('weekFrom', range.from);
      if (range.to) params.set('weekTo', range.to);
      if (range.brandId) params.set('brandId', range.brandId);
      const qs = params.toString();
      return api<Report>(`/api/reports/hour-deviation${qs ? `?${qs}` : ''}`);
    },
    refetchOnWindowFocus: false
  });
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    const from = String(data.get('from') ?? ''); const to = String(data.get('to') ?? '');
    if (from && to && from > to) { setError('Başlangıç haftası bitiş haftasından sonra olamaz.'); return; }
    setError(''); setRange({ from, to, brandId: String(data.get('brandId') ?? '') });
  }
  return <Card className="mt-4 p-5">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h2 className="font-semibold">Planlanan ve gerçekleşen saat sapması</h2>
        <p className="mt-1 text-xs text-[#6d7175]">Haftalık görev planı ile girilen çalışma saatlerinin farkı · en fazla 52 haftalık aralık</p>
      </div>
      <span className="rounded-lg border px-2.5 py-1 text-xs font-semibold text-[#6d7175]">Yalnız bilgi verir; finansal hesaba yazılmaz</span>
    </div>
    <form className="mt-4 flex flex-wrap items-end gap-3" onSubmit={submit}>
      <label className="text-sm">Başlangıç haftası<input className="input mt-1" type="date" name="from" min="2020-01-06" max="2100-12-27" step={7} defaultValue={range.from} /></label>
      <label className="text-sm">Bitiş haftası<input className="input mt-1" type="date" name="to" min="2020-01-06" max="2100-12-27" step={7} defaultValue={range.to} /></label>
      <label className="text-sm">Marka<select className="input mt-1" name="brandId" defaultValue={range.brandId}><option value="">Tüm markalar</option>{brands.data?.items.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
      <button className={button}>Aralığı göster</button>
      {(range.from || range.to || range.brandId) && <button type="button" className={button} onClick={() => { setError(''); setRange({ from: '', to: '', brandId: '' }); }}>Son 12 haftaya dön</button>}
    </form>
    {error && <p role="alert" className="mt-3 text-sm text-[#d72c0d]">{error}</p>}
    {query.isPending ? <p className="mt-4" role="status">Saat sapması hesaplanıyor…</p> : query.isError
      ? <p className="mt-4" role="alert">{query.error.message} <button className="underline" onClick={() => void query.refetch()}>Yeniden dene</button></p>
      : <>
        <p className="mt-4 text-xs text-[#6d7175]">Haftalar: {dateText(query.data.weekFrom)} – {dateText(query.data.weekTo)} (pazartesi başlangıç) · İptal edilen saatler gerçekleşene sayılmaz, ayrı gösterilir.</p>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[['Görev sayısı', String(query.data.totals.taskCount)], ['Planlanan', hours(query.data.totals.plannedHours)], ['Gerçekleşen', hours(query.data.totals.actualHours)], ['Sapma', hours(query.data.totals.differenceHours)]].map(([label, value]) =>
            <div key={label} className="rounded-lg border p-3"><p className="text-sm text-[#6d7175]">{label}</p><p className="mt-1 font-semibold">{value}</p></div>)}
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
          <Badge tone={toneFor(query.data.totals.differenceHours)}>{query.data.totals.status}</Badge>
          {query.data.totals.completionRatio !== null && <span className="text-[#6d7175]">Doluluk: <strong className="text-[#4a4d50]">{percent(query.data.totals.completionRatio)}</strong></span>}
          <span className="text-[#6d7175]">İptal edilen: <strong className="text-[#4a4d50]">{hours(query.data.totals.voidedHours)}</strong></span>
        </div>
        {query.data.items.length === 0 ? <p className="mt-4 text-sm">Bu aralıkta planlanmış veya kaydedilmiş saat yok.</p> : <>
          <div className="mt-4 overflow-x-auto rounded-lg border">
            <table className="w-full min-w-[640px] text-sm">
              <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr>
                <th className="px-4 py-2">Marka</th><th className="px-4 py-2">Görev</th><th className="px-4 py-2">Planlanan</th>
                <th className="px-4 py-2">Gerçekleşen</th><th className="px-4 py-2">İptal</th><th className="px-4 py-2">Sapma</th><th className="px-4 py-2">Durum</th>
              </tr></thead>
              <tbody>{query.data.items.map(row => <tr key={row.brandId} className="border-t">
                <td className="px-4 py-2 font-semibold">{row.brand || 'Marka belirtilmedi'}</td>
                <td className="px-4 py-2">{row.taskCount}</td>
                <td className="px-4 py-2">{hours(row.plannedHours)}</td>
                <td className="px-4 py-2">{hours(row.actualHours)}</td>
                <td className="px-4 py-2">{hours(row.voidedHours)}</td>
                <td className="px-4 py-2 font-semibold">{hours(row.differenceHours)}</td>
                <td className="px-4 py-2"><Badge tone={toneFor(row.differenceHours)}>{row.status}</Badge></td>
              </tr>)}</tbody>
            </table>
          </div>
          <div className="mt-4 overflow-x-auto rounded-lg border">
            <h3 className="border-b px-4 py-2 text-sm font-semibold">Görev bazında fark (en fazla 100 görev)</h3>
            <table className="w-full min-w-[560px] text-sm">
              <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr>
                <th className="px-4 py-2">Marka</th><th className="px-4 py-2">Görev</th><th className="px-4 py-2">Planlanan</th>
                <th className="px-4 py-2">Gerçekleşen</th><th className="px-4 py-2">Sapma</th>
              </tr></thead>
              <tbody>{query.data.tasks.map((row, i) => <tr key={`${row.brandId}-${row.task}-${i}`} className="border-t">
                <td className="px-4 py-2">{row.brand || '—'}</td>
                <td className="break-words px-4 py-2">{row.task}</td>
                <td className="px-4 py-2">{hours(row.plannedHours)}</td>
                <td className="px-4 py-2">{hours(row.actualHours)}</td>
                <td className="px-4 py-2 font-semibold">{hours(row.differenceHours)}</td>
              </tr>)}</tbody>
            </table>
          </div>
        </>}
        <ul className="mt-3 list-disc space-y-1 pl-5 text-xs text-[#6d7175]">
          <li>Sapma = gerçekleşen − planlanan. Artı değer planın üzerinde, eksi değer planın altında çalışıldığı anlamına gelir.</li>
          <li>Bu rapor yalnız planlama görüşmesi içindir; bordro, çalışan performans puanı, hakediş veya maliyet hesabına yazılmaz.</li>
        </ul>
      </>}
  </Card>;
}
