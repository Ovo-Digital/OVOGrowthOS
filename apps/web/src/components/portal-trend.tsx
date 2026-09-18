'use client';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise, percent } from '@/lib/api';
import { Card } from '@/components/ui/core';
import { todayText } from '@/components/work-tasks';
import type { PortalSnapshot } from '@/components/portal-report';

type Metrics = PortalSnapshot['metrics'];
type Trend = { months: number; currency: string; sharedMonths: number; items: { year: number; month: number; reportId: string | null; version: number | null; publishedAt: string | null; metrics: Metrics | null; netRevenueChange: number | null }[];
  totals: { netRevenue: number; ovoFee: number; adSpend: number; brandContribution: number; paid: number; outstanding: number; mer: number | null } | null };

export function PortalTrend({ onSelectReport }: { onSelectReport: (id: string) => void }) {
  const [end, setEnd] = useState(() => todayText().slice(0, 7)); const [months, setMonths] = useState(3); const [currency, setCurrency] = useState('');
  const [year, month] = end.split('-').map(Number); const valid = year >= 2020 && year <= 2100 && month >= 1 && month <= 12;
  const query = useQuery({ queryKey: ['portal-trend', end, months, currency], queryFn: () => api<{ currencies: string[]; trend: Trend }>(`/api/portal/trend?endYear=${year}&endMonth=${month}&months=${months}${currency ? `&currency=${currency}` : ''}`), enabled: valid, refetchInterval: 30000 });
  const trend = query.data?.trend; const currencies = Array.from(new Set([...(query.data?.currencies ?? []), currency || trend?.currency || 'TRY']));
  const amount = (n: number) => moneyPrecise(n, trend?.currency ?? 'TRY');
  return <Card className="my-4 space-y-4 p-5 print:hidden"><h2 className="text-lg font-semibold">Paylaşılan aylara toplu bakış</h2>
    <p className="text-sm">Yalnız markanıza açık raporlar kullanılır. Her ay için paylaşımı açık en yüksek sürüm bir kez alınır; aynı ayın sürümleri toplanmaz. Son ay seçtiğiniz süreye dahildir.</p>
    <div className="flex flex-wrap items-end gap-3"><label>Son ay<input type="month" className="input mt-1" value={end} onChange={e => setEnd(e.target.value)} min="2020-01" max="2100-12" required /></label><label>Görünüm<select className="input mt-1" value={months} onChange={e => setMonths(Number(e.target.value))}>{[3, 6, 12].map(n => <option value={n} key={n}>{n} ay</option>)}</select></label><label>Karşılaştırma para birimi<select className="input mt-1" value={currency || trend?.currency || 'TRY'} onChange={e => setCurrency(e.target.value)}>{currencies.map(c => <option key={c}>{c}</option>)}</select></label><button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50" disabled={!valid || query.isFetching} onClick={() => query.refetch()}>Toplu görünümü yenile</button></div>
    <p className="text-sm">Rakamlar her raporun yayımlandığı andaki hâlidir; güncel banka bakiyesi veya bugünkü borç değildir. Sonradan yapılan ödemeler yeni rapor sürümü paylaşılmadıkça buraya gelmez. Para birimleri çevrilmez ve birbirine eklenmez.</p>
    {!valid ? <p role="alert">Geçerli bir son ay seçin.</p> : query.isPending ? <p role="status">Paylaşılan aylar yükleniyor…</p> : query.isError ? <p role="alert">Toplu görünüm alınamadı. {query.error.message}</p> : trend && <>
      <p className="text-sm"><strong>{trend.months} ayın {trend.sharedMonths} tanesinde</strong> bu para biriminde açık rapor var. Eksik ay sıfır sonuç değildir. Yeni sürüm geri çekilmiş ama eski sürüm açık bırakılmışsa o eski sürüm kullanılır; aşağıdaki sürüm numarasını kontrol edin.</p>
      {trend.totals ? <><h3 className="font-semibold">Yalnız paylaşılmış ayların toplamı</h3><dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{[['Net ciro', trend.totals.netRevenue], ['Reklam gideri', trend.totals.adSpend], ['OVO hakedişi', trend.totals.ovoFee], ['Markaya kalan katkı', trend.totals.brandContribution], ['Sürümlerde kayıtlı ödenen', trend.totals.paid], ['Sürümlerde kayıtlı kalan', trend.totals.outstanding]].map(([label, value]) => <div className="min-w-0 rounded-lg border p-3" key={String(label)}><dt className="text-sm">{label}</dt><dd className="mt-1 break-words font-semibold">{amount(Number(value))}</dd></div>)}</dl><p className="text-sm">Toplu reklam verimliliği: <strong>{trend.totals.mer === null ? 'Hesaplanamadı' : trend.totals.mer.toLocaleString('tr-TR', { maximumFractionDigits: 2 }) + 'x'}</strong>. Toplam net ciro / toplam reklam gideridir; aylık oranların ortalaması değildir. Markaya kalan katkı vergi sonrası net kâr değildir. İade oranını kendi kaynak raporunda inceleyin; oranlar toplanmaz.</p></>
        : <p>Seçilen aralık ve para biriminde paylaşılmış rapor yok. Toplam sıfır olarak gösterilmez.</p>}
      <div className="grid gap-3 lg:grid-cols-2">{trend.items.map(row => <article key={`${row.year}-${row.month}`} className="min-w-0 space-y-2 rounded-lg border p-4 text-sm"><h3 className="font-semibold">{row.month}/{row.year}</h3>{row.metrics ? <>
        <p>Sürüm {row.version} · Yayımlanma: {new Date(row.publishedAt!).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (Türkiye)</p>
        <p className="break-words">Net ciro: <strong>{amount(row.metrics.netRevenue)}</strong> · Reklam gideri: {amount(row.metrics.adSpend)}</p><p className="break-words">OVO hakedişi: {amount(row.metrics.ovoFee)} · Markaya kalan katkı: {amount(row.metrics.brandContribution)}</p>
        <p>Bir önceki takvim ayına göre net ciro değişimi: {row.netRevenueChange === null ? 'Karşılaştırılamıyor; önceki ay yok, sıfır veya negatif baz var ya da görünüm bu aydan başlıyor.' : percent(row.netRevenueChange)} Bu fark nedenini tek başına açıklamaz.</p>
        <a className="inline-block underline" href="#selected-portal-report" onClick={() => onSelectReport(row.reportId!)}>Bu ayın kaynak raporunu aç</a>
      </> : <p>Bu ay ve para biriminde paylaşılmış rapor yok. Gelir sıfır kabul edilmedi.</p>}</article>)}</div>
    </>}
  </Card>;
}
