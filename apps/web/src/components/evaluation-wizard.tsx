'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { api, money } from '@/lib/api';
import { turkce } from '@/lib/turkish';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { ConditionsPanel, Condition } from '@/components/record-panels';
const steps = [
    'Firma',
    'Ciro ve satış',
    'Ürün kârlılığı',
    'Pazarlama',
    'Operasyon',
    'Ürün ve marka',
    'Büyüme',
    'OVO maliyetleri',
    'Analiz',
    'Öneri',
];
type Brand = { id: string; name: string };
type Confidence = 'Verified' | 'ProvidedByBrand' | 'Estimated' | 'Unknown';
type Draft = {
    brandId: string;
    currentStep: number;
    status: string;
    averageMonthlyRevenue: number;
    revenueConfidence: Confidence;
    grossMarginRate: number;
    grossMarginConfidence: Confidence;
    cogsRate: number;
    cogsConfidence: Confidence;
    averageOrderValue: number;
    aovConfidence: Confidence;
    returnRate: number;
    returnRateConfidence: Confidence;
    currentAdSpend: number;
    adSpendConfidence: Confidence;
    currentCac: number;
    cacConfidence: Confidence;
    averageCustomerLtv: number;
    ltvConfidence: Confidence;
    stockCoverageDays: number;
    stockCoverageConfidence: Confidence;
    monthlyOrders: number;
    monthlySessions: number;
    newCustomers: number;
    returningCustomers: number;
    variableCostRate: number;
    productMarketFit: number;
    growthPotential: number;
    operationalReadiness: number;
    creativeCapability: number;
    founderCooperation: number;
    dataMaturity: number;
    internalMonthlyCost: number;
    setupInvestment: number;
};
type Eval = Draft & {
    id: string;
    partnershipScore: number;
    dataConfidenceScore: number;
    decision: string;
    recommendedDealType: string;
    recommendedMinimumFee: number;
    recommendedTargetMer: number;
    recommendationSnapshotJson: string;
    brand?: { economics: Partial<Draft> };
    conditions?: Condition[];
};
type Result = {
    decision: string;
    partnershipScore: number;
    dataConfidenceScore: number;
    recommendedDealType: string;
    recommendedMinimumMonthlyFee: number;
    recommendedTargetMer: number;
    requiredConditions: { code: string; title: string }[];
    positiveSignals: string[];
    riskSignals: string[];
    reasons: string[];
    missingInputs: string[];
};
const defaults: Draft = {
    brandId: '',
    currentStep: 1,
    status: 'Draft',
    averageMonthlyRevenue: 0,
    revenueConfidence: 'Unknown',
    grossMarginRate: 0,
    grossMarginConfidence: 'Unknown',
    cogsRate: 0,
    cogsConfidence: 'Unknown',
    averageOrderValue: 0,
    aovConfidence: 'Unknown',
    returnRate: 0,
    returnRateConfidence: 'Unknown',
    currentAdSpend: 0,
    adSpendConfidence: 'Unknown',
    currentCac: 0,
    cacConfidence: 'Unknown',
    averageCustomerLtv: 0,
    ltvConfidence: 'Unknown',
    stockCoverageDays: 0,
    stockCoverageConfidence: 'Unknown',
    monthlyOrders: 0,
    monthlySessions: 0,
    newCustomers: 0,
    returningCustomers: 0,
    variableCostRate: 0.08,
    productMarketFit: 3,
    growthPotential: 3,
    operationalReadiness: 3,
    creativeCapability: 3,
    founderCooperation: 3,
    dataMaturity: 3,
    internalMonthlyCost: 25_000,
    setupInvestment: 250_000,
};
export function EvaluationWizard({
    evaluationId,
    brandId,
}: {
    evaluationId?: string;
    brandId?: string;
}) {
    const evalQuery = useQuery({
        queryKey: ['evaluation', evaluationId],
        queryFn: () => api<Eval>(`/api/evaluations/${evaluationId}`),
        enabled: !!evaluationId,
    });
    const brands = useQuery({
        queryKey: ['brands'],
        queryFn: () => api<{ items: Brand[] }>('/api/brands'),
    });
    if (evaluationId && evalQuery.isLoading)
        return <Card className="p-8">Değerlendirme yükleniyor…</Card>;
    const existing = evalQuery.data;
    const initial = existing
        ? {
              ...defaults,
              ...existing,
              ...existing.brand?.economics,
              brandId: existing.brandId,
          }
        : { ...defaults, brandId: brandId ?? '' };
    return (
        <Wizard
            key={existing?.id ?? brandId ?? 'new'}
            initial={initial}
            id={existing?.id}
            brands={brands.data?.items ?? []}
            existing={existing}
        />
    );
}
function Wizard({
    initial,
    id: initialId,
    brands,
    existing,
}: {
    initial: Draft;
    id?: string;
    brands: Brand[];
    existing?: Eval;
}) {
    const router = useRouter();
    const [id, setId] = useState(initialId);
    const [step, setStep] = useState(Math.max(0, initial.currentStep - 1));
    const [d, setD] = useState(initial);
    const [result, setResult] = useState<Result | null>(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const locked =
        !!existing?.status &&
        ['Analyzed', 'Approved', 'Rejected', 'Archived'].includes(
            existing.status,
        );
    function set<K extends keyof Draft>(k: K, v: Draft[K]) {
        setD((x) => ({ ...x, [k]: v }));
    }
    function nextStep() {
        const message =
            step === 0 && !d.brandId
                ? 'Devam etmek için bir marka seçin.'
                : step === 1 &&
                    (d.averageMonthlyRevenue <= 0 ||
                        d.averageOrderValue <= 0 ||
                        d.monthlyOrders < 0 ||
                        d.monthlySessions < 0)
                  ? 'Ciro, sepet tutarı, sipariş ve ziyaret bilgilerini kontrol edin.'
                  : step === 2 &&
                      (d.grossMarginRate <= 0 ||
                          d.cogsRate <= 0 ||
                          d.grossMarginRate > 1 ||
                          d.cogsRate > 1 ||
                          d.returnRate < 0 ||
                          d.returnRate > 1 ||
                          d.variableCostRate < 0 ||
                          d.variableCostRate > 1)
                    ? 'Kârlılık oranlarını 0 ile 100 arasında geçerli yüzdeler olarak girin.'
                    : step === 4 && d.stockCoverageDays <= 0
                      ? 'Stok yeterlilik gününü girin.'
                      : '';
        if (message) {
            setError(message);
            return;
        }
        setError('');
        setStep((x) => x + 1);
    }
    async function save(exit = false) {
        setBusy(true);
        setError('');
        try {
            const payload = {
                ...d,
                currentStep: step + 1,
                status: step === 0 ? 'Draft' : 'InProgress',
            };
            const saved = await api<Eval>(
                id ? `/api/evaluations/${id}` : '/api/evaluations',
                { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload) },
            );
            setId(saved.id);
            if (exit) router.push('/evaluations');
            return saved.id;
        } catch (e) {
            setError(
                e instanceof Error ? e.message : 'Değerlendirme kaydedilemedi',
            );
            throw e;
        } finally {
            setBusy(false);
        }
    }
    async function analyze() {
        try {
            const evaluationId = await save();
            const r = await api<Result>(
                `/api/evaluations/${evaluationId}/analyze`,
                { method: 'POST' },
            );
            setResult(r);
            setStep(9);
            router.replace(`/evaluations/${evaluationId}`);
        } catch {}
    }
    const frozen =
        existing?.recommendationSnapshotJson &&
        existing.recommendationSnapshotJson !== '{}'
            ? (JSON.parse(existing.recommendationSnapshotJson) as Result)
            : null;
    const shown =
        result ??
        frozen ??
        (existing
            ? {
                  decision: existing.decision,
                  partnershipScore: existing.partnershipScore,
                  dataConfidenceScore: existing.dataConfidenceScore,
                  recommendedDealType: existing.recommendedDealType,
                  recommendedMinimumMonthlyFee: existing.recommendedMinimumFee,
                  recommendedTargetMer: existing.recommendedTargetMer,
                  requiredConditions: [],
                  positiveSignals: [],
                  riskSignals: [],
                  reasons: [],
                  missingInputs: [],
              }
            : null);
    return (
        <>
            <PageHeader
                title={id ? 'Değerlendirme' : 'Yeni değerlendirme'}
                description={
                    id
                        ? `Kaydedilmiş çalışma · ${turkce(existing?.status ?? d.status)}`
                        : 'Kayıtlı bir markayı seçerek başlayın; daha sonra kaldığınız yerden devam edebilirsiniz.'
                }
            />
            <div className="mb-4 flex overflow-x-auto rounded-xl border bg-white p-3">
                {steps.map((x, i) => (
                    <button
                        key={x}
                        onClick={() => setStep(i)}
                        className={`min-w-[115px] text-left text-[11px] ${i === step ? 'font-bold' : 'text-[#6d7175]'}`}
                    >
                        <span
                            className={`mr-2 inline-grid h-6 w-6 place-items-center rounded-full ${i === step ? 'bg-[#303030] text-white' : 'bg-[#eceeef]'}`}
                        >
                            {i + 1}
                        </span>
                        {x}
                    </button>
                ))}
            </div>
            <div className="grid gap-4 xl:grid-cols-[1fr_300px]">
                <Card className="p-6">
                    <div className="label">10 ADIMDAN {step + 1}. ADIM</div>
                    <h2 className="mt-1 text-xl font-bold">{steps[step]}</h2>
                    <div className="mt-6">
                        <Fields
                            step={step}
                            d={d}
                            set={set}
                            brands={brands}
                            result={shown}
                        />
                    </div>
                    {error && (
                        <p className="mt-4 rounded-lg bg-[#fff4f2] p-3 text-sm text-[#d72c0d]">
                            {error}
                        </p>
                    )}
                    <div className="mt-7 flex justify-between border-t pt-5">
                        <button
                            disabled={step === 0}
                            onClick={() => setStep((x) => x - 1)}
                            className="rounded-lg border px-4 py-2 text-sm disabled:opacity-40"
                        >
                            Geri
                        </button>
                        <div className="flex gap-2">
                            {!locked && (
                                <button
                                    disabled={busy || !d.brandId}
                                    onClick={() => save(true)}
                                    className="rounded-lg border px-4 py-2 text-sm font-semibold"
                                >
                                    Kaydet ve çık
                                </button>
                            )}
                            {step < 8 && (
                                <button
                                    onClick={nextStep}
                                    className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white"
                                >
                                    Devam et
                                </button>
                            )}
                            {step === 8 && !locked && (
                                <button
                                    disabled={busy || !d.brandId}
                                    onClick={analyze}
                                    className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white"
                                >
                                    {busy
                                        ? 'Hesaplanıyor…'
                                        : 'Analizi çalıştır'}
                                </button>
                            )}
                            {shown && step === 9 && (
                                <>
                                    <a
                                        href={`/evaluations/${id}/scenarios`}
                                        className="rounded-lg border px-4 py-2 text-sm font-semibold"
                                    >
                                        Senaryolar
                                    </a>
                                    {(existing?.status === 'Analyzed' ||
                                        !!result) &&
                                        shown.decision !== 'Reject' &&
                                        shown.decision !== 'NeedMoreData' && (
                                        <button
                                            onClick={() =>
                                                api(
                                                    `/api/evaluations/${id}/approve`,
                                                    { method: 'POST' },
                                                ).then(() => location.reload())
                                            }
                                            className="rounded-lg bg-[#008060] px-4 py-2 text-sm font-semibold text-white"
                                        >
                                            Değerlendirmeyi onayla
                                        </button>
                                    )}
                                </>
                            )}
                        </div>
                    </div>
                </Card>
                <Card className="h-fit p-5">
                    <h3 className="font-semibold">Veri kalitesi</h3>
                    <p className="mt-2 text-xs leading-5 text-[#6d7175]">
                        Kritik bilgilerin kaynağı ve güven düzeyi saklanır.
                        Bilinmeyen değerler varsa sistem karar vermeden önce ek
                        bilgi ister.
                    </p>
                    <div className="mt-4">
                        <Info
                            l="Veri güven puanı"
                            v={
                                shown
                                    ? `${shown.dataConfidenceScore} / 100`
                                    : 'Analizi çalıştır'
                            }
                        />
                        <Info l="Oran giriş biçimi" v="5 yazın = %5" />
                    </div>
                </Card>
            </div>
            {existing?.conditions?.length ? (
                <div className="mt-4">
                    <ConditionsPanel conditions={existing.conditions} />
                </div>
            ) : null}
        </>
    );
}
function Fields({
    step,
    d,
    set,
    brands,
    result,
}: {
    step: number;
    d: Draft;
    set: <K extends keyof Draft>(k: K, v: Draft[K]) => void;
    brands: Brand[];
    result: Result | null;
}) {
    if (step === 0)
        return (
            <label className="block max-w-md">
                <span className="label">MARKA</span>
                <select
                    disabled={!!d.brandId}
                    className="input mt-1.5"
                    value={d.brandId}
                    onChange={(e) => set('brandId', e.target.value)}
                >
                    <option value="">Kayıtlı bir marka seçin</option>
                    {brands.map((b) => (
                        <option value={b.id} key={b.id}>
                            {b.name}
                        </option>
                    ))}
                </select>
            </label>
        );
    if (step === 1)
        return (
            <Grid>
                <Num l="Aylık ciro" k="averageMonthlyRevenue" d={d} set={set} />
                <Conf
                    l="Ciro bilgisinin güven düzeyi"
                    k="revenueConfidence"
                    d={d}
                    set={set}
                />
                <Num
                    l="Ortalama sepet tutarı"
                    k="averageOrderValue"
                    d={d}
                    set={set}
                />
                <Conf
                    l="Sepet tutarının güven düzeyi"
                    k="aovConfidence"
                    d={d}
                    set={set}
                />
                <Num
                    l="Aylık sipariş sayısı"
                    k="monthlyOrders"
                    d={d}
                    set={set}
                />
                <Num
                    l="Aylık site ziyareti"
                    k="monthlySessions"
                    d={d}
                    set={set}
                />
            </Grid>
        );
    if (step === 2)
        return (
            <Grid>
                <Rate
                    l="Brüt kâr marjı"
                    k="grossMarginRate"
                    d={d}
                    set={set}
                />
                <Conf
                    l="Marj bilgisinin güven düzeyi"
                    k="grossMarginConfidence"
                    d={d}
                    set={set}
                />
                <Rate l="Ürün maliyeti oranı" k="cogsRate" d={d} set={set} />
                <Conf
                    l="Ürün maliyetinin güven düzeyi"
                    k="cogsConfidence"
                    d={d}
                    set={set}
                />
                <Rate l="İade oranı" k="returnRate" d={d} set={set} />
                <Conf
                    l="İade bilgisinin güven düzeyi"
                    k="returnRateConfidence"
                    d={d}
                    set={set}
                />
                <Rate
                    l="Değişken gider oranı"
                    k="variableCostRate"
                    d={d}
                    set={set}
                />
            </Grid>
        );
    if (step === 3)
        return (
            <Grid>
                <Num
                    l="Aylık reklam harcaması"
                    k="currentAdSpend"
                    d={d}
                    set={set}
                />
                <Conf
                    l="Reklam harcamasının güven düzeyi"
                    k="adSpendConfidence"
                    d={d}
                    set={set}
                />
                <Num
                    l="Yeni müşteri edinme maliyeti (CAC)"
                    k="currentCac"
                    d={d}
                    set={set}
                />
                <Conf
                    l="CAC bilgisinin güven düzeyi"
                    k="cacConfidence"
                    d={d}
                    set={set}
                />
                <Num
                    l="Müşteri yaşam boyu değeri (LTV)"
                    k="averageCustomerLtv"
                    d={d}
                    set={set}
                />
                <Conf
                    l="LTV bilgisinin güven düzeyi"
                    k="ltvConfidence"
                    d={d}
                    set={set}
                />
            </Grid>
        );
    if (step === 4)
        return (
            <Grid>
                <Num
                    l="Stok yeterlilik günü"
                    k="stockCoverageDays"
                    d={d}
                    set={set}
                />
                <Conf
                    l="Stok bilgisinin güven düzeyi"
                    k="stockCoverageConfidence"
                    d={d}
                    set={set}
                />
                <Score
                    l="Operasyonel hazırlık"
                    k="operationalReadiness"
                    d={d}
                    set={set}
                />
            </Grid>
        );
    if (step === 5)
        return (
            <Grid>
                <Score
                    l="Ürün-pazar uyumu"
                    k="productMarketFit"
                    d={d}
                    set={set}
                />
                <Score
                    l="İçerik üretme kapasitesi"
                    k="creativeCapability"
                    d={d}
                    set={set}
                />
                <Score
                    l="Kurucu iş birliği"
                    k="founderCooperation"
                    d={d}
                    set={set}
                />
            </Grid>
        );
    if (step === 6)
        return (
            <Grid>
                <Score
                    l="Büyüme potansiyeli"
                    k="growthPotential"
                    d={d}
                    set={set}
                />
                <Score l="Veri yeterliliği" k="dataMaturity" d={d} set={set} />
                <Num l="Yeni müşteri sayısı" k="newCustomers" d={d} set={set} />
                <Num
                    l="Tekrar alışveriş yapan müşteri sayısı"
                    k="returningCustomers"
                    d={d}
                    set={set}
                />
            </Grid>
        );
    if (step === 7)
        return (
            <Grid>
                <Num
                    l="OVO aylık iç maliyeti"
                    k="internalMonthlyCost"
                    d={d}
                    set={set}
                />
                <Num l="Kurulum yatırımı" k="setupInvestment" d={d} set={set} />
            </Grid>
        );
    if (!result)
        return (
            <div className="rounded-lg border bg-[#f7f7f8] p-8 text-center text-sm text-[#6d7175]">
                Bilgileri kaydedip analizi çalıştırın. Sistem kullanılan
                kuralları, girilen bilgileri, hesapları ve öneriyi
                değiştirilemeyecek şekilde saklar.
            </div>
        );
    return (
        <div>
            <div className="flex items-center gap-3">
                <h3 className="text-2xl font-bold">
                    {turkce(result.decision)}
                </h3>
                <Badge
                    tone={
                        result.decision === 'Accept'
                            ? 'green'
                            : result.decision === 'Reject'
                              ? 'red'
                              : 'yellow'
                    }
                >
                    {result.partnershipScore} / 100
                </Badge>
            </div>
            <div className="mt-6 grid gap-4 md:grid-cols-3">
                <Info
                    l="Veri güveni"
                    v={`${result.dataConfidenceScore} / 100`}
                />
                <Info
                    l="Anlaşma modeli"
                    v={turkce(result.recommendedDealType)}
                />
                <Info
                    l="Asgari aylık ücret"
                    v={money(result.recommendedMinimumMonthlyFee)}
                />
                <Info
                    l="Başa baş reklam verimliliği (MER)"
                    v={`${result.recommendedTargetMer.toFixed(2)}x`}
                />
            </div>
            {result.missingInputs.length > 0 && (
                <List title="Eksik bilgiler" items={result.missingInputs} />
            )}
            <List title="Olumlu göstergeler" items={result.positiveSignals} />
            <List title="Risk göstergeleri" items={result.riskSignals} />
            <List
                title="Gerekli koşullar"
                items={result.requiredConditions.map((x) => x.title)}
            />
        </div>
    );
}
function Grid({ children }: { children: React.ReactNode }) {
    return <div className="grid gap-5 md:grid-cols-2">{children}</div>;
}
function Num<K extends keyof Draft>({
    l,
    k,
    d,
    set,
}: {
    l: string;
    k: K;
    d: Draft;
    set: (k: K, v: Draft[K]) => void;
}) {
    return (
        <label>
            <span className="label">{l.toUpperCase()}</span>
            <input
                type="number"
                step="any"
                className="input mt-1.5"
                value={String(d[k])}
                onChange={(e) => set(k, Number(e.target.value) as Draft[K])}
            />
        </label>
    );
}
function Rate<K extends keyof Draft>({
    l,
    k,
    d,
    set,
}: {
    l: string;
    k: K;
    d: Draft;
    set: (k: K, v: Draft[K]) => void;
}) {
    return (
        <label>
            <span className="label">{l.toUpperCase()}</span>
            <div className="relative mt-1.5">
                <input
                    type="number"
                    min="0"
                    max="100"
                    step="0.01"
                    className="input pr-10"
                    value={String(Number(d[k]) * 100)}
                    onChange={(e) =>
                        set(k, (Number(e.target.value) / 100) as Draft[K])
                    }
                />
                <span className="pointer-events-none absolute right-3 top-2.5 text-sm text-[#6d7175]">
                    %
                </span>
            </div>
        </label>
    );
}
function Score<K extends keyof Draft>({
    l,
    k,
    d,
    set,
}: {
    l: string;
    k: K;
    d: Draft;
    set: (k: K, v: Draft[K]) => void;
}) {
    return (
        <label>
            <span className="label">{l.toUpperCase()}</span>
            <select
                className="input mt-1.5"
                value={String(d[k])}
                onChange={(e) => set(k, Number(e.target.value) as Draft[K])}
            >
                {[1, 2, 3, 4, 5].map((x) => (
                    <option key={x}>{x}</option>
                ))}
            </select>
        </label>
    );
}
function Conf<K extends keyof Draft>({
    l,
    k,
    d,
    set,
}: {
    l: string;
    k: K;
    d: Draft;
    set: (k: K, v: Draft[K]) => void;
}) {
    return (
        <label>
            <span className="label">{l.toUpperCase()}</span>
            <select
                className="input mt-1.5"
                value={String(d[k])}
                onChange={(e) => set(k, e.target.value as Draft[K])}
            >
                {['Verified', 'ProvidedByBrand', 'Estimated', 'Unknown'].map(
                    (x) => (
                        <option key={x} value={x}>
                            {turkce(x)}
                        </option>
                    ),
                )}
            </select>
        </label>
    );
}
function Info({ l, v }: { l: string; v: string }) {
    return (
        <div className="mb-3">
            <div className="label">{l.toUpperCase()}</div>
            <div className="mt-1 text-sm font-semibold">{v}</div>
        </div>
    );
}
function List({ title, items }: { title: string; items: string[] }) {
    if (!items.length) return null;
    return (
        <div className="mt-5">
            <h4 className="text-sm font-bold">{title}</h4>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-[#6d7175]">
                {items.map((x) => (
                    <li key={x}>{turkce(x)}</li>
                ))}
            </ul>
        </div>
    );
}
