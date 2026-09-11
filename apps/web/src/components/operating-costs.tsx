'use client';
import { type ReactNode, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, percent, type SessionUser } from '@/lib/api';
import { Badge, Card } from '@/components/ui/core';
import { dayText, todayText } from '@/components/work-tasks';

type CostEntry = { id: string; kind: string; amount: number; hours: number | null; hourlyCost: number | null; incurredOn: string; reference: string; description: string; createdBy: string; voidedAt: string | null; voidedBy: string; voidReason: string };
type Costs = { id: string; year: number; month: number; status: string; currency: string; ovoFee: number; ovoGrossProfit: number; revision: number; confirmedAt: string | null; confirmedBy: string | null; summary: { plannedCost: number; recordedCost: number; directExpenses: number; teamCost: number; hours: number; costDifference: number; contributionAfterRecordedCosts: number | null; marginAfterRecordedCosts: number | null; complete: boolean }; entries: CostEntry[]; reviews: { id: string; complete: boolean; reason: string; createdBy: string; createdAt: string }[] };
type InvestmentEntry = { id: string; kind: string; amount: number; occurredOn: string; reference: string; description: string; createdBy: string; voidedAt: string | null; voidedBy: string; voidReason: string };
type Investment = { id: string; status: string; currency: string; revision: number; summary: { plannedInvestment: number; recordedInvestment: number; recordedRecovery: number; remaining: number; hasRecords: boolean }; entries: InvestmentEntry[] };

function PrivateCostAccess({ children }: { children: ReactNode }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  if (me.isPending) return <p className="mt-4 text-sm">Maliyet alanı için yetki kontrol ediliyor…</p>;
  if (me.isError) return <p role="alert" className="mt-4 text-sm">Hesap yetkisi okunamadı. Sayfayı yenileyin.</p>;
  return me.data.role === 'Admin' || me.data.role === 'Partner' ? children : null;
}
export function ServiceCosts({ id }: { id: string }) { return <PrivateCostAccess><ServiceCostsContent id={id} /></PrivateCostAccess>; }
export function InvestmentLedger({ id }: { id: string }) { return <PrivateCostAccess><InvestmentContent id={id} /></PrivateCostAccess>; }

function ServiceCostsContent({ id }: { id: string }) {
  const cache = useQueryClient();
  const [message, setMessage] = useState('');
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const query = useQuery({ queryKey: ['service-costs', id], queryFn: () => api<Costs>(`/api/performance/${id}/costs`) });
  const saved = async () => { setMessage('Maliyet takibi güncellendi; kapanmış dönem hesabı değiştirilmedi.'); await cache.invalidateQueries({ queryKey: ['service-costs', id] }); };
  if (query.isPending) return <Card className="mt-5 p-5">Gerçekleşen giderler yükleniyor…</Card>;
  if (query.isError) return <Card className="mt-5 p-5"><p role="alert">{query.error.message}</p><button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></Card>;
  const d = query.data; const s = d.summary; const money = (v: number) => moneyPrecise(v, d.currency);
  const closed = ['Locked', 'Invoiced', 'Paid'].includes(d.status);
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap justify-between gap-3"><h2 className="text-lg font-semibold">OVO gerçekleşen hizmet maliyeti</h2><Badge tone={s.complete ? 'green' : 'yellow'}>{s.complete ? 'Maliyet kontrolü tamamlandı' : 'Maliyetler henüz tamamlanmadı'}</Badge></div>
    <p className="mt-2 text-sm">Yalnız yönetici ve iş ortağı görür. Bu markanın {d.month}/{d.year} dönemi için açıkça girilen KDV hariç giderlerdir; ortak giderler ve vergiler otomatik dağıtılmaz.</p>
    <dl className="my-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">{[
      ['Kapalı dönemdeki tahmini maliyet', money(s.plannedCost)], ['Girilen gerçek gider', money(s.recordedCost)], ['Tahmine göre gider farkı (+ daha fazla)', money(s.costDifference)],
      ['Tahmini maliyetle kayıtlı brüt kâr', money(d.ovoGrossProfit)], ['Gerçek gider sonrası katkı', s.contributionAfterRecordedCosts === null ? 'Maliyet kontrolü bekleniyor' : money(s.contributionAfterRecordedCosts)],
      ['Gerçek gider sonrası katkı oranı', s.marginAfterRecordedCosts === null ? 'Hesaplanmıyor' : percent(s.marginAfterRecordedCosts)]
    ].map(([label, value]) => <div key={label} className="rounded-lg border p-3"><dt className="text-sm">{label}</dt><dd className="mt-1 font-semibold">{value}</dd></div>)}</dl>
    <p className="text-sm">Doğrudan gider: {money(s.directExpenses)} · Ekip çalışması: {s.hours.toLocaleString('tr-TR', { maximumFractionDigits: 4 })} saat / {money(s.teamCost)}. Karşılaştırmada hakedişten yalnız gerçek gider çıkarılır; tahmini gider ikinci kez düşülmez.</p>
    <p className="mt-2 text-xs text-[#6d7175]">Bu sonuç vergi sonrası net kâr değildir. Ana rapordaki kapalı dönem brüt kârı değişmez. Yatırım harcamaları ayrı defterde tutulur; aynı gideri iki yere girmeyin. Çalışma saati maliyeti giren kişinin beyanıdır, bordro veya otomatik maaş hesabı değildir.</p>
    {!closed && <p className="mt-3 text-sm">Maliyet karşılaştırmasına başlamak için aylık sonucu önce onaylayıp kilitleyin.</p>}
    {message && <p className="mt-3 text-sm" role="status">{message}</p>}
    {closed && !s.complete && <CostForm key={d.revision} data={d} onSaved={saved} />}
    {closed && (!s.complete || me.data?.role === 'Admin') && <CostConfirmation key={`confirm-${d.revision}`} data={d} onSaved={saved} />}
    {s.complete && <p className="mt-3 text-sm">Kontrol eden: {d.confirmedBy}. Değişiklik için yönetici gerekçeyle maliyet kontrolünü yeniden açmalıdır.</p>}
    <h3 className="mt-5 font-semibold">Gider geçmişi</h3>{d.entries.length === 0 && <p className="mt-2 text-sm">Henüz gider girilmedi. Bu, giderin sıfır olduğunu kanıtlamaz; gider yoksa kontrolü açıklamasıyla tamamlayın.</p>}
    <div className="mt-3 space-y-3">{d.entries.map(e => <article key={e.id} className="rounded-lg border p-3"><div className="flex flex-wrap justify-between gap-2"><strong>{money(e.amount)} · {dayText(e.incurredOn)}</strong><Badge tone={e.voidedAt ? 'red' : 'neutral'}>{e.voidedAt ? 'İptal edildi' : e.kind === 'TeamWork' ? 'Ekip çalışması' : 'Doğrudan gider'}</Badge></div><p className="mt-2 whitespace-pre-wrap break-words text-sm">{e.description}</p><p className="mt-1 break-words text-xs">Referans: {e.reference} · Kaydeden: {e.createdBy}{e.hours !== null && ` · ${e.hours} saat × ${money(e.hourlyCost ?? 0)}`}</p>{e.voidedAt ? <p className="mt-2 text-sm">İptal: {e.voidReason} · {e.voidedBy}</p> : !s.complete && me.data?.role === 'Admin' && <CostVoid path={`/api/performance/${id}/costs/entries/${e.id}/void`} revision={d.revision} onSaved={saved} />}</article>)}</div>
    {d.reviews.length > 0 && <><h3 className="mt-5 font-semibold">Maliyet kontrol geçmişi</h3>{d.reviews.map(r => <p className="mt-2 rounded-lg border p-3 text-sm" key={r.id}>{r.complete ? 'Tamamlandı' : 'Yeniden açıldı'} · {r.createdBy} · {new Date(r.createdAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })}<br />{r.reason}</p>)}</>}
  </Card>;
}

function CostForm({ data: d, onSaved }: { data: Costs; onSaved: () => Promise<void> }) {
  const [form, setForm] = useState(() => ({ id: crypto.randomUUID(), kind: 'DirectExpense', amount: '', hours: '', hourlyCost: '', incurredOn: '', reference: '', description: '' }));
  const save = useMutation({ mutationFn: () => api(`/api/performance/${d.id}/costs/entries`, { method: 'POST', body: JSON.stringify({ ...form, amount: form.kind === 'DirectExpense' ? Number(form.amount) : 0, hours: form.kind === 'TeamWork' ? Number(form.hours) : null, hourlyCost: form.kind === 'TeamWork' ? Number(form.hourlyCost) : null, revision: d.revision }) }), onSuccess: onSaved });
  return <form className="mt-5 rounded-lg border bg-[#f7f7f8] p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><h3 className="font-semibold">Gider ekle</h3><fieldset disabled={save.isPending} className="mt-3 grid gap-3 sm:grid-cols-2">
    <label>Gider türü<select className="input mt-1" value={form.kind} onChange={e => setForm({ ...form, kind: e.target.value })}><option value="DirectExpense">Doğrudan gider</option><option value="TeamWork">Ekip çalışma süresi</option></select></label>
    <label>Gider tarihi<input className="input mt-1" type="date" required min={`${d.year}-${String(d.month).padStart(2, '0')}-01`} max={todayText()} value={form.incurredOn} onChange={e => setForm({ ...form, incurredOn: e.target.value })} /></label>
    {form.kind === 'DirectExpense' ? <label>Gider tutarı (KDV hariç, {d.currency})<input className="input mt-1" type="number" required min="0.0001" step="0.0001" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} /></label> : <><label>Çalışma süresi (saat)<input className="input mt-1" type="number" required min="0.0001" max="744" step="0.0001" value={form.hours} onChange={e => setForm({ ...form, hours: e.target.value })} /></label><label>Bir saatlik maliyet ({d.currency})<input className="input mt-1" type="number" required min="0.0001" max="1000000" step="0.0001" value={form.hourlyCost} onChange={e => setForm({ ...form, hourlyCost: e.target.value })} /></label></>}
    <label>Gider referansı<input className="input mt-1" required maxLength={160} value={form.reference} onChange={e => setForm({ ...form, reference: e.target.value })} /></label>
    <label className="sm:col-span-2">Gider açıklaması<textarea className="input mt-1" required maxLength={1000} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
    <button className="rounded-lg bg-[#303030] px-3 py-2 text-white">{save.isPending ? 'Kaydediliyor…' : 'Gideri kaydet'}</button>
  </fieldset>{save.isError && <p className="mt-2 text-sm text-red-700" role="alert">{save.error.message}</p>}</form>;
}

function CostConfirmation({ data: d, onSaved }: { data: Costs; onSaved: () => Promise<void> }) {
  const [reason, setReason] = useState('');
  const save = useMutation({ mutationFn: () => api(`/api/performance/${d.id}/costs/confirmation`, { method: 'PUT', body: JSON.stringify({ complete: !d.summary.complete, reason, revision: d.revision }) }), onSuccess: onSaved });
  return <form className="mt-4 rounded-lg border p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><fieldset disabled={save.isPending} className="space-y-3"><label className="block">{d.summary.complete ? 'Maliyet kontrolünü yeniden açma nedeni' : 'Maliyet kontrol sonucu'}<input className="input mt-1" required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label>{!d.summary.complete && <label className="block text-sm"><input className="mr-2" type="checkbox" required />Bu dönemin doğrudan giderlerini ve ekip maliyetlerini kontrol ettim; eksik kalem yok.</label>}<button className="rounded-lg border px-3 py-2 text-sm">{save.isPending ? 'Kaydediliyor…' : d.summary.complete ? 'Maliyet kontrolünü yeniden aç' : 'Maliyet kontrolünü tamamla'}</button></fieldset>{save.isError && <p role="alert" className="mt-2 text-sm text-red-700">{save.error.message}</p>}</form>;
}

function InvestmentContent({ id }: { id: string }) {
  const cache = useQueryClient(); const [message, setMessage] = useState('');
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const query = useQuery({ queryKey: ['investment', id], queryFn: () => api<Investment>(`/api/deals/${id}/investment`) });
  const saved = async () => { setMessage('Yatırım defteri güncellendi. Hakediş veya tahsilat ikinci kez gelir sayılmadı.'); await cache.invalidateQueries({ queryKey: ['investment', id] }); };
  if (query.isPending) return <Card className="mt-5 p-5">Yatırım takibi yükleniyor…</Card>;
  if (query.isError) return <Card className="mt-5 p-5"><p role="alert">{query.error.message}</p><button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></Card>;
  const d = query.data; const money = (v: number) => moneyPrecise(v, d.currency);
  const allowed = ['Accepted', 'Active', 'Expired', 'Terminated'].includes(d.status);
  return <Card className="mt-5 p-5"><h2 className="text-lg font-semibold">Yatırım ve geri kazanım defteri</h2><p className="mt-2 text-sm">Yalnız yönetici ve iş ortağı görür. Anlaşmadaki yatırım bütçesi harcanmış para sayılmaz. Gerçek harcamayı ve geri kazanıma ayırdığınız tutarı dayanağıyla ayrı girin; hakedişlerden otomatik pay ayrılmaz.</p>
    <dl className="my-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{[['Anlaşmadaki yatırım bütçesi', d.summary.plannedInvestment], ['Kayıtlı yatırım harcaması', d.summary.recordedInvestment], ['Açıkça kaydedilen geri kazanım', d.summary.recordedRecovery], ['Geri kazanılacak kayıtlı yatırım', d.summary.remaining]].map(([label, value]) => <div className="rounded-lg border p-3" key={String(label)}><dt className="text-sm">{label}</dt><dd className="mt-1 font-semibold">{money(Number(value))}</dd></div>)}</dl>
    {!d.summary.hasRecords && <p className="text-sm">Gerçek yatırım kaydı yok. Sıfır görünen kalan, yatırımın geri kazanıldığını göstermez.</p>}
    <p className="mt-2 text-xs">Tutarlar KDV hariç ve yalnız bu anlaşmaya aittir. Geri kazanım kaydı yeni tahsilat veya ek gelir yaratmaz; hizmet kârından da tekrar düşülmez. Bu defter, belirtilen kayıtların takibidir; tüm masrafların eksiksiz olduğunu veya banka bakiyesini kanıtlamaz.</p>
    {message && <p className="mt-3 text-sm" role="status">{message}</p>}
    {allowed ? <InvestmentForm key={d.revision} data={d} onSaved={saved} /> : <p className="mt-3 text-sm">Yatırım takibi için anlaşmanın önce kabul edilmesi gerekir.</p>}
    <h3 className="mt-5 font-semibold">Yatırım geçmişi</h3><div className="mt-3 space-y-3">{d.entries.map(e => <article key={e.id} className="rounded-lg border p-3"><div className="flex flex-wrap justify-between gap-2"><strong>{money(e.amount)} · {dayText(e.occurredOn)}</strong><Badge tone={e.voidedAt ? 'red' : e.kind === 'Recovery' ? 'green' : 'neutral'}>{e.voidedAt ? 'İptal edildi' : e.kind === 'Recovery' ? 'Geri kazanım' : 'Yatırım harcaması'}</Badge></div><p className="mt-2 whitespace-pre-wrap break-words text-sm">{e.description}</p><p className="mt-1 break-words text-xs">Referans: {e.reference} · Kaydeden: {e.createdBy}</p>{e.voidedAt ? <p className="mt-2 text-sm">İptal: {e.voidReason} · {e.voidedBy}</p> : me.data?.role === 'Admin' && <CostVoid path={`/api/deals/${id}/investment/entries/${e.id}/void`} revision={d.revision} onSaved={saved} />}</article>)}</div>
  </Card>;
}

function InvestmentForm({ data: d, onSaved }: { data: Investment; onSaved: () => Promise<void> }) {
  const [form, setForm] = useState(() => ({ id: crypto.randomUUID(), kind: 'Investment', amount: '', occurredOn: '', reference: '', description: '' }));
  const save = useMutation({ mutationFn: () => api(`/api/deals/${d.id}/investment/entries`, { method: 'POST', body: JSON.stringify({ ...form, amount: Number(form.amount), revision: d.revision }) }), onSuccess: onSaved });
  return <form className="mt-5 rounded-lg border bg-[#f7f7f8] p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><h3 className="font-semibold">Yatırım kaydı ekle</h3><fieldset disabled={save.isPending} className="mt-3 grid gap-3 sm:grid-cols-2">
    <label>Kayıt türü<select className="input mt-1" value={form.kind} onChange={e => setForm({ ...form, kind: e.target.value })}><option value="Investment">Gerçek yatırım harcaması</option><option value="Recovery">Açıkça ayrılan geri kazanım</option></select></label>
    <label>Tutar (KDV hariç, {d.currency})<input className="input mt-1" type="number" required min="0.0001" step="0.0001" max={form.kind === 'Recovery' ? d.summary.remaining : undefined} value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} /></label>
    <label>Gerçekleşme tarihi<input className="input mt-1" type="date" required min="2020-01-01" max={todayText()} value={form.occurredOn} onChange={e => setForm({ ...form, occurredOn: e.target.value })} /></label>
    <label>Belge veya karar referansı<input className="input mt-1" required maxLength={160} value={form.reference} onChange={e => setForm({ ...form, reference: e.target.value })} /></label>
    <label className="sm:col-span-2">Harcamanın veya geri kazanım kararının dayanağı<textarea className="input mt-1" required maxLength={1000} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
    <label className="text-sm sm:col-span-2"><input className="mr-2" type="checkbox" required />Tutarı ve dayanağını kontrol ettim; aynı harcamayı başka bir gider kaydında kullanmadım.</label>
    <button className="rounded-lg bg-[#303030] px-3 py-2 text-white">{save.isPending ? 'Kaydediliyor…' : 'Yatırım kaydını kaydet'}</button>
  </fieldset>{save.isError && <p role="alert" className="mt-2 text-sm text-red-700">{save.error.message}</p>}</form>;
}

function CostVoid({ path, revision, onSaved }: { path: string; revision: number; onSaved: () => Promise<void> }) {
  const [open, setOpen] = useState(false); const [reason, setReason] = useState('');
  const save = useMutation({ mutationFn: () => api(path, { method: 'POST', body: JSON.stringify({ reason, revision }) }), onSuccess: onSaved });
  return open ? <form className="mt-3" onSubmit={e => { e.preventDefault(); save.mutate(); }}><p className="text-xs">Yalnız yanlış girilmiş kaydı iptal edin. Geçmiş silinmez; aktif toplam yeniden hesaplanır.</p><fieldset disabled={save.isPending} className="mt-2 space-y-2"><label className="block">Hatalı kayıt iptal nedeni<input className="input mt-1" required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><button className="rounded-lg border px-3 py-2 text-sm text-red-700">Kaydı gerekçeyle iptal et</button><button type="button" className="ml-3 text-sm underline" onClick={() => setOpen(false)}>Vazgeç</button></fieldset>{save.isError && <p role="alert" className="mt-2 text-sm text-red-700">{save.error.message}</p>}</form> : <button className="mt-2 text-sm text-red-700 underline" onClick={() => setOpen(true)}>Hatalı kaydı iptal et</button>;
}
