'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise, percent, type SessionUser } from '@/lib/api';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { todayText } from '@/components/work-tasks';
import { turkce } from '@/lib/turkish';

type Metrics = { id: string; year: number; month: number; status: string; netRevenue: number; ovoFee: number; adSpend: number; mer: number | null; refundRate: number | null; brandContribution: number; paid: number; outstanding: number };
type Report = { brandId: string; brandName: string; currency: string; year: number; month: number; scope: string; audience: string; generatedAt: string; current: Metrics | null; previous: Metrics | null; revenueChange: number | null; explanations: { whatHappened: string; whyItMatters: string; nextStep: string }[]; csv: string;
  internal?: { storedGrossProfit: number; portfolioFeeShare: number; concentrationWarning: boolean; costs: { plannedCost: number; recordedCost: number; complete: boolean; contributionAfterRecordedCosts: number | null }; investment: { plannedInvestment: number; recordedInvestment: number; recordedRecovery: number; remaining: number; hasRecords: boolean } } };
const scopes = { Closed: 'Kapanmış dönemler', Approved: 'Onaylı, kilit bekleyen', Preparation: 'Hazırlık ve kontrol', All: 'Tüm kayıtlar - taslaklar dahil' };

export function BrandReportPicker() {
  const [page, setPage] = useState(1);
  const brands = useQuery({ queryKey: ['report-brand-picker', page], queryFn: () => api<{ items: { id: string; name: string }[]; total: number; pageSize: number }>(`/api/brands?page=${page}&pageSize=20`) });
  return <Card className="mb-5 p-5"><h2 className="font-semibold">Açıklamalı marka raporu</h2><p className="mt-2 text-sm">Önceki ayla karşılaştırma, anlaşılır açıklamalar ve PDF/tablo çıktısı için bir marka seçin.</p>{brands.isPending ? <p className="mt-3 text-sm">Markalar yükleniyor…</p> : brands.isError ? <p role="alert" className="mt-3 text-sm">{brands.error.message}</p> : <><div className="mt-3 flex flex-wrap gap-2">{brands.data.items.map(b => <Link className="rounded-lg border px-3 py-2 text-sm underline" key={b.id} href={`/reports/brands/${b.id}`}>{b.name}</Link>)}</div>{brands.data.total === 0 && <p className="mt-3 text-sm">Henüz marka kaydı yok.</p>}{brands.data.total > brands.data.pageSize && <div className="mt-3 flex justify-between text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki markalar</button><span>Sayfa {page}</span><button disabled={page * brands.data.pageSize >= brands.data.total} onClick={() => setPage(page + 1)}>Sonraki markalar</button></div>}</>}</Card>;
}

export function BrandReportView({ id }: { id: string }) {
  const [period, setPeriod] = useState(() => todayText().slice(0, 7));
  const [scope, setScope] = useState('Closed'); const [audience, setAudience] = useState('brand'); const [currency, setCurrency] = useState('');
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const validCurrency = currency === '' || /^[A-Z]{3}$/.test(currency);
  const query = useQuery({ queryKey: ['brand-report', id, period, scope, audience, currency], queryFn: () => api<Report>(`/api/reports/brands/${id}?year=${period.split('-')[0]}&month=${period.split('-')[1]}&scope=${scope}&audience=${audience}${currency ? `&currency=${encodeURIComponent(currency)}` : ''}`), enabled: !!period && validCurrency && (audience !== 'internal' || canManage), refetchOnWindowFocus: false });
  const d = query.data; const money = (value: number | null | undefined) => value == null ? 'Veri yok' : moneyPrecise(value, d?.currency);
  const ratio = (value: number | null | undefined) => value == null ? 'Hesaplanamıyor' : percent(value);
  function downloadCsv() {
    if (!d) return;
    const url = URL.createObjectURL(new Blob(['\uFEFF', d.csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a'); link.href = url; link.download = `ovo-marka-raporu-${d.year}-${String(d.month).padStart(2, '0')}-${d.audience}.csv`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  return <>
    <div className="report-controls"><PageHeader title="Açıklamalı marka raporu" description="Seçili ayı bir önceki takvim ayıyla aynı kapsam ve para biriminde karşılaştırın." />
      <Card className="mb-5 p-4"><div className="flex flex-wrap items-end gap-3">
        <label>Dönem<input className="input mt-1" type="month" required min="2020-01" max="2100-12" value={period} onChange={e => setPeriod(e.target.value)} /></label>
        <label>Kapsam<select className="input mt-1" value={scope} onChange={e => setScope(e.target.value)}>{Object.entries(scopes).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
        <label>Rapor görünümü<select className="input mt-1" value={audience} onChange={e => setAudience(e.target.value)}><option value="brand">Markayla paylaşılabilir</option>{canManage && <option value="internal">OVO iç yönetim (paylaşmayın)</option>}</select></label>
        <label>Para birimi<input aria-describedby="report-currency-help" className="input mt-1 w-28" maxLength={3} value={currency || d?.currency || ''} onChange={e => setCurrency(e.target.value.toUpperCase())} placeholder="TRY" /></label>
        <button className="rounded-lg border px-3 py-2 text-sm" onClick={() => query.refetch()} disabled={query.isFetching || !period || !validCurrency}>Raporu yenile</button>
      </div><p id="report-currency-help" className="mt-3 text-xs">Üç harfli para birimi kullanın (TRY, USD, EUR). Dönem/kapsam değişince yeni rapor hazırlanır. İndirilen tablo ve PDF’ye kaydedilen görünüm, ekrandaki aynı rapor anını kullanır.</p></Card>
      <div className="mb-4 flex flex-wrap gap-3"><button disabled={!d || query.isFetching || query.isError} onClick={() => window.print()} className="rounded-lg bg-[#303030] px-3 py-2 text-sm text-white">PDF’ye kaydet / yazdır</button><button disabled={!d || query.isFetching || query.isError} onClick={downloadCsv} className="rounded-lg border px-3 py-2 text-sm">Tabloyu indir (CSV)</button><Link href={`/brands/${id}`} className="rounded-lg border px-3 py-2 text-sm">Markayı aç</Link></div>
      <p className="mb-4 text-xs">PDF düğmesinden sonra tarayıcının hedef bölümünde “PDF olarak kaydet” seçin; yazıcıya göndermek zorunda değilsiniz. Paylaşmadan önce görünümün “Markayla paylaşılabilir” olduğundan emin olun.</p>
    </div>
    {!period ? <Card className="p-5">Bir dönem seçin.</Card> : !validCurrency ? <Card className="p-5">Para birimini üç harfle tamamlayın (örneğin TRY).</Card> : query.isPending ? <Card className="p-5">Rapor hazırlanıyor…</Card> : query.isError || !d ? <Card className="p-5"><p role="alert">{query.error?.message || 'Rapor alınamadı.'}</p></Card> : <article className="brand-report-document rounded-xl border bg-white p-5 sm:p-8">
      <header className="report-heading mb-5 border-b pb-4"><p className="text-sm font-bold">OVO Growth OS</p><h1 className="mt-2 text-2xl font-bold">{d.brandName} - {d.month}/{d.year}</h1><p className="mt-2 text-sm">{scopes[d.scope as keyof typeof scopes]} · {d.currency} · {d.audience === 'internal' ? 'OVO İÇ YÖNETİM - MARKAYLA PAYLAŞMAYIN' : 'Markayla paylaşılabilir rapor'}</p><p className="mt-1 text-xs">Hazırlandı: {new Date(d.generatedAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (Türkiye saati)</p></header>
      <p className="mb-4 rounded-lg border p-3 text-sm">{d.scope === 'Closed' ? 'Yalnız kapanmış dönem sonuçları kullanılmıştır.' : 'Bu görünüm kapanmamış veya taslak sonuçlar içerebilir; kesinleşmiş gelir olarak yorumlamayın.'} Tutarlar KDV hariçtir. Katkı vergi sonrası net kâr, hakediş ise banka bakiyesi değildir.</p>
      {!d.current && <p role="status" className="mb-4 font-semibold">Seçili ay ve kapsamda veri yok. Rakamlar sıfır kabul edilmedi.</p>}
      <div className="report-table overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr><th className="border-b py-3 pr-3">Gösterge</th><th className="border-b py-3 pr-3">{d.month}/{d.year}</th><th className="border-b py-3">{d.previous ? `${d.previous.month}/${d.previous.year}` : 'Önceki ay (veri yok)'}</th></tr></thead><tbody>{[
        ['Net ciro', money(d.current?.netRevenue), money(d.previous?.netRevenue)], ['OVO hakedişi', money(d.current?.ovoFee), money(d.previous?.ovoFee)], ['Reklam harcaması', money(d.current?.adSpend), money(d.previous?.adSpend)],
        ['Reklam verimliliği (MER)', d.current?.mer == null ? 'Hesaplanamıyor' : `${d.current.mer.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}x`, d.previous?.mer == null ? 'Hesaplanamıyor' : `${d.previous.mer.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}x`],
        ['İade / brüt satış', ratio(d.current?.refundRate), ratio(d.previous?.refundRate)], ['Markaya kalan katkı', money(d.current?.brandContribution), money(d.previous?.brandContribution)],
        ['Hakedişe bağlı ödenen', money(d.current?.paid), money(d.previous?.paid)], ['Kalan alacak', money(d.current?.outstanding), money(d.previous?.outstanding)]
      ].map(([label, current, previous]) => <tr key={label}><th className="border-b py-3 pr-3 font-medium">{label}</th><td className="border-b py-3 pr-3">{current}</td><td className="border-b py-3">{previous}</td></tr>)}</tbody></table></div>
      <p className="mt-3 text-xs">Net ciro değişimi: {d.revenueChange === null ? 'Karşılaştırılabilir pozitif önceki ay verisi yok' : percent(d.revenueChange)}. Ödenen tutar, hakedişin ait olduğu aya bağlıdır; paranın aynı takvim ayında alındığını göstermez ve eski tarihsiz ödenmişleri içerebilir.</p>
      <div className="mt-3 flex flex-wrap gap-3 text-xs">{d.current && <Link className="underline" href={`/performance/${d.current.id}`}>Kaynak dönem: {turkce(d.current.status)}</Link>}{d.previous && <Link className="underline" href={`/performance/${d.previous.id}`}>Önceki dönem: {turkce(d.previous.status)}</Link>}</div>
      <h2 className="mt-6 text-lg font-semibold">Markanın durumu ve sonraki adımlar</h2><div className="mt-3 space-y-3">{d.explanations.map((e, index) => <section className="report-explanation rounded-lg border p-4" key={index}><h3 className="font-semibold">Ne oldu?</h3><p className="mt-1 text-sm">{e.whatHappened}</p><h3 className="mt-3 font-semibold">Neden dikkat gerekiyor?</h3><p className="mt-1 text-sm">{e.whyItMatters}</p><h3 className="mt-3 font-semibold">Sonraki adım</h3><p className="mt-1 text-sm">{e.nextStep}</p></section>)}</div>
      {d.internal && <section className="mt-6 rounded-lg border border-[#e3bd5c] bg-[#fff8eb] p-4"><h2 className="text-lg font-semibold">Yalnız OVO iç yönetimi</h2><Badge tone={d.internal.costs.complete ? 'green' : 'yellow'}>{d.internal.costs.complete ? 'Maliyet kontrolü tamamlandı' : 'Maliyetler eksik olabilir'}</Badge><dl className="mt-3 space-y-2 text-sm">{[
        ['Planlanan hizmet maliyeti', money(d.internal.costs.plannedCost)], ['Girilen gerçek hizmet maliyeti', money(d.internal.costs.recordedCost)], ['Gerçek gider sonrası katkı', d.internal.costs.complete ? money(d.internal.costs.contributionAfterRecordedCosts) : 'Kontrol tamamlanmadı; gösterilmiyor'],
        ['Portföyde pozitif hakedişlerden aldığı pay', percent(d.internal.portfolioFeeShare)], ['Kayıtlı yatırım harcaması', money(d.internal.investment.recordedInvestment)], ['Kayıtlı geri kazanım', money(d.internal.investment.recordedRecovery)], ['Kalan kayıtlı yatırım', money(d.internal.investment.remaining)]
      ].map(([label, value]) => <div className="flex flex-wrap justify-between gap-2 border-b pb-2" key={label}><dt>{label}</dt><dd className="font-semibold">{value}</dd></div>)}</dl>{d.internal.concentrationWarning && <p className="mt-3 text-sm">Bu markanın seçili ay/kapsam/para birimindeki pozitif hakediş payı, ayarlardaki yoğunlaşma eşiğini aşıyor. Gelirin tek markaya bağımlılığını değerlendirin.</p>}{!d.internal.investment.hasRecords && <p className="mt-2 text-sm">Yatırım kaydı yok; sıfır kalan “geri kazanıldı” demek değildir.</p>}<p className="mt-2 text-xs">Gerçek gider ayrı karşılaştırmadır; kapanmış brüt kârı yeniden yazmaz. Ortak gider, vergi ve yatırım dağıtımı otomatik yapılmaz.</p></section>}
      <footer className="mt-6 border-t pt-3 text-xs">Bu açıklamalar kayıtlı verilere dayalı sabit kurallarla hazırlanır; neden-sonuç ilişkisini veya gelecek performansı garanti etmez. Rapor yalnız {d.brandName} markasına aittir. Paylaşım öncesinde kapsamı, dönemi ve eksik veri uyarılarını kontrol edin.</footer>
    </article>}
    <style>{`@media print { @page { size: A4; margin: 14mm; } body:has(.brand-report-document) aside, body:has(.brand-report-document) header:not(.report-heading), .report-controls { display: none !important; } body:has(.brand-report-document) .lg\\:ml-\\[240px\\] { margin-left: 0 !important; } body:has(.brand-report-document) main { max-width: none !important; padding: 0 !important; } .brand-report-document { border: 0 !important; padding: 0 !important; font-size: 10pt; } .report-explanation, .report-heading, tr { break-inside: avoid; } .report-table { overflow: visible !important; } a { color: inherit !important; text-decoration: none !important; } }`}</style>
  </>;
}
