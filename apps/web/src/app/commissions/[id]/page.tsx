'use client';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { api, money, percent } from '@/lib/api';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { turkce } from '@/lib/turkish';
import { CollectionPanel } from '@/components/collection-panel';

type Performance = { brand: { name: string }; deal: { currency: string }; year: number; month: number; status: string; commissionableRevenue: number; ovoFee: number; commissionBreakdownJson: string };
type Breakdown = { baseRetainer: number; calculatedShare: number; minimumFee: number; adjustments: number; finalFee: number; effectiveRate: number; tiers: { revenueAmount: number; rate: number; fee: number }[] };

export default function Page() {
  const { id } = useParams<{ id: string }>();
  const { data, error } = useQuery({ queryKey: ['performance', id], queryFn: () => api<Performance>(`/api/performance/${id}`) });
  if (error) return <Card className="p-6 text-[#d72c0d]">{error.message}</Card>;
  if (!data) return <Card className="p-6">Yükleniyor…</Card>;
  const breakdown = JSON.parse(data.commissionBreakdownJson) as Breakdown;
  const amount = (value: number) => money(value, data.deal.currency);
  const closed = ['Locked', 'Invoiced', 'Paid'].includes(data.status);
  return <>
    <PageHeader title="Hakediş dökümü" description={`${data.brand.name} · ${data.month}/${data.year} · ${data.deal.currency}`} action={<Badge tone={data.status === 'Paid' ? 'green' : 'blue'}>{turkce(data.status)}</Badge>} />
    <Card className="mb-4 max-w-3xl p-4 text-sm">{closed ? 'Bu kayıt kapanmış dönemler kapsamındadır.' : 'Bu kayıt henüz kilitlenmedi; kapanmış dönem toplamına dahil değildir.'} Hakediş ve tahsilat ayrı takip edilir. Ödeme ayrıntılarını aşağıda inceleyin.</Card>
    <Card className="max-w-3xl p-6">
      <Row label="Hesaplamaya esas ciro" value={amount(data.commissionableRevenue)} />
      {(breakdown.tiers ?? []).map((tier, index) => <div className="my-3 rounded-lg bg-[#f7f7f8] p-3 text-sm" key={index}><strong>Kademe {index + 1}</strong><p className="mt-1">{amount(tier.revenueAmount)} × {percent(tier.rate)} → {amount(tier.fee)}</p></div>)}
      <Row label="Sabit aylık ücret" value={amount(breakdown.baseRetainer)} />
      <Row label="Hesaplanan pay" value={amount(breakdown.calculatedShare)} />
      <Row label="Asgari ücret" value={amount(breakdown.minimumFee)} />
      <Row label="Düzeltmeler" value={amount(breakdown.adjustments)} />
      <Row label="Kayıtlı OVO hakedişi (rapora yansıyan tutar)" value={amount(data.ovoFee)} />
      <Row label="Gerçekleşen oran" value={percent(breakdown.effectiveRate)} />
      {breakdown.finalFee !== data.ovoFee && <p role="alert" className="mt-3 text-sm text-[#8e1f0b]">Hesap dökümü ile kayıtlı hakediş tutarı farklı. Bu kaydı kontrol için yöneticinize bildirin; rapor kayıtlı hakedişi kullanır.</p>}
      <Link href={`/performance/${id}`} className="mt-5 inline-block rounded-lg border px-4 py-2 text-sm font-semibold">Aylık sonucu aç</Link>
    </Card>
    <CollectionPanel id={id} />
  </>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <div className="flex flex-wrap justify-between gap-2 border-b py-3 text-sm"><span>{label}</span><strong>{value}</strong></div>;
}
