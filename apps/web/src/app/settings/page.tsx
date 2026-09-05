'use client';
import { FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card, PageHeader } from '@/components/ui/core';
import { notify } from '@/components/feedback';
type S = {
    id: string;
    defaultCurrency: string;
    defaultVatRate: number;
    defaultContractMonths: number;
    defaultSetupInvestment: number;
    targetOvoGrossMargin: number;
    targetBrandContributionMargin: number;
    minimumFeeMultiplier: number;
    existingRevenueThreshold: number;
    concentrationRiskThreshold: number;
    minimumPartnershipScore: number;
    conditionalPartnershipScore: number;
    minimumDataConfidenceScore: number;
    minimumRecommendedAdSpend: number;
    defaultRuleSetId?: string;
};
const rateKeys = new Set<keyof S>([
    'defaultVatRate',
    'targetOvoGrossMargin',
    'targetBrandContributionMargin',
    'concentrationRiskThreshold',
]);
export default function Page() {
    const qc = useQueryClient();
    const { data: s } = useQuery({
        queryKey: ['settings'],
        queryFn: () => api<S>('/api/settings'),
    });
    async function submit(e: FormEvent<HTMLFormElement>) {
        e.preventDefault();
        const f = new FormData(e.currentTarget);
        const n = (x: string) => Number(f.get(x));
        const rate = (x: string) => n(x) / 100;
        await api('/api/settings', {
            method: 'PUT',
            body: JSON.stringify({
                defaultCurrency: f.get('defaultCurrency'),
                defaultVatRate: rate('defaultVatRate'),
                defaultContractMonths: n('defaultContractMonths'),
                defaultSetupInvestment: n('defaultSetupInvestment'),
                targetOvoGrossMargin: rate('targetOvoGrossMargin'),
                targetBrandContributionMargin: rate(
                    'targetBrandContributionMargin',
                ),
                minimumFeeMultiplier: n('minimumFeeMultiplier'),
                existingRevenueThreshold: n('existingRevenueThreshold'),
                concentrationRiskThreshold: rate(
                    'concentrationRiskThreshold',
                ),
                minimumPartnershipScore: n('minimumPartnershipScore'),
                conditionalPartnershipScore: n('conditionalPartnershipScore'),
                minimumDataConfidenceScore: n('minimumDataConfidenceScore'),
                minimumRecommendedAdSpend: n('minimumRecommendedAdSpend'),
                defaultRuleSetId: s?.defaultRuleSetId,
            }),
        });
        notify('Genel ayarlar kaydedildi.');
        await qc.invalidateQueries({ queryKey: ['settings'] });
    }
    if (!s) return <Card className="p-5">Yükleniyor…</Card>;
    return (
        <>
            <PageHeader
                title="Genel ayarlar"
                description="Tüm hesaplamalarda kullanılan ortak varsayılan değerler."
                action={<div className="flex gap-2"><a href="/deal-templates" className="rounded-lg border px-4 py-2 text-sm font-semibold">Anlaşma şablonları</a><a href="/users" className="rounded-lg border px-4 py-2 text-sm font-semibold">Kullanıcılar</a></div>}
            />
            <Card className="max-w-4xl p-6">
                <form onSubmit={submit} className="grid gap-5 md:grid-cols-2">
                    {[
                        ['defaultCurrency', 'Varsayılan para birimi'],
                        ['defaultVatRate', 'Varsayılan KDV oranı'],
                        [
                            'defaultContractMonths',
                            'Varsayılan sözleşme süresi (ay)',
                        ],
                        [
                            'defaultSetupInvestment',
                            'Varsayılan kurulum yatırımı',
                        ],
                        ['targetOvoGrossMargin', 'Hedef OVO brüt kâr marjı'],
                        [
                            'targetBrandContributionMargin',
                            'Hedef marka katkı marjı',
                        ],
                        ['minimumFeeMultiplier', 'Asgari ücret çarpanı'],
                        ['existingRevenueThreshold', 'Mevcut ciro eşiği'],
                        [
                            'concentrationRiskThreshold',
                            'Müşteri yoğunlaşma riski eşiği',
                        ],
                        ['minimumPartnershipScore', 'Asgari ortaklık puanı'],
                        ['conditionalPartnershipScore', 'Koşullu kabul puanı'],
                        [
                            'minimumDataConfidenceScore',
                            'Asgari veri güven puanı',
                        ],
                        [
                            'minimumRecommendedAdSpend',
                            'Asgari önerilen reklam bütçesi',
                        ],
                    ].map(([k, l]) => (
                        <label key={k}>
                            <span className="label">
                                {l.toUpperCase()}
                                {rateKeys.has(k as keyof S) ? ' (%)' : ''}
                            </span>
                            <input
                                name={k}
                                type={
                                    k === 'defaultCurrency' ? 'text' : 'number'
                                }
                                step="any"
                                className="input mt-1.5"
                                defaultValue={String(
                                    rateKeys.has(k as keyof S)
                                        ? Number(s[k as keyof S]) * 100
                                        : (s[k as keyof S] ?? ''),
                                )}
                            />
                        </label>
                    ))}
                    <div className="md:col-span-2 flex justify-end">
                        <button className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white">
                            Ayarları kaydet
                        </button>
                    </div>
                </form>
            </Card>
        </>
    );
}
