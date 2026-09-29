'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { API_URL, api, money, moneyPrecise, percent, token } from '@/lib/api';
import { Badge, Card, EmptyState, ErrorState, LoadingState, PageHeader } from '@/components/ui/core';
import {
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
import { turkce } from '@/lib/turkish';
import { collectionState, type CollectionBalance } from '@/components/collection-panel';
import { dayText } from '@/components/work-tasks';
type C = {
    balance: CollectionBalance;
    dueOn: string | null;
    invoiceReference: string;
    id: string;
    year: number;
    month: number;
    brand: string;
    commissionableRevenue: number;
    dealType: string;
    monthlyRetainer: number;
    minimumMonthlyFee: number;
    ovoFee: number;
    effectiveRate: number;
    commissionStatus: string;
    periodStatus: string;
};
type CommissionPage = Paged<C> & { currency: string; currencies: string[]; paid: number; outstanding: number; overdue: number; summary: { ovoFee: number; recordCount: number } };
export default function Page() {
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [sort, setSort] = useState('recent');
    const [page, setPage] = useState(1);
    const [period, setPeriod] = useState('');
    const [scope, setScope] = useState('All');
    const [currency, setCurrency] = useState('');
    const [collection, setCollection] = useState('all');
    const term = useDebouncedValue(search);
    const [exporting, setExporting] = useState(false);
    async function downloadXlsx() {
        if (exporting) return;
        setExporting(true);
        try {
            const params = new URLSearchParams({ sort, scope, collection });
            if (term) params.set('search', term);
            if (status) params.set('status', status);
            if (period) { params.set('year', period.split('-')[0]); params.set('month', period.split('-')[1]); }
            if (currency) params.set('currency', currency);
            const response = await fetch(`${API_URL}/api/commissions/export?${params}`, { headers: { authorization: 'Bearer ' + token() } });
            if (!response.ok) throw new Error('Excel dosyası oluşturulamadı.');
            const url = URL.createObjectURL(await response.blob());
            const link = document.createElement('a'); link.href = url; link.download = `hakedisler${period ? '-' + period : ''}.xlsx`; link.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            window.dispatchEvent(new CustomEvent('ovo:notice', { detail: { message: e instanceof Error ? e.message : 'Excel dosyası oluşturulamadı.', tone: 'error' } }));
        } finally { setExporting(false); }
    }
    const { data, error, isLoading } = useQuery({
        queryKey: ['commissions', term, status, sort, page, period, scope, currency, collection],
        queryFn: () =>
            api<CommissionPage>(
                `/api/commissions?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}&scope=${scope}&collection=${collection}${status ? `&status=${status}` : ''}${period ? `&year=${period.split('-')[0]}&month=${period.split('-')[1]}` : ''}${currency ? `&currency=${currency}` : ''}`,
            ),
    });
    return (
        <div className="commission-document">
            <div className="report-controls">
                <PageHeader
                    title="Hakedişler"
                    description="Hakediş, parçalı ödeme ve kalan alacağı ayrı takip edin. Fatura ve ödeme eklemek için ilgili ayı açın. Tutarlar KDV hariçtir."
                />
                <div className="mb-4 flex flex-wrap items-center gap-3">
                    <Link href="/commissions/planning" className="text-sm font-semibold underline">Alacak yaşı ve vade takvimini aç</Link>
                    <button className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={!data} onClick={() => window.print()}>PDF’ye kaydet / yazdır</button>
                    <button className="rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50" disabled={!data || exporting} onClick={() => void downloadXlsx()}>{exporting ? 'Dosya hazırlanıyor…' : 'Excel olarak indir (.xlsx)'}</button>
                    <p className="text-xs text-[#6d7175]">Yazdırma, seçili filtrenin açık sayfasındaki kayıtları ve özet satırlarını içerir. Excel dosyası aynı filtredeki <strong>tüm sayfaları</strong> tek dosyada toplar.</p>
                </div>
            <Card className="mb-4 p-4">
                <div className="flex flex-wrap items-end gap-3">
                    <label><span className="label">Dönem (boşsa tüm aylar)</span><input type="month" className="input mt-1.5" min="2020-01" max="2100-12" value={period} onChange={e => { setPeriod(e.target.value); setPage(1); }} /></label>
                    <label><span className="label">Kapsam</span><select className="input mt-1.5" value={scope} onChange={e => { setScope(e.target.value); setPage(1); }}><option value="All">Tüm kayıtlar (taslaklar dahil)</option><option value="Closed">Kapanmış dönemler</option><option value="Approved">Onaylı, henüz kilitlenmemiş</option><option value="Preparation">Hazırlık ve kontrol aşaması</option></select></label>
                    <label><span className="label">Para birimi</span><select className="input mt-1.5" value={currency || data?.currency || ''} onChange={e => { setCurrency(e.target.value); setPage(1); }}>{!data && <option value="">Yükleniyor…</option>}{data?.currencies.map(value => <option key={value}>{value}</option>)}</select></label>
                    <label><span className="label">Tahsilat görünümü</span><select className="input mt-1.5" value={collection} onChange={e => { setCollection(e.target.value); setPage(1); }}>{[['all', 'Tümü'], ['outstanding', 'Alacağı kalanlar'], ['partial', 'Kısmen ödenenler'], ['overdue', 'Vadesi geçenler'], ['settled', 'Alacağı kapananlar'], ['review', 'İnceleme gerekli']].map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
                    <button className="rounded-lg border px-3 py-2 text-sm" onClick={() => { setPeriod(''); setPage(1); }}>Tüm aylar</button>
                </div>
                {data && <p className="mt-3 text-sm"><strong>Filtreye uyan toplam hakediş: {data.summary.recordCount ? money(data.summary.ovoFee, data.currency) : 'Kayıt yok'}</strong> · {data.summary.recordCount} kayıt, tüm sayfalar dahil. {scope !== 'Closed' && 'Bu toplamın tamamı kapanmış veya tahsil edilmiş hakediş değildir.'}</p>}
                {data && <p className="mt-2 text-sm">Aynı filtredeki kapanmış kayıtlar · Ödenen: {moneyPrecise(data.paid, data.currency)} · Kalan: {moneyPrecise(data.outstanding, data.currency)} · Bugün vadesi geçmiş kalan: {moneyPrecise(data.overdue, data.currency)}. Eski tarihsiz ödenmişler, ödenen toplamına dahildir.</p>}
            </Card>
            </div>
            <Card className="overflow-hidden">
                <div className="report-controls">
                <ListControls
                    sort={sort}
                    onSort={(value) => { setSort(value); setPage(1); }}
                    search={search}
                    onSearch={(v) => {
                        setSearch(v);
                        setPage(1);
                    }}
                    status={status}
                    onStatus={(v) => {
                        setStatus(v);
                        setPage(1);
                    }}
                    placeholder="Markaya göre ara…"
                    statuses={[
                        ['Draft', 'Taslak'],
                        ['Approved', 'Onaylı / kilitli, faturalanmamış'],
                        ['Invoiced', 'Faturalandı'],
                        ['Paid', 'Ödendi'],
                    ]}
                />
                </div>
                {isLoading ? (
                    <LoadingState />
                ) : error ? (
                    <ErrorState message={error.message} />
                ) : data?.items.length ? (
                    <div className="table-scroll">
                        <div className="overflow-x-auto"><table className="w-full min-w-[1120px] text-left text-sm">
                            <thead className="bg-[#f7f7f8] text-xs uppercase text-[#6d7175]">
                                <tr>
                                    {[
                                        'Ay',
                                        'Marka',
                                        'Hesaplamaya esas ciro',
                                        'Anlaşma modeli',
                                        'Aylık ücret',
                                        'Asgari ücret',
                                        'Son hakediş',
                                        'Gerçekleşen oran',
                                        'Durum',
                                        'Dönem kapanışı',
                                        'Ödenen / kalan',
                                        'Tahsilat / vade',
                                    ].map((x) => (
                                        <th className="px-4 py-3" key={x}>
                                            {x}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {data.items.map((x) => (
                                    <tr className="border-t" key={x.id}>
                                        <td className="px-4 py-4">
                                            <Link
                                                className="font-semibold text-[#005bd3]"
                                                href={`/commissions/${x.id}`}
                                            >
                                                {x.month}/{x.year}
                                            </Link>
                                        </td>
                                        <td className="px-4">{x.brand}</td>
                                        <td className="px-4">
                                            {money(x.commissionableRevenue, data.currency)}
                                        </td>
                                        <td className="px-4">
                                            {turkce(x.dealType)}
                                        </td>
                                        <td className="px-4">
                                            {money(x.monthlyRetainer, data.currency)}
                                        </td>
                                        <td className="px-4">
                                            {money(x.minimumMonthlyFee, data.currency)}
                                        </td>
                                        <td className="px-4 font-semibold">
                                            {money(x.ovoFee, data.currency)}
                                        </td>
                                        <td className="px-4">
                                            {percent(x.effectiveRate)}
                                        </td>
                                        <td className="px-4">
                                            <Badge
                                                tone={
                                                    x.commissionStatus ===
                                                    'Paid'
                                                        ? 'green'
                                                        : 'blue'
                                                }
                                            >
                                                {turkce(x.commissionStatus)}
                                            </Badge>
                                        </td>
                                        <td className="px-4"><Badge>{turkce(x.periodStatus)}</Badge></td>
                                        <td className="px-4 whitespace-nowrap">{moneyPrecise(x.balance.paid, data.currency)} / {moneyPrecise(x.balance.outstanding, data.currency)}</td>
                                        <td className="px-4 py-3"><Badge tone={x.balance.overdueDays || x.balance.needsReview ? 'red' : 'neutral'}>{collectionState(x.balance.state)}</Badge><p className="mt-1">Vade: {dayText(x.dueOn)}{x.balance.overdueDays > 0 && ` · ${x.balance.overdueDays} gün gecikti`}</p><p className="mt-1 break-words">{x.invoiceReference || 'Fatura referansı yok'}</p></td>
                                    </tr>
                                ))}
                            </tbody>
                        </table></div>
                    </div>
                ) : (
                    <EmptyState message="Arama ve filtrelere uygun hakediş bulunamadı." />
                )}
                {data && <div className="report-controls"><Pagination {...data} onPage={setPage} /></div>}
            </Card>
            <style>{`@media print { @page { size: A4 landscape; margin: 10mm; } body:has(.commission-document) aside, body:has(.commission-document) header:not(.report-heading), .report-controls { display: none !important; } body:has(.commission-document) .lg\\:ml-\\[240px\\] { margin-left: 0 !important; } body:has(.commission-document) main { max-width: none !important; padding: 0 !important; } .commission-document { font-size: 8.5pt; } .commission-document table { min-width: 0 !important; } .commission-document .table-scroll, .commission-document .overflow-x-auto { overflow: visible !important; } .commission-document thead { display: table-header-group; } tr { break-inside: avoid; } a { color: inherit !important; text-decoration: none !important; } }`}</style>
        </div>
    );
}
