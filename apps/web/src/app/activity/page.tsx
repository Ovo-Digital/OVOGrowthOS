'use client';
import { Suspense, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { turkce, turkceTarih } from '@/lib/turkish';
import {
    EmptyState,
    ListControls,
    Pagination,
    Paged,
    useDebouncedValue,
} from '@/components/list-controls';
type Audit = {
    id: string;
    userId: string;
    action: string;
    entityType: string;
    entityId: string;
    reason: string;
    createdAt: string;
};
export default function Page() {
    return <Suspense fallback={<p>İşlem geçmişi yükleniyor…</p>}><ActivityPage /></Suspense>;
}
function ActivityPage() {
    const parameters = useSearchParams();
    const [entityId, setEntityId] = useState(parameters.get('entityId') ?? '');
    const [search, setSearch] = useState('');
    const [type, setType] = useState(parameters.get('entityType') ?? '');
    const [sort, setSort] = useState('recent');
    const [page, setPage] = useState(1);
    const term = useDebouncedValue(search);
    const { data, error, isLoading } = useQuery({
        queryKey: ['audit', term, type, entityId, sort, page],
        queryFn: () =>
            api<Paged<Audit>>(
                `/api/audit?page=${page}&search=${encodeURIComponent(term)}&sort=${sort}${type ? `&entityType=${encodeURIComponent(type)}` : ''}${entityId ? `&entityId=${encodeURIComponent(entityId)}` : ''}`,
            ),
    });
    return (
        <>
            <PageHeader
                title="İşlem geçmişi"
                description="Karar, anlaşma, aylık sonuç ve hakediş değişikliklerinin kayıtları."
            />
            <Card className="overflow-hidden">
                {entityId && <p className="p-4 text-sm">Yalnızca seçtiğiniz kaydın geçmişi gösteriliyor. <button className="underline" onClick={() => { setEntityId(''); setPage(1); }}>Kayıt filtresini kaldır</button></p>}
                <ListControls
                    sort={sort}
                    onSort={(value) => { setSort(value); setPage(1); }}
                    search={search}
                    onSearch={(v) => {
                        setSearch(v);
                        setPage(1);
                    }}
                    status={type}
                    onStatus={(v) => {
                        setType(v);
                        setEntityId('');
                        setPage(1);
                    }}
                    placeholder="İşlem veya çalışan ara…"
                    statuses={[
                        ['WorkTask', 'Ekip görevi'],
                        ['Brand', 'Marka'],
                        ['Evaluation', 'Değerlendirme'],
                        ['Deal', 'Anlaşma'],
                        ['MonthlyPerformance', 'Aylık sonuç'],
                        ['RuleSet', 'Kural seti'],
                        ['Settings', 'Ayarlar'],
                    ]}
                />
                {isLoading ? (
                    <p className="p-5 text-sm">Yükleniyor…</p>
                ) : error ? (
                    <p className="p-5 text-[#d72c0d]">{error.message}</p>
                ) : data?.items.length ? (
                    <div className="table-scroll">
                        <table className="w-full min-w-[860px] text-left text-sm">
                            <thead className="bg-[#f7f7f8] text-xs uppercase text-[#6d7175]">
                                <tr>
                                    {[
                                        'Tarih',
                                        'İşlem',
                                        'Kayıt türü',
                                        'İşlemi yapan',
                                        'Açıklama',
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
                                        <td className="px-5 py-3">
                                            {turkceTarih(x.createdAt)}
                                        </td>
                                        <td className="px-5">
                                            <Badge tone="blue">
                                                {turkce(x.action)}
                                            </Badge>
                                        </td>
                                        <td className="px-5">
                                            {turkce(x.entityType)}
                                            <div className="text-xs text-[#6d7175]">
                                                {x.entityId}
                                            </div>
                                        </td>
                                        <td className="px-5">{x.userId}</td>
                                        <td className="px-5">
                                            {x.reason || '—'}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState>
                        Arama ve filtrelere uygun işlem bulunamadı.
                    </EmptyState>
                )}
                {data && <Pagination {...data} onPage={setPage} />}
            </Card>
        </>
    );
}
