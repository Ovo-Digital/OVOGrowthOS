'use client';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, money, percent } from '@/lib/api';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { turkce } from '@/lib/turkish';
import { notify } from '@/components/feedback';
type Deal = {
    id: string;
    evaluationId: string;
    name: string;
    status: string;
    dealType: string;
    minimumMonthlyFee: number;
    monthlyRetainer: number;
    revenueShareRate: number;
};
type E = {
    status: string;
    brandId: string;
    brand: {
        name: string;
        economics: {
            averageMonthlyRevenue: number;
            grossMarginRate: number;
            currentAdSpend: number;
            returnRate: number;
            averageOrderValue: number;
            newCustomers: number;
            variableCostRate: number;
        };
    };
    internalMonthlyCost: number;
    setupInvestment: number;
    recommendedTargetContributionMargin: number;
};
type Compare = {
    dealId: string;
    name: string;
    ovoFee: number;
    effectiveRate: number;
    ovoGrossProfit: number;
    ovoMargin: number;
    brandContributionProfit: number;
    brandContributionMargin: number;
    setupPayback: number;
    breakEvenMer: number;
    risk: string;
    score: number;
    reasons: string[];
};
export default function Page() {
    const { id } = useParams<{ id: string }>();
    const qc = useQueryClient();
    const e = useQuery({
        queryKey: ['evaluation', id],
        queryFn: () => api<E>(`/api/evaluations/${id}`),
    });
    const deals = useQuery({
        queryKey: ['deals'],
        queryFn: () => api<Deal[]>('/api/deals'),
    });
    const [comparison, setComparison] = useState<Compare[]>([]);
    const mine = deals.data?.filter((x) => x.evaluationId === id) ?? [];
    const create = useMutation({
        mutationFn: () =>
            api(`/api/deals/from-evaluation/${id}/templates`, {
                method: 'POST',
            }),
        onSuccess: async () => {
            notify('Etkin şablonlardan anlaşma seçenekleri oluşturuldu.');
            await qc.invalidateQueries({ queryKey: ['deals'] });
        },
    });
    async function compare() {
        const x = e.data!;
        const b = x.brand.economics;
        const basis = {
            evaluationId: id,
            name: 'Karşılaştırma temeli',
            monthlyRevenue: b.averageMonthlyRevenue,
            grossMarginRate: b.grossMarginRate,
            adSpend: b.currentAdSpend,
            returnRate: b.returnRate,
            averageOrderValue: b.averageOrderValue,
            newCustomers: b.newCustomers,
            variableCostRate: b.variableCostRate,
            ovoInternalMonthlyCost: x.internalMonthlyCost,
            minimumMonthlyFee: 45000,
            commissionModel: 'FixedRetainer',
            revenueShareRate: 0,
            monthlyRetainer: 0,
            baselineRevenue: b.averageMonthlyRevenue,
            incrementalRate: 0,
            profitShareRate: 0,
            commissionTiersJson: '[]',
            targetBrandContributionMargin:
                x.recommendedTargetContributionMargin || 0.15,
            setupInvestment: x.setupInvestment,
            contractMonths: 24,
        };
        setComparison(
            await api<Compare[]>('/api/deals/compare', {
                method: 'POST',
                body: JSON.stringify({ dealIds: mine.map((x) => x.id), basis }),
            }),
        );
    }
    return (
        <>
            <PageHeader
                title="Anlaşma karşılaştırması"
                description={`${e.data?.brand.name ?? 'Değerlendirme'} · aynı verilerle hesaplanan ticari seçenekler`}
            />
            {e.data?.status !== 'Approved' && (
                <Card className="mb-4 border-[#f1d4a8] bg-[#fff8eb] p-4 text-sm">
                    Anlaşma seçeneklerini oluşturmadan önce değerlendirmeyi
                    onaylayın.
                </Card>
            )}
            <div className="flex gap-2">
                <button
                    disabled={
                        e.data?.status !== 'Approved' ||
                        create.isPending ||
                        mine.length > 0
                    }
                    onClick={() => create.mutate()}
                    className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white disabled:opacity-40"
                >
                    Şablonlardan seçenek oluştur
                </button>
                <button
                    disabled={mine.length < 3}
                    onClick={compare}
                    className="rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-40"
                >
                    Seçenekleri karşılaştır
                </button>
            </div>
            <div className="mt-4 grid gap-4 xl:grid-cols-3">
                {(comparison.length ? comparison : mine).map((x, i) => {
                    const c = 'score' in x ? x : null;
                    return (
                        <Card
                            className={`p-5 ${i === 0 && comparison.length ? 'border-[#008060]' : ''}`}
                            key={'score' in x ? x.dealId : x.id}
                        >
                            <div className="flex items-center justify-between">
                                <h2 className="font-semibold">{x.name}</h2>
                                {i === 0 && comparison.length && (
                                    <Badge tone="green">Önerilen</Badge>
                                )}
                            </div>
                            {c ? (
                                <div className="mt-5 space-y-3">
                                    <Row l="OVO hakedişi" v={money(c.ovoFee)} />
                                    <Row
                                        l="Gerçekleşen oran"
                                        v={percent(c.effectiveRate)}
                                    />
                                    <Row
                                        l="OVO brüt kârı"
                                        v={money(c.ovoGrossProfit)}
                                    />
                                    <Row
                                        l="OVO brüt kâr marjı"
                                        v={percent(c.ovoMargin)}
                                    />
                                    <Row
                                        l="Marka katkı marjı"
                                        v={percent(c.brandContributionMargin)}
                                    />
                                    <Row
                                        l="Kurulum yatırımının geri dönüşü"
                                        v={`${c.setupPayback.toFixed(1)} ay`}
                                    />
                                    <Row
                                        l="Başa baş MER"
                                        v={`${c.breakEvenMer.toFixed(2)}x`}
                                    />
                                    <Row l="Risk" v={turkce(c.risk)} />
                                    <div className="border-t pt-3 text-xs text-[#6d7175]">
                                        {c.reasons.join(' · ')}
                                    </div>
                                    <button
                                        onClick={() =>
                                            api(
                                                `/api/deals/${c.dealId}/accept`,
                                                { method: 'POST' },
                                            ).then(() =>
                                                qc.invalidateQueries({
                                                    queryKey: ['deals'],
                                                }),
                                            )
                                        }
                                        className="w-full rounded-lg bg-[#008060] py-2 text-sm font-semibold text-white"
                                    >
                                        Seçeneği kabul et
                                    </button>
                                </div>
                            ) : (
                                <div className="mt-4">
                                    <Badge tone="blue">
                                        {turkce((x as Deal).dealType)}
                                    </Badge>
                                    <p className="mt-3 text-sm text-[#6d7175]">
                                        {turkce((x as Deal).status)}
                                    </p>
                                    <Link className="mt-4 inline-block text-sm font-semibold text-[#005bd3]" href={`/deals/${(x as Deal).id}`}>Anlaşmayı düzenle</Link>
                                </div>
                            )}
                        </Card>
                    );
                })}
            </div>
        </>
    );
}
function Row({ l, v }: { l: string; v: string }) {
    return (
        <div className="flex justify-between text-sm">
            <span className="text-[#6d7175]">{l}</span>
            <strong>{v}</strong>
        </div>
    );
}
