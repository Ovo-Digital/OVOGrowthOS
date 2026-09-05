'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Badge, Card, PageHeader, PrimaryLink } from '@/components/ui/core';
import {
    EmptyState,
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
import { turkce } from '@/lib/turkish';
type E = {
    id: string;
    brand: { name: string };
    status: string;
    currentStep: number;
    partnershipScore: number;
    dataConfidenceScore: number;
    decision: string;
    updatedAt: string;
};
export default function Page() {
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const [sort, setSort] = useState('recent');
    const [page, setPage] = useState(1);
    const term = useDebouncedValue(search);
    const { data, error, isLoading } = useQuery({
        queryKey: ['evaluations', term, status, sort, page],
        queryFn: () =>
            api<Paged<E>>(
                `/api/evaluations?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}${status ? `&status=${status}` : ''}`,
            ),
    });
    return (
        <>
            <PageHeader
                title="Değerlendirmeler"
                description="Taslaklara devam edin veya tamamlanmış kararları inceleyin."
                action={
                    <PrimaryLink href="/evaluations/new">
                        Yeni değerlendirme
                    </PrimaryLink>
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
                    placeholder="Marka adına göre ara…"
                    statuses={[
                        ['Draft', 'Taslak'],
                        ['InProgress', 'Devam ediyor'],
                        ['ReadyForAnalysis', 'Bilgi bekliyor'],
                        ['Analyzed', 'Analiz tamamlandı'],
                        ['Approved', 'Onaylandı'],
                        ['Rejected', 'Reddedildi'],
                        ['Archived', 'Arşivlendi'],
                    ]}
                />
                {isLoading ? (
                    <p className="p-6 text-sm">Yükleniyor…</p>
                ) : error ? (
                    <p className="p-6 text-[#d72c0d]">{error.message}</p>
                ) : data?.items.length ? (
                    <div className="table-scroll">
                        <table className="w-full min-w-[760px] text-left text-sm">
                            <thead className="bg-[#f7f7f8] text-xs uppercase text-[#6d7175]">
                                <tr>
                                    {[
                                        'Marka',
                                        'Durum',
                                        'İlerleme',
                                        'Puan',
                                        'Veri güveni',
                                        'Karar',
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
                                                href={`/evaluations/${x.id}`}
                                            >
                                                {x.brand?.name ?? 'Marka'}
                                            </Link>
                                        </td>
                                        <td className="px-5">
                                            <Badge
                                                tone={
                                                    x.status === 'Approved'
                                                        ? 'green'
                                                        : x.status ===
                                                            'Rejected'
                                                          ? 'red'
                                                          : x.status ===
                                                                  'Draft' ||
                                                              x.status ===
                                                                  'InProgress'
                                                            ? 'blue'
                                                            : 'yellow'
                                                }
                                            >
                                                {turkce(x.status)}
                                            </Badge>
                                        </td>
                                        <td className="px-5">
                                            Adım {x.currentStep}/10
                                        </td>
                                        <td className="px-5">
                                            {x.partnershipScore}
                                        </td>
                                        <td className="px-5">
                                            {x.dataConfidenceScore}
                                        </td>
                                        <td className="px-5">
                                            {turkce(x.decision)}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState>
                        Arama ve filtrelere uygun değerlendirme bulunamadı.
                    </EmptyState>
                )}
                {data && <Pagination {...data} onPage={setPage} />}
            </Card>
        </>
    );
}
