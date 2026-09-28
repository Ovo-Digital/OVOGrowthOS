'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, money, moneyPrecise, percent } from '@/lib/api';
import { turkce } from '@/lib/turkish';
import { Badge, Card, MetricCard, PageHeader, PrimaryLink } from '@/components/ui/core';
import { DashboardChart, DashboardRatioChart, type ReportTrend } from '@/components/dashboard-chart';

type Totals = { recordCount: number; netRevenue: number; ovoFee: number; ovoInternalCost: number; ovoGrossProfit: number; brandContributionProfit: number; totalAdSpend: number; ovoMargin: number | null; mer: number | null };
type Report = {
  period: { year: number; month: number } | null; currency: string; currencies: string[]; scope: string; totals: Totals;
  outstandingCommission: number; paidCommission: number; periodOutstandingCommission: number; allPeriodsPaidCommission: number;
  cashReceivedInSelectedMonth: number; legacyUndatedPaid: number; overdueCommission: number; collectionReviewCount: number;
  totalActiveSetupInvestment: number; activeBrands: number; averagePartnershipScore: number | null;
  stages: { closed: Totals; approved: Totals; preparation: Totals };
  coverage: { recordedBrands: number; selectedBrands: number; expectedBrands: number; missingBrands: { brandId: string; name: string }[]; unknownStartDateBrands: number; unclosedRecords: number };
  concentrationRisk: string; largestClientRevenueShare: number; largestClientOvoFeeShare: number; top3RevenueConcentration: number; top3OvoRevenueConcentration: number;
  trends: ReportTrend[]; dealModelDistribution: { dealType: string; count: number }[];
  currencyTotals: { currency: string; netRevenue: number; ovoFee: number; recordCount: number }[];
  conversion: { target: string; rates: Record<string, number>; missing: string[]; netRevenue: number | null; ovoFee: number | null } | null;
  brands: { id: string; brandId: string; name: string; status: string; netRevenue: number; ovoFee: number; ovoGrossProfit: number; ovoInternalCost: number; mer: number | null; contributionMargin: number | null; health: string }[];
};
const scopes = [['Closed', 'Kapanmış dönemler'], ['Approved', 'Onaylı, henüz kilitlenmemiş'], ['Preparation', 'Hazırlık ve kontrol aşaması'], ['All', 'Tüm kayıtlar (taslaklar dahil)']] as const;
  const ratio = (value: number | null) => value === null ? 'Hesaplanamıyor' : percent(value);

export function PortfolioReportView({ title, brandId, showCharts = false }: { title: string; brandId?: string; showCharts?: boolean }) {
  const [period, setPeriod] = useState('');
  const [scope, setScope] = useState('Closed');
  const [currency, setCurrency] = useState('');
  const [rateDraft, setRateDraft] = useState<Record<string, string>>({});
  const [convertTarget, setConvertTarget] = useState('');
  const [conversionRequest, setConversionRequest] = useState<{ target: string; rates: string } | null>(null);
  const parameters = new URLSearchParams({ scope });
  if (period) { const [year, month] = period.split('-'); parameters.set('year', year); parameters.set('month', month); }
  if (currency) parameters.set('currency', currency);
  if (brandId) parameters.set('brandId', brandId);
  if (conversionRequest) { parameters.set('convertTo', conversionRequest.target); parameters.set('manualRates', conversionRequest.rates); }
  const report = useQuery({ queryKey: ['dashboard', period, scope, currency, brandId, conversionRequest], queryFn: () => api<Report>(`/api/dashboard?${parameters}`) });
  const trendNote = (() => {
    const points = (report.data?.trends ?? []).filter(t => t.netRevenue !== null);
    if (points.length < 6) return '';
    const average = (rows: typeof points) => rows.reduce((sum, t) => sum + (t.netRevenue ?? 0), 0) / rows.length;
    const previous = average(points.slice(-6, -3));
    const latest = average(points.slice(-3));
    if (previous === 0) return 'Son 6 ayda veri var ama önceki 3 ayın ortalaması sıfır; basit trend karşılaştırması yapılamıyor. Bu bir tahmin değildir.';
    const change = (latest - previous) / previous;
    const direction = change > 0.005 ? 'yükseliyor' : change < -0.005 ? 'düşüyor' : 'neredeyse aynı kaldı';
    return <>Son 3 ayın ortalama net cirosu önceki 3 aya göre <strong>{percent(Math.abs(change))}</strong> {direction} ({moneyPrecise(previous, report.data?.currency)} → {moneyPrecise(latest, report.data?.currency)}). Bu değer yalnız kayıtlı veriden hesaplanan basit bir trenddir; gelecek dönem için tahmin değildir.</>;
  })();
  const data = report.data;
  const amount = (value: number) => money(value, data?.currency);
  const hasData = !!data?.totals.recordCount;
  const selectedMonth = period || (data?.period ? `${data.period.year}-${String(data.period.month).padStart(2, '0')}` : '');
  const stageName = scopes.find(([key]) => key === scope)?.[1];
  const periodLabel = data?.period ? `${data.period.month}/${data.period.year}` : 'Henüz dönem kaydı yok';
  return <div className={brandId ? 'mt-6' : ''}>
    <PageHeader title={title} description="Kayıtlı aylık sonuçlar, kapanış aşamaları ve tahsil edilmemiş hakedişler." action={showCharts ? <PrimaryLink href="/evaluations/new">Yeni değerlendirme</PrimaryLink> : undefined} />
    <Card className="mb-4 p-4">
      <div className="flex flex-wrap items-end gap-3">
        <label><span className="label">Dönem</span><input type="month" min="2020-01" max="2100-12" className="input mt-1.5" value={selectedMonth} onChange={e => setPeriod(e.target.value)} /></label>
        <label className="min-w-0"><span className="label">Hesaba katılan kayıtlar</span><select className="input mt-1.5" value={scope} onChange={e => setScope(e.target.value)}>{scopes.map(([key, label]) => <option value={key} key={key}>{label}</option>)}</select></label>
        <label><span className="label">Para birimi</span><select className="input mt-1.5" value={currency || data?.currency || ''} onChange={e => setCurrency(e.target.value)}>{!data && <option value="">Yükleniyor…</option>}{data?.currencies.map(value => <option key={value}>{value}</option>)}</select></label>
        <button className="rounded-lg border px-3 py-2 text-sm" onClick={() => setPeriod('')}>Son kayıtlı ay</button>
      </div>
      <p className="mt-3 text-xs text-[#6d7175]">Kapanmış dönemler: kilitlenmiş, faturalanmış veya ödenmiş kayıtlar. Onaylı ama kilitlenmemiş tutarlar ayrı tutulur. Farklı para birimleri birbiriyle toplanmaz.</p>
    </Card>
    {report.isPending ? <Card className="p-6"><p role="status">Finansal sonuçlar yükleniyor…</p></Card> : report.isError || !data ? <Card className="p-6"><p role="alert">{report.error?.message || 'Rapor alınamadı.'}</p><button className="mt-3 underline" onClick={() => report.refetch()}>Yeniden dene</button></Card> : <>
      <p className="mb-3 text-sm font-semibold">{periodLabel} · {stageName} · {data.currency}</p>
      {scope !== 'Closed' && <Card className="mb-4 border-[#e3bd5c] bg-[#fff8eb] p-4 text-sm">Bu görünümdeki tutarların tamamı kapanmış hakediş değildir. Taslak veya henüz kilitlenmemiş kayıtlar değişebilir; tahsilat olarak yorumlamayın.</Card>}
      <Card className="mb-4 p-4 text-sm">
        <p>{data.coverage.recordedBrands} markanın dönem kaydı var; seçili kapsama {data.coverage.selectedBrands} marka dahil. {data.coverage.unclosedRecords} kayıt henüz kapanmamış.</p>
        {data.coverage.missingBrands.length > 0 && <p className="mt-2 text-[#8e1f0b]">Dönem kaydı eksik: {data.coverage.missingBrands.map((brand, i) => <span key={brand.brandId}>{i > 0 && ', '}<Link className="underline" href={`/brands/${brand.brandId}`}>{brand.name}</Link></span>)}. Bu markalar toplamda sıfır gelir sayılmadı; verileri yok.</p>}
        {data.coverage.unknownStartDateBrands > 0 && <p className="mt-2 text-[#6d7175]">{data.coverage.unknownStartDateBrands} etkin markanın anlaşma başlangıcı kayıtlı değil; geçmiş ayda veri beklenip beklenmediği kesin belirlenemiyor.</p>}
        {!hasData && <p className="mt-2 font-semibold">Seçtiğiniz ay ve kapsamda veri yok. Bu, gelirin sıfır olduğu anlamına gelmez.</p>}
      </Card>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <MetricCard label="PORTFÖY NET CİROSU" value={hasData ? amount(data.totals.netRevenue) : 'Veri yok'} />
        <MetricCard label="OVO HAKEDİŞİ" value={hasData ? amount(data.totals.ovoFee) : 'Veri yok'} />
        <MetricCard label="KAYITLI OVO HİZMET MALİYETİ" value={hasData ? amount(data.totals.ovoInternalCost) : 'Veri yok'} />
        <MetricCard label="OVO BRÜT KÂRI" value={hasData ? amount(data.totals.ovoGrossProfit) : 'Veri yok'} />
        <MetricCard label="OVO BRÜT KÂR MARJI" value={hasData ? ratio(data.totals.ovoMargin) : 'Veri yok'} />
        <MetricCard label="MARKALARA KALAN KATKI" value={hasData ? amount(data.totals.brandContributionProfit) : 'Veri yok'} />
      </div>
      <p className="mt-3 text-xs text-[#6d7175]">OVO brüt kârı, kayıttaki hakedişten hizmet maliyeti çıkarılarak hesaplanır; bu maliyet mevcut sistemde anlaşmadaki tahmindir. Vergi sonrası net kâr veya banka bakiyesi değildir. Eski dönem hesapları bu rapor açılırken yeniden hesaplanmaz.</p>
      {data.currencyTotals.length > 0 && <Card className="mt-4 p-5">
        <h2 className="font-semibold">Para birimi bazında toplamlar</h2>
        <p className="mt-1 text-xs text-[#6d7175]">Seçili dönem ({periodLabel}) ve kapsam için her para birimi ayrı toplanır; farklı para birimleri birbiriyle toplanmaz.</p>
        <div className="mt-3 overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{['Para birimi', 'Net ciro', 'OVO hakedişi', 'Dönem kaydı'].map(h => <th className="border-b py-2 pr-3" key={h}>{h}</th>)}</tr></thead>
          <tbody>{data.currencyTotals.map(row => <tr key={row.currency}><td className="border-b py-2 pr-3 font-semibold">{row.currency}</td><td className="border-b py-2 pr-3">{money(row.netRevenue, row.currency)}</td><td className="border-b py-2 pr-3">{money(row.ovoFee, row.currency)}</td><td className="border-b py-2">{row.recordCount}</td></tr>)}</tbody></table></div>
        {data.currencyTotals.length > 1 && (() => { const target = convertTarget || data.currency; return <div className="mt-4 rounded-lg border p-3">
          <h3 className="text-sm font-semibold">Manuel kurla genel toplam</h3>
          <p className="mt-1 text-xs text-[#6d7175]">Kurlar elle girilir; sistemden canlı kur çekilmez. Genel toplam açıklayıcı bir tahmindir; mali rapor, fatura veya banka bakiyesi değildir.</p>
          <div className="mt-3 flex flex-wrap items-end gap-3">
            <label><span className="label">Toplam para birimi</span><select className="input mt-1.5" value={target} onChange={e => { setConvertTarget(e.target.value); setConversionRequest(null); }}>{data.currencyTotals.map(row => <option key={row.currency}>{row.currency}</option>)}</select></label>
            {data.currencyTotals.filter(row => row.currency !== target).map(row => <label key={row.currency}><span className="label">1 {row.currency} = ? {target}</span><input className="input mt-1.5 w-32" inputMode="decimal" placeholder="ör. 38,5" value={rateDraft[row.currency] ?? ''} onChange={e => setRateDraft(r => ({ ...r, [row.currency]: e.target.value }))} /></label>)}
            <button className="rounded-lg border px-3 py-2 text-sm" onClick={() => { const rates = data.currencyTotals.filter(r => r.currency !== target).map(r => `${r.currency}=${(rateDraft[r.currency] || '').trim()}`).filter(x => !x.endsWith('=')).join(','); setConversionRequest({ target, rates }); }}>Genel toplamı hesapla</button>
          </div>
          {data.conversion && (data.conversion.missing.length > 0
            ? <p role="alert" className="mt-3 text-sm text-[#8e1f0b]">Şu para birimleri için kur girilmedi: {data.conversion.missing.join(', ')}. Genel toplam hesaplanmadı.</p>
            : <div className="mt-3 rounded-lg border bg-[#f7f7f8] p-3 text-sm">
                <p><strong>Genel toplam (net ciro): {moneyPrecise(data.conversion.netRevenue ?? 0, data.conversion.target)}</strong> · OVO hakedişi: {moneyPrecise(data.conversion.ovoFee ?? 0, data.conversion.target)}</p>
                <p className="mt-1 text-xs text-[#6d7175]">{Object.keys(data.conversion.rates).length === 0 ? 'Başka para birimi yok; toplam doğrudan dönem toplamıdır.' : <>Kullanılan manuel kurlar: {Object.entries(data.conversion.rates).map(([c, r]) => `1 ${c} = ${r}`).join(' · ')}</>}</p>
              </div>)}
        </div>; })()}
      </Card>}
      <Card className="mt-4 p-5"><h2 className="font-semibold">Bu ayın kapanış aşamaları</h2><div className="mt-3 grid gap-3 sm:grid-cols-3">{[['Kapanmış', data.stages.closed], ['Onaylı, kilit bekliyor', data.stages.approved], ['Hazırlık / kontrol', data.stages.preparation]].map(([label, value]) => { const totals = value as Totals; return <div className="rounded-lg border p-3" key={String(label)}><p className="text-sm">{String(label)}</p><p className="mt-1 font-semibold">{totals.recordCount ? amount(totals.ovoFee) : 'Kayıt yok'}</p><p className="text-xs text-[#6d7175]">{totals.recordCount} dönem kaydı</p></div>; })}</div></Card>
      <Card className="mt-4 p-5"><h2 className="font-semibold">Hakediş ve tahsilat ayrımı</h2><dl className="mt-3 space-y-3 text-sm">{[
        ['Seçili aydan kalan alacak', moneyPrecise(data.periodOutstandingCommission, data.currency)],
        ['Seçili ayın hakedişine ait ödemeler (eski ödenmişler dahil)', moneyPrecise(data.paidCommission, data.currency)],
        ['Tüm aylardan kalan alacak', moneyPrecise(data.outstandingCommission, data.currency)],
        ['Tüm dönemlere ait ödemeler (eski ödenmişler dahil)', moneyPrecise(data.allPeriodsPaidCommission, data.currency)],
        ['Seçili takvim ayında tarihi kayıtlı para girişi', moneyPrecise(data.cashReceivedInSelectedMonth, data.currency)],
        ['Ödeme tarihi bilinmeyen eski ödenmiş hakedişler (tüm aylar)', moneyPrecise(data.legacyUndatedPaid, data.currency)],
        ['Bugün itibarıyla vadesi geçmiş kalan alacak (tüm aylar)', moneyPrecise(data.overdueCommission, data.currency)],
        ['Şu an etkin anlaşmaların toplam başlangıç yatırımı', amount(data.totalActiveSetupInvestment)]
      ].map(([label, value]) => <div className="flex flex-wrap justify-between gap-2 border-b pb-2" key={label}><dt>{label}</dt><dd className="font-semibold">{value}</dd></div>)}</dl><p className="mt-3 text-xs text-[#6d7175]">Bu bölüm kapanmış dönemlere bakar; üstteki taslak filtresinden etkilenmez. Hakediş ayı ile paranın alındığı ay farklı olabilir. Takvim ayındaki para girişi yalnızca gerçek tarihi girilmiş, iptal edilmemiş ödemeleri içerir; eski tarihsiz ödenmişler hariçtir. Tutarlar KDV hariçtir; banka bakiyesi değildir. Başlangıç yatırımından ne kadarının geri kazanıldığı da bu tutardan anlaşılmaz.</p>{data.collectionReviewCount > 0 && <p role="alert" className="mt-2 text-sm text-red-700">{data.collectionReviewCount} hakediş inceleme gerektiriyor. Negatif tutarlar otomatik borç veya iade kabul edilmez; hakedişlerde “İnceleme gerekli” filtresini açın.</p>}</Card>
      {!brandId && <Card className="mt-4 overflow-hidden"><div className="border-b p-5"><h2 className="font-semibold">Seçili kapsamdaki marka sonuçları</h2><p className="mt-1 text-xs text-[#6d7175]">Satırlar ve üstteki toplamlar aynı kayıtları kullanır. Her satırdan kaynağı açabilirsiniz.</p></div>{data.brands.length ? <div className="table-scroll"><div className="overflow-x-auto"><table className="w-full min-w-[800px] text-left text-sm"><thead><tr>{['Marka', 'Dönem durumu', 'Net ciro', 'OVO hakedişi', 'MER', 'Katkı marjı'].map(label => <th className="px-4 py-3" key={label}>{label}</th>)}</tr></thead><tbody>{data.brands.map(brand => <tr className="border-t" key={brand.id}><td className="px-4 py-3"><Link className="font-semibold underline" href={`/brands/${brand.brandId}`}>{brand.name}</Link></td><td className="px-4"><Link href={`/performance/${brand.id}`}><Badge>{turkce(brand.status)}</Badge></Link></td><td className="px-4">{amount(brand.netRevenue)}</td><td className="px-4"><Link className="underline" href={`/commissions/${brand.id}`}>{amount(brand.ovoFee)}</Link></td><td className="px-4">{brand.mer === null ? 'Hesaplanamıyor' : `${brand.mer.toFixed(2)}x`}</td><td className="px-4">{ratio(brand.contributionMargin)}</td></tr>)}</tbody></table></div></div> : <p className="p-5 text-sm">Bu kapsamda marka sonucu yok.</p>}</Card>}
      {showCharts && <>
        <Card className="mt-4 p-5"><h2 className="font-semibold">Ciro ve kâr gelişimi</h2><p className="mb-4 mt-1 text-xs text-[#6d7175]">Seçili ayda biten son 12 ay · {stageName}. Veri olmayan aylar boş bırakılır.</p><DashboardChart data={data.trends} currency={data.currency} /></Card>
        <Card className="mt-4 p-5"><h2 className="mb-4 font-semibold">Verimlilik gelişimi</h2><DashboardRatioChart data={data.trends} /></Card>
        {trendNote && <p className="mt-3 rounded-lg border bg-[#f7f7f8] p-3 text-sm">{trendNote}</p>}
        {!brandId && <Card className="mt-4 p-5"><h2 className="font-semibold">Portföy ve güncel iş durumu</h2><p className="mt-2 text-sm">Bugün etkin marka: {data.activeBrands}. Seçili kapsamda müşteri yoğunlaşma riski: {turkce(data.concentrationRisk)}.</p><p className="mt-2 text-sm">En büyük müşterinin hakediş payı: {hasData && data.totals.ovoFee > 0 ? percent(data.largestClientOvoFeeShare) : 'Hesaplanamıyor'} · En büyük üç müşterinin payı: {hasData && data.totals.ovoFee > 0 ? percent(data.top3OvoRevenueConcentration) : 'Hesaplanamıyor'}.</p><p className="mt-2 text-sm">Etkin anlaşma modelleri: {data.dealModelDistribution.map(x => `${turkce(x.dealType)} (${x.count})`).join(', ') || 'Etkin anlaşma yok'}.</p></Card>}
      </>}
    </>}
  </div>;
}
