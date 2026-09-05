'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, money, percent } from '@/lib/api';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import {
    EmptyState,
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
import { turkce } from '@/lib/turkish';
type C = {
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
};
export default function Page() {
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [sort, setSort] = useState('recent');
    const [page, setPage] = useState(1);
    const term = useDebouncedValue(search);
    const { data, error, isLoading } = useQuery({
        queryKey: ['commissions', term, status, sort, page],
        queryFn: () =>
            api<Paged<C>>(
                `/api/commissions?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}${status ? `&status=${status}` : ''}`,
            ),
    });
    return (
        <>
            <PageHeader
                title="Hakedişler"
                description="Kesinleşmiş anlaşma koşulları ve aylık sonuçlara göre hesaplanan OVO hakedişleri."
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
                        ['Approved', 'Onaylandı'],
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
                        <table className="w-full min-w-[1120px] text-left text-sm">
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
                                            {money(x.commissionableRevenue)}
                                        </td>
                                        <td className="px-4">
                                            {turkce(x.dealType)}
                                        </td>
                                        <td className="px-4">
                                            {money(x.monthlyRetainer)}
                                        </td>
                                        <td className="px-4">
                                            {money(x.minimumMonthlyFee)}
                                        </td>
                                        <td className="px-4 font-semibold">
                                            {money(x.ovoFee)}
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
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState>
                        Arama ve filtrelere uygun hakediş bulunamadı.
                    </EmptyState>
                )}
                {data && <Pagination {...data} onPage={setPage} />}
            </Card>
        </>
    );
}
