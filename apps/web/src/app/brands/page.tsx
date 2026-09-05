'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, money, percent } from '@/lib/api';
import { Badge, Card, PageHeader, PrimaryLink } from '@/components/ui/core';
import {
    EmptyState,
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
import { turkce } from '@/lib/turkish';
type Brand = {
    id: string;
    name: string;
    industry: string;
    platform: string;
    status: string;
    economics?: { averageMonthlyRevenue: number; grossMarginRate: number };
};
export default function Brands() {
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [sort, setSort] = useState('name');
    const [page, setPage] = useState(1);
    const term = useDebouncedValue(search);
    const { data, isLoading, error } = useQuery({
        queryKey: ['brands', term, status, sort, page],
        queryFn: () =>
            api<Paged<Brand>>(
                `/api/brands?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}${status ? `&status=${status}` : ''}`,
            ),
    });
    const changeSearch = (v: string) => {
        setSearch(v);
        setPage(1);
    };
    const changeStatus = (v: string) => {
        setStatus(v);
        setPage(1);
    };
    return (
        <>
            <PageHeader
                title="Markalar"
                description="Potansiyel ve aktif iş ortaklığı yürüttüğümüz markalar."
                action={
                    <PrimaryLink href="/brands/new">Marka ekle</PrimaryLink>
                }
            />
            <Card className="overflow-hidden">
                <ListControls
                    sort={sort}
                    onSort={(value) => { setSort(value); setPage(1); }}
                    search={search}
                    onSearch={changeSearch}
                    status={status}
                    onStatus={changeStatus}
                    placeholder="Markalarda ara…"
                    statuses={[
                        ['Lead', 'Potansiyel marka'],
                        ['Evaluation', 'Değerlendiriliyor'],
                        ['Negotiation', 'Görüşmede'],
                        ['Active', 'Etkin'],
                        ['Paused', 'Duraklatıldı'],
                        ['Rejected', 'Reddedildi'],
                        ['Closed', 'Arşivlendi'],
                    ]}
                />
                {isLoading ? (
                    <p className="p-6 text-sm">Yükleniyor…</p>
                ) : error ? (
                    <p className="p-6 text-sm text-[#d72c0d]">
                        {error.message}
                    </p>
                ) : data?.items.length ? (
                    <div className="table-scroll">
                        <table className="w-full min-w-[760px] text-left text-sm">
                            <thead className="bg-[#f7f7f8] text-xs uppercase text-[#6d7175]">
                                <tr>
                                    {[
                                        'Marka',
                                        'Durum',
                                        'Altyapı',
                                        'Aylık ciro',
                                        'Brüt kâr marjı',
                                    ].map((x) => (
                                        <th key={x} className="px-5 py-3">
                                            {x}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {data.items.map((b) => (
                                    <tr key={b.id} className="border-t">
                                        <td className="px-5 py-4">
                                            <Link
                                                href={`/brands/${b.id}`}
                                                className="font-semibold text-[#005bd3]"
                                            >
                                                {b.name}
                                            </Link>
                                            <div className="text-xs text-[#6d7175]">
                                                {b.industry ||
                                                    'Sektör belirtilmedi'}
                                            </div>
                                        </td>
                                        <td className="px-5">
                                            <Badge
                                                tone={
                                                    b.status === 'Active'
                                                        ? 'green'
                                                        : b.status ===
                                                            'Rejected'
                                                          ? 'red'
                                                          : 'blue'
                                                }
                                            >
                                                {turkce(b.status)}
                                            </Badge>
                                        </td>
                                        <td className="px-5">
                                            {turkce(b.platform)}
                                        </td>
                                        <td className="px-5">
                                            {money(
                                                b.economics
                                                    ?.averageMonthlyRevenue ??
                                                    0,
                                            )}
                                        </td>
                                        <td className="px-5">
                                            {percent(
                                                b.economics?.grossMarginRate ??
                                                    0,
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState>
                        Arama ve filtrelere uygun marka bulunamadı.
                    </EmptyState>
                )}{' '}
                {data && <Pagination {...data} onPage={setPage} />}
            </Card>
        </>
    );
}
