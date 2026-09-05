'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, money, percent } from '@/lib/api';
import { notify } from '@/components/feedback';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import {
    EmptyState,
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
import { turkce } from '@/lib/turkish';
type D = {
    id: string;
    name: string;
    status: string;
    dealType: string;
    brand: { name: string };
    monthlyRetainer: number;
    minimumMonthlyFee: number;
    revenueShareRate: number;
};
export default function Page() {
    const qc = useQueryClient();
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [sort, setSort] = useState('recent');
    const [page, setPage] = useState(1);
    const term = useDebouncedValue(search);
    const { data, error, isLoading } = useQuery({
        queryKey: ['deals', term, status, sort, page],
        queryFn: () =>
            api<Paged<D>>(
                `/api/deals?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}${status ? `&status=${status}` : ''}`,
            ),
    });
    async function activate(id: string) {
        await api(`/api/deals/${id}/activate`, { method: 'POST' });
        notify('Anlaşma etkinleştirildi.');
        await qc.invalidateQueries({ queryKey: ['deals'] });
        await qc.invalidateQueries({ queryKey: ['tasks'] });
    }
    return (
        <>
            <PageHeader
                title="Anlaşmalar"
                description="Kaydedilmiş ticari koşullar ve anlaşmaların güncel durumu."
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
                    placeholder="Anlaşma veya marka ara…"
                    statuses={[
                        ['Draft', 'Taslak'],
                        ['InternalReview', 'İç inceleme'],
                        ['Proposed', 'Önerildi'],
                        ['Negotiation', 'Görüşmede'],
                        ['Accepted', 'Kabul edildi'],
                        ['Active', 'Etkin'],
                        ['Expired', 'Süresi doldu'],
                        ['Terminated', 'Sonlandırıldı'],
                    ]}
                />
                {isLoading ? (
                    <p className="p-5 text-sm">Yükleniyor…</p>
                ) : error ? (
                    <p className="p-5 text-[#d72c0d]">{error.message}</p>
                ) : data?.items.length ? (
                    <div className="table-scroll">
                        <table className="w-full min-w-[980px] text-left text-sm">
                            <thead className="bg-[#f7f7f8] text-xs uppercase text-[#6d7175]">
                                <tr>
                                    {[
                                        'Anlaşma',
                                        'Marka',
                                        'Model',
                                        'Aylık ücret',
                                        'Asgari ücret',
                                        'Pay oranı',
                                        'Durum',
                                        'İşlem',
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
                                        <td className="px-4 py-4 font-semibold">
                                            <Link className="text-[#005bd3]" href={`/deals/${x.id}`}>{x.name}</Link>
                                        </td>
                                        <td className="px-4">
                                            {x.brand?.name}
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
                                        <td className="px-4">
                                            {percent(x.revenueShareRate)}
                                        </td>
                                        <td className="px-4">
                                            <Badge
                                                tone={
                                                    x.status === 'Active'
                                                        ? 'green'
                                                        : 'blue'
                                                }
                                            >
                                                {turkce(x.status)}
                                            </Badge>
                                        </td>
                                        <td className="px-4">
                                            {x.status === 'Accepted' && (
                                                <button
                                                    className="text-sm font-semibold text-[#008060]"
                                                    onClick={() =>
                                                        activate(x.id)
                                                    }
                                                >
                                                    Etkinleştir
                                                </button>
                                            )}
                                            {x.status === 'Active' && (
                                                <Link
                                                    href="/performance/new"
                                                    className="text-sm font-semibold"
                                                >
                                                    Aylık sonuç gir
                                                </Link>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState>
                        Arama ve filtrelere uygun anlaşma bulunamadı.
                    </EmptyState>
                )}
                {data && <Pagination {...data} onPage={setPage} />}
            </Card>
        </>
    );
}
