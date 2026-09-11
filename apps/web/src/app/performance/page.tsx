'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, money } from '@/lib/api';
import { Badge, Card, PageHeader, PrimaryLink } from '@/components/ui/core';
import {
    EmptyState,
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
import { turkce } from '@/lib/turkish';
type P = {
    id: string;
    year: number;
    month: number;
    brand: { name: string };
    deal: { currency: string };
    netRevenue: number;
    ovoFee: number;
    mer: number;
    status: string;
};
export default function Page() {
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [sort, setSort] = useState('recent');
    const [page, setPage] = useState(1);
    const term = useDebouncedValue(search);
    const { data, error, isLoading } = useQuery({
        queryKey: ['performance', term, status, sort, page],
        queryFn: () =>
            api<Paged<P>>(
                `/api/performance?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}${status ? `&status=${status}` : ''}`,
            ),
    });
    return (
        <>
            <PageHeader
                title="Aylık sonuçlar"
                description="Onay ve ödeme sürecindeki aylık finansal sonuçlar."
                action={
                    <div className="flex flex-wrap gap-2"><PrimaryLink href="/performance/import">Dosyadan aktar</PrimaryLink><PrimaryLink href="/performance/new">
                        Aylık sonuç gir
                    </PrimaryLink></div>
                }
            />
            <Card className="overflow-hidden">
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
                        ['UnderReview', 'Kontrol bekliyor'],
                        ['Approved', 'Onaylandı'],
                        ['Locked', 'Kilitlendi'],
                        ['Invoiced', 'Faturalandı'],
                        ['Paid', 'Ödendi'],
                    ]}
                />
                {isLoading ? (
                    <p className="p-5 text-sm">Yükleniyor…</p>
                ) : error ? (
                    <p className="p-5 text-[#d72c0d]">{error.message}</p>
                ) : data?.items.length ? (
                    <div className="table-scroll">
                        <table className="w-full min-w-[720px] text-left text-sm">
                            <thead className="bg-[#f7f7f8] text-xs uppercase text-[#6d7175]">
                                <tr>
                                    {[
                                        'Dönem',
                                        'Marka',
                                        'Net ciro',
                                        'OVO ücreti',
                                        'MER',
                                        'Durum',
                                    ].map((x) => (
                                        <th className="px-5 py-3" key={x}>
                                            {x}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {data.items.map((x) => (
                                    <tr className="border-t" key={x.id}>
                                        <td className="px-5 py-4">
                                            <Link
                                                className="font-semibold text-[#005bd3]"
                                                href={`/performance/${x.id}`}
                                            >
                                                {x.month}/{x.year}
                                            </Link>
                                        </td>
                                        <td className="px-5">
                                            {x.brand?.name}
                                        </td>
                                        <td className="px-5">
                                            {money(x.netRevenue, x.deal.currency)}
                                        </td>
                                        <td className="px-5">
                                            {money(x.ovoFee, x.deal.currency)}
                                        </td>
                                        <td className="px-5">
                                            {x.mer.toFixed(2)}x
                                        </td>
                                        <td className="px-5">
                                            <Badge
                                                tone={
                                                    x.status === 'Paid'
                                                        ? 'green'
                                                        : x.status === 'Locked'
                                                          ? 'yellow'
                                                          : 'blue'
                                                }
                                            >
                                                {turkce(x.status)}
                                            </Badge>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState>
                        Arama ve filtrelere uygun aylık sonuç bulunamadı.
                    </EmptyState>
                )}
                {data && <Pagination {...data} onPage={setPage} />}
            </Card>
        </>
    );
}
