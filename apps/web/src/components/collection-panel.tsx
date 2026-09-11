'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { Badge, Card } from '@/components/ui/core';
import { dayText, todayText } from '@/components/work-tasks';

export type CollectionBalance = { receivable: number; paid: number; outstanding: number; legacyPaid: number; datedPayments: number; overdueDays: number; state: string; needsReview: boolean };
type Payment = { id: string; amount: number; paidOn: string; reference: string; note: string; createdBy: string; voidedAt: string | null; voidedBy: string; voidReason: string };
type Collection = { id: string; status: string; currency: string; balance: CollectionBalance; initialized: boolean; revision: number; invoiceReference: string; invoiceOn: string | null; dueOn: string | null; payments: Payment[] };
export const collectionState = (state: string) => ({ NeedsReview: 'İnceleme gerekli', NotClosed: 'Henüz kapanmadı', Settled: 'Alacak kapandı', PartiallyPaid: 'Kısmen ödendi', Unpaid: 'Ödeme bekleniyor' })[state] ?? 'Kontrol edin';

export function CollectionPanel({ id }: { id: string }) {
  const cache = useQueryClient();
  const [message, setMessage] = useState('');
  const query = useQuery({ queryKey: ['collection', id], queryFn: () => api<Collection>(`/api/performance/${id}/collection`) });
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const saved = async () => {
    setMessage('Tahsilat kaydı güncellendi. Kapalı dönemin hakediş ve kâr hesabı değişmedi.');
    await Promise.all(['collection', 'performance', 'commissions', 'dashboard'].map(key => cache.invalidateQueries({ queryKey: [key] })));
  };
  if (query.isPending) return <Card className="mt-5 p-5">Tahsilat bilgileri yükleniyor…</Card>;
  if (query.isError) return <Card className="mt-5 p-5"><p role="alert">{query.error.message}</p><button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></Card>;
  const data = query.data;
  const manage = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const closed = ['Locked', 'Invoiced', 'Paid'].includes(data.status);
  const amount = (value: number) => moneyPrecise(value, data.currency);
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap justify-between gap-3"><h2 className="text-lg font-semibold">Fatura ve tahsilat takibi</h2><Badge tone={data.balance.needsReview || data.balance.overdueDays ? 'red' : data.balance.state === 'Settled' ? 'green' : 'blue'}>{collectionState(data.balance.state)}</Badge></div>
    <p className="mt-2 text-sm">Tüm tutarlar KDV hariçtir. Bu ekran fatura düzenlemez ve bankaya bağlanmaz; belgenizdeki hakedişe ait ödeme payını kaydedersiniz.</p>
    <dl className="my-4 grid gap-3 sm:grid-cols-3">{[['Kayıtlı hakediş', data.balance.receivable], ['Toplam ödenen', data.balance.paid], ['Kalan alacak', data.balance.outstanding]].map(([label, value]) => <div className="rounded-lg border p-3" key={String(label)}><dt className="text-sm">{label}</dt><dd className="mt-1 font-semibold">{amount(Number(value))}</dd></div>)}</dl>
    {data.balance.legacyPaid > 0 && <p className="mb-3 rounded-lg bg-[#fff8eb] p-3 text-sm">{amount(data.balance.legacyPaid)} eski “ödendi” bilgisinden taşınmıştır. Gerçek ödeme tarihi bilinmediği için aylık para girişi toplamına eklenmez; tekrar ödeme girmeyin.</p>}
    {data.balance.needsReview && <p role="alert" className="mb-3 text-sm">Bu tutar inceleme gerektiriyor. Negatif hakediş, fazla ödeme, gerçek para iadesi ve mahsup bu akışta desteklenmiyor; yöneticinizle görüşün.</p>}
    <p className="text-sm">Fatura referansı: {data.invoiceReference || 'Kayıtlı değil'} · Fatura tarihi: {dayText(data.invoiceOn)} · Vade: {dayText(data.dueOn)}</p>
    {data.balance.overdueDays > 0 && <p className="mt-2 font-semibold text-[#8e1f0b]">Kalan alacak {data.balance.overdueDays} gün gecikmiş. Vade Türkiye’deki bugünün tarihine göre değerlendirilir.</p>}
    {closed && !data.dueOn && data.balance.outstanding > 0 && <p className="mt-2 text-sm">Vade bilinmiyor; bu alacağa gecikmiş denilemiyor.</p>}
    {!closed && <p className="mt-3 text-sm">Tahsilat takibine başlamak için önce dönemi onaylayıp kilitleyin.</p>}
    {message && <p role="status" className="mt-3 text-sm">{message}</p>}
    {manage && closed && !data.balance.needsReview && <InvoiceForm key={`invoice-${data.revision}`} data={data} onSaved={saved} />}
    {manage && data.initialized && data.balance.outstanding > 0 && !data.balance.needsReview && <PaymentForm key={`payment-${data.revision}`} data={data} onSaved={saved} />}
    <h3 className="mt-6 font-semibold">Ödeme geçmişi</h3>
    {!data.payments.length && <p className="mt-2 text-sm">Tarihi ve referansı kayıtlı ödeme yok.</p>}
    <div className="mt-3 space-y-3">{data.payments.map(payment => <article key={payment.id} className="rounded-lg border p-4">
      <div className="flex flex-wrap justify-between gap-2"><strong>{amount(payment.amount)} · {dayText(payment.paidOn)}</strong><Badge tone={payment.voidedAt ? 'red' : 'green'}>{payment.voidedAt ? 'Hatalı kayıt iptal edildi' : 'Ödeme kaydı'}</Badge></div>
      <p className="mt-2 break-words text-sm">Referans: {payment.reference} · Kaydeden: {payment.createdBy}</p><p className="mt-1 whitespace-pre-wrap break-words text-sm">{payment.note}</p>
      {payment.voidedAt ? <p className="mt-2 break-words text-sm">İptal nedeni: {payment.voidReason} · İşlemi yapan: {payment.voidedBy}</p> : me.data?.role === 'Admin' && <VoidForm data={data} payment={payment} onSaved={saved} />}
    </article>)}</div>
    <Link className="mt-4 inline-block text-sm underline" href={`/activity?entityType=MonthlyPerformance&entityId=${id}`}>İşlem geçmişini aç</Link>
  </Card>;
}

function InvoiceForm({ data, onSaved }: { data: Collection; onSaved: () => Promise<void> }) {
  const [reference, setReference] = useState(data.invoiceReference);
  const [invoiceOn, setInvoiceOn] = useState(data.invoiceOn ?? '');
  const [dueOn, setDueOn] = useState(data.dueOn ?? '');
  const [reason, setReason] = useState('');
  const save = useMutation({ mutationFn: () => api(`/api/performance/${data.id}/collection/invoice`, { method: 'PUT', body: JSON.stringify({ reference, invoiceOn: invoiceOn || null, dueOn: dueOn || null, reason, revision: data.revision }) }), onSuccess: onSaved });
  const required = data.status === 'Locked';
  return <form className="mt-5 rounded-lg border bg-[#f7f7f8] p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><h3 className="font-semibold">{data.initialized ? 'Fatura bilgisini güncelle' : 'Fatura takibini başlat'}</h3>
    {!required && <p className="mt-2 text-xs">Eski faturanın bilinmeyen tarihlerini boş bırakabilirsiniz; tahmini tarih yazmayın.</p>}
    <fieldset disabled={save.isPending} className="mt-3 grid gap-3 sm:grid-cols-2">
      <label>Fatura referansı<input className="input mt-1" required={required} maxLength={100} value={reference} onChange={e => setReference(e.target.value)} /></label>
      <label>Fatura tarihi<input className="input mt-1" type="date" required={required} min="2020-01-01" max={todayText()} value={invoiceOn} onChange={e => setInvoiceOn(e.target.value)} /></label>
      <label>Son ödeme tarihi (vade)<input className="input mt-1" type="date" required={required} min={invoiceOn || '2020-01-01'} max="2100-12-31" value={dueOn} onChange={e => setDueOn(e.target.value)} /></label>
      {data.initialized && <label>Değişiklik nedeni<input className="input mt-1" required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label>}
      <button className="rounded-lg border bg-white px-3 py-2 text-sm">{save.isPending ? 'Kaydediliyor…' : data.initialized ? 'Fatura bilgisini kaydet' : 'Faturayı kaydet ve takibi başlat'}</button>
    </fieldset>{save.isError && <p className="mt-2 text-sm text-red-700" role="alert">{save.error.message}</p>}
  </form>;
}

function PaymentForm({ data, onSaved }: { data: Collection; onSaved: () => Promise<void> }) {
  const [id] = useState(() => crypto.randomUUID());
  const [amount, setAmount] = useState('');
  const [paidOn, setPaidOn] = useState('');
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');
  const save = useMutation({ mutationFn: () => api(`/api/performance/${data.id}/collection/payments`, { method: 'POST', body: JSON.stringify({ id, amount: Number(amount), paidOn, reference, note, revision: data.revision }) }), onSuccess: onSaved });
  return <form className="mt-5 rounded-lg border p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><h3 className="font-semibold">Gerçekleşmiş ödeme ekle</h3><p className="mt-2 text-xs">Tek ödeme referansını yalnızca bir hakedişte kullanın. Bankadaki KDV dahil toplamı buraya yazmayın; bu hakedişe ait KDV hariç payı girin.</p>
    <fieldset disabled={save.isPending} className="mt-3 grid gap-3 sm:grid-cols-2">
      <label>Ödeme tutarı (KDV hariç, {data.currency})<input className="input mt-1" type="number" inputMode="decimal" required min="0.0001" max={data.balance.outstanding} step="0.0001" value={amount} onChange={e => setAmount(e.target.value)} /></label>
      <label>Gerçek ödeme tarihi<input className="input mt-1" type="date" required min="2020-01-01" max={todayText()} value={paidOn} onChange={e => setPaidOn(e.target.value)} /></label>
      <label>Ödeme referansı<input className="input mt-1" required maxLength={160} value={reference} onChange={e => setReference(e.target.value)} /></label>
      <label>Ödeme notu<input className="input mt-1" maxLength={1000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <label className="text-sm sm:col-span-2"><input type="checkbox" required className="mr-2" />Ödemenin gerçekleştiğini ve KDV hariç tutarı kontrol ettim.</label>
      <button className="rounded-lg bg-[#303030] px-3 py-2 text-white">{save.isPending ? 'Kaydediliyor…' : 'Ödemeyi kaydet'}</button>
    </fieldset>{save.isError && <p className="mt-2 text-sm text-red-700" role="alert">{save.error.message}</p>}
  </form>;
}

function VoidForm({ data, payment, onSaved }: { data: Collection; payment: Payment; onSaved: () => Promise<void> }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const save = useMutation({ mutationFn: () => api(`/api/performance/${data.id}/collection/payments/${payment.id}/void`, { method: 'POST', body: JSON.stringify({ reason, revision: data.revision }) }), onSuccess: onSaved });
  return open ? <form className="mt-3" onSubmit={e => { e.preventDefault(); save.mutate(); }}><p className="mb-2 text-sm">Bu işlem yanlış girilmiş kaydı iptal eder; gerçek banka iadesi değildir. Kalan alacak artar, dönem finansal olarak kapalı kalır.</p><fieldset disabled={save.isPending} className="space-y-2"><label className="block">İptal nedeni<input className="input mt-1" required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><button className="rounded-lg border border-red-700 px-3 py-2 text-sm text-red-700">Hatalı kaydı gerekçeyle iptal et</button><button type="button" className="ml-3 text-sm underline" onClick={() => setOpen(false)}>Vazgeç</button></fieldset>{save.isError && <p role="alert" className="mt-2 text-sm text-red-700">{save.error.message}</p>}</form> : <button className="mt-3 text-sm text-red-700 underline" onClick={() => setOpen(true)}>Hatalı ödeme kaydını iptal et</button>;
}
