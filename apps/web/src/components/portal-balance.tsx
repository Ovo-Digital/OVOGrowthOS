'use client';
import { useQuery } from '@tanstack/react-query';
import { api, moneyPrecise } from '@/lib/api';
import { Badge, Card, EmptyState, LoadingState } from '@/components/ui/core';

type PromiseInfo = { amount: number; promisedOn: string; remaining: number; state: string };
type BalanceItem = { year: number; month: number; currency: string; receivable: number; paid: number; outstanding: number; overdueDays: number; dueOn: string | null; promise: PromiseInfo | null };
type BalanceResponse = { items: BalanceItem[]; totals: { currency: string; outstanding: number; overdue: number }[] };

const promiseStates: Record<string, string> = { Waiting: 'Bekliyor', Overdue: 'Vadesi geçti', Covered: 'Karşılandı', NeedsReview: 'İnceleme gerekli', Cancelled: 'İptal edildi' };

export function PortalBalance() {
  const query = useQuery({ queryKey: ['portal-balance'], queryFn: () => api<BalanceResponse>('/api/portal/balance'), retry: false, refetchInterval: 30000 });
  if (query.isError) return null;
  if (query.isPending) return <Card className="mt-5 p-5 print:hidden"><LoadingState label="Bakiye yükleniyor…" /></Card>;
  const { items, totals } = query.data;
  return <Card className="mt-5 p-5 print:hidden">
    <h2 className="font-semibold">Açık bakiye ve ödeme sözleri</h2>
    <p className="mt-1 text-sm text-[#6d7175]">Kapanmış dönemlerdeki güncel açık alacak ve kayıtlı ödeme sözleriniz. Tutarlar KDV hariçtir; banka bakiyesi veya tahsilat garantisi değildir. Bu alandan ödeme yapılmaz ve onay verilmez; sorunuz varsa bir raporun altından soru gönderin.</p>
    {items.length === 0
      ? <EmptyState message="Açık bakiye görünmüyor. Kapanmış hiçbir dönemde ödenmemiş kalan tutar yok." />
      : <>
        <div className="mt-3 flex flex-wrap gap-2">{totals.map(t => <Badge key={t.currency} tone={t.overdue > 0 ? 'red' : 'yellow'}>{`Toplam açık ${moneyPrecise(t.outstanding, t.currency)}${t.overdue > 0 ? ` · bunun ${moneyPrecise(t.overdue, t.currency)} vadesi geçmiş` : ''}`}</Badge>)}</div>
        <div className="mt-4 overflow-x-auto"><table className="w-full min-w-[720px] text-left text-sm">
          <thead><tr>{['Dönem', 'Alacak', 'Ödenen', 'Açık bakiye', 'Vade', 'Ödeme sözü'].map(h => <th className="border-b py-2 pr-3" key={h}>{h}</th>)}</tr></thead>
          <tbody>{items.map(x => <tr key={`${x.year}-${x.month}`}>
            <td className="border-b py-2 pr-3 font-semibold">{x.month}/{x.year}</td>
            <td className="border-b py-2 pr-3">{moneyPrecise(x.receivable, x.currency)}</td>
            <td className="border-b py-2 pr-3">{moneyPrecise(x.paid, x.currency)}</td>
            <td className="border-b py-2 pr-3 font-semibold">{moneyPrecise(x.outstanding, x.currency)}</td>
            <td className="border-b py-2 pr-3">{x.dueOn ?? '—'}{x.overdueDays > 0 && <span className="text-[#8e1f0b]"> · {x.overdueDays} gün gecikti</span>}</td>
            <td className="border-b py-2">{x.promise ? <>{moneyPrecise(x.promise.remaining, x.currency)} · {x.promise.promisedOn} · {promiseStates[x.promise.state] ?? x.promise.state}</> : '—'}</td>
          </tr>)}</tbody>
        </table></div>
        <p className="mt-3 text-xs text-[#6d7175]">Yalnız kapanmış dönemlerin bakiyesi borç sayılır; taslak veya onay bekleyen dönemler bu listeye girmez. Ödeme sözü, markanızın bildirdiği beklenen tutar ve tarihtir; ödeme garantisi değildir. Paylaşılan raporlar yayımlanma anındaki donmuş değerlerdir; bu tablo ise bugünün durumunu gösterir.</p>
      </>}
  </Card>;
}
