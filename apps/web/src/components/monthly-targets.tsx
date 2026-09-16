'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, percent, type SessionUser } from '@/lib/api';
import { periodNumber, targetMargin } from '@/lib/period-input';
import { turkce } from '@/lib/turkish';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { todayText, dayText, type TeamMember, type Paged } from '@/components/work-tasks';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Metric = 'NetRevenue' | 'AdSpend' | 'ContributionMargin';
type Target = { id: string; year: number; month: number; currency: string; netRevenueGoal: number; adBudget: number; contributionMarginGoal: number; ownerId: string; revision: number; updatedAt: string };
type Result = { metric: Metric; target: number; actual: number | null; difference: number | null; relativeDifference: number | null; percentagePointDifference: number | null; needsAttention: boolean | null };
type TargetData = { brandName: string; target: Target | null; owner?: { name: string; isActive: boolean }; comparison: { performanceId: string | null; performanceUpdatedAt: string | null; status: string | null; isClosed: boolean; missingReason: string | null; metrics: Result[] } | null; actions?: { metric: Metric; taskId: string; targetRevision: number; completedAt: string | null; dueOn: string }[] };
type History = { id: string; userId: string; createdAt: string; reason: string; oldValueJson: string | null; newValueJson: string };
const labels: Record<Metric, string> = { NetRevenue: 'Net ciro', AdSpend: 'Reklam gideri', ContributionMargin: 'Markaya kalan katkı marjı' };
const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';
const localNumber = (n: number) => new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 4, useGrouping: false }).format(n);
const stamp = (s: string) => new Date(s).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' });

export function MonthlyTargets({ brandId }: { brandId: string }) {
  const [period, setPeriod] = useState(() => todayText().slice(0, 7)); const [currency, setCurrency] = useState('');
  const [editing, setEditing] = useState(false); const [action, setAction] = useState<{ data: TargetData; metric: Metric } | null>(null);
  const [message, setMessage] = useState(''); const cache = useQueryClient();
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const brand = useQuery({ queryKey: ['target-brand', brandId], queryFn: () => api<{ name: string; currency: string }>(`/api/brands/${brandId}`) });
  const selectedCurrency = currency || brand.data?.currency || 'TRY';
  const valid = /^20\d{2}-(0[1-9]|1[0-2])$|^2100-(0[1-9]|1[0-2])$/.test(period) && Number(period.slice(0, 4)) >= 2020 && /^[A-Z]{3}$/.test(selectedCurrency);
  const path = `/api/brands/${brandId}/targets/${period.replace('-', '/')}/${selectedCurrency}`;
  const query = useQuery({ queryKey: ['monthly-target', brandId, period, selectedCurrency], queryFn: () => api<TargetData>(path), enabled: valid && !!brand.data, refetchOnWindowFocus: false });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner'; const data = query.data;
  function refreshed(text: string) { setEditing(false); setAction(null); setMessage(text); cache.invalidateQueries({ queryKey: ['monthly-target'] }); cache.invalidateQueries({ queryKey: ['target-history'] }); cache.invalidateQueries({ queryKey: ['work-tasks'] }); cache.invalidateQueries({ queryKey: ['tasks'] }); }
  return <>
    <PageHeader title="Aylık hedef ve bütçe" description={`${brand.data?.name ?? 'Marka'} · Hedef belirleyin, gerçekleşen sonuçla karşılaştırın ve takip işini atayın.`} />
    <div className="mb-4 flex flex-wrap gap-4 text-sm"><Link className="underline" href={`/brands/${brandId}`}>Markaya dön</Link><Link className="underline" href={`/reports/brands/${brandId}`}>Açıklamalı marka raporu</Link><Link className="underline" href="/guide#aylik-hedef-ve-butce">Nasıl kullanılır?</Link></div>
    <Card className="mb-4 p-5"><fieldset disabled={editing || !!action} className="flex flex-wrap gap-4"><label>Hedef ayı<input type="month" required min="2020-01" max="2100-12" className="input mt-1" value={period} onChange={e => { setPeriod(e.target.value); setMessage(''); }} /></label><label>Para birimi<input className="input mt-1 w-28" maxLength={3} value={selectedCurrency} onChange={e => { setCurrency(e.target.value.toUpperCase()); setMessage(''); }} /></label><button className={button} disabled={!valid || query.isFetching} onClick={() => query.refetch()}>Son durumu yenile</button></fieldset>
      <p className="mt-3 text-sm">TRY, USD ve EUR gibi para birimlerini ayrı izleyin. Bu hedefler işletme planıdır; anlaşma ücretini, hakedişi veya reklam hesabındaki bütçeyi değiştirmez. Müşteri portalında paylaşılmaz.</p></Card>
    {message && <p role="status" className="mb-4">{message}</p>}
    {!valid ? <p role="alert">Geçerli bir ay ve üç harfli para birimi seçin.</p> : brand.isError || query.isError || me.isError ? <p role="alert">{brand.error?.message || query.error?.message || me.error?.message}</p> : query.isPending ? <p role="status">Hedefler yükleniyor…</p> : data && <>
      {!data.target ? <Card className="p-5"><h2 className="font-semibold">Bu ay için hedef belirlenmedi</h2><p className="my-3">Hedef yokluğu sıfır hedef anlamına gelmez. Karşılaştırma için önce ciro hedefini, reklam bütçesini, katkı marjını ve sorumlusunu belirleyin.</p>{canManage && !editing && <button className={button} onClick={() => setEditing(true)}>Hedef belirle</button>}</Card> : <>
        <Card className="mb-4 p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{data.target.month}/{data.target.year} · {data.target.currency} · Hedef sürümü {data.target.revision}</h2>{canManage && !editing && !action && <button className={button} onClick={() => setEditing(true)}>Hedefi gerekçeyle değiştir</button>}</div><p className="mt-3 text-sm">Sorumlu: {data.owner?.name}{data.owner && !data.owner.isActive && ' (hesabı kapalı; yeni sorumlu seçin)'} · Son değişiklik: {stamp(data.target.updatedAt)}</p>
          <p className="mt-2 text-sm">{data.comparison?.missingReason || (data.comparison?.isClosed ? 'Kapanmış dönemin kayıtlı sonucu ile karşılaştırılıyor.' : `Geçici sonuç (${turkce(data.comparison?.status ?? '')}): ay kapanınca değişebilir.`)}</p>{data.comparison?.performanceId && <Link href={`/performance/${data.comparison.performanceId}`} className="mt-2 inline-block text-sm underline">Kaynak aylık sonucu aç</Link>}
          <p className="mt-3 text-sm">Katkı marjı, OVO hakedişi çıktıktan sonra markaya kalan katkının net ciroya oranıdır. Net ciro sıfır veya negatifse marj hesaplanmaz. Bu oran şirketin tüm giderleri sonrası net kârı değildir.</p></Card>
        <div className="grid gap-4 lg:grid-cols-3">{data.comparison?.metrics.map(row => {
          const amount = (value: number | null) => value === null ? 'Veri yok' : row.metric === 'ContributionMargin' ? percent(value) : moneyPrecise(value, selectedCurrency);
          const task = data.actions?.find(a => a.metric === row.metric);
          return <Card className="p-5" key={row.metric}><h3 className="mb-3 font-semibold">{labels[row.metric]}</h3><Badge tone={row.needsAttention === null ? 'neutral' : row.needsAttention ? 'yellow' : 'green'}>{row.needsAttention === null ? 'Karşılaştırılamıyor' : row.needsAttention ? 'Takip gerekli' : 'Hedef sınırında veya daha iyi'}</Badge>
            <dl className="my-4 space-y-2 text-sm"><div><dt>{row.metric === 'AdSpend' ? 'Bütçe üst sınırı' : 'Hedef alt sınırı'}</dt><dd className="font-semibold">{amount(row.target)}</dd></div><div><dt>Gerçekleşen</dt><dd className="font-semibold">{amount(row.actual)}</dd></div><div><dt>Gerçekleşen − hedef</dt><dd>{row.metric === 'ContributionMargin' ? row.percentagePointDifference === null ? 'Hesaplanamıyor' : `${localNumber(row.percentagePointDifference)} yüzde puan` : amount(row.difference)}</dd></div>{row.metric !== 'ContributionMargin' && <div><dt>Hedefe göre yüzde farkı</dt><dd>{row.relativeDifference === null ? 'Uygun baz yok; hesaplanmadı' : percent(row.relativeDifference)}</dd></div>}</dl>
            <p className="mb-3 text-xs">{row.metric === 'AdSpend' ? 'Bütçe altı harcama tek başına başarı anlamına gelmez; ciroyu ve marjı da kontrol edin.' : 'Hedef altı sonuçta nedeni kaynak verilerle araştırın; sistem neden tahmini yapmaz.'}</p>
            {task ? <p className="text-sm">Takip işi {task.completedAt ? 'tamamlandı' : 'açık'} · {dayText(task.dueOn)} · Hedef sürümü {task.targetRevision}. <Link className="underline" href={`/brands/${brandId}#team-work`}>Mevcut işi aç</Link></p> : canManage && row.needsAttention && <button className={button} disabled={editing || !!action} onClick={() => setAction({ data, metric: row.metric })}>Takip işi oluştur: {labels[row.metric]}</button>}
          </Card>;
        })}</div>
      </>}
      {editing && <TargetForm path={path} target={data.target} onClose={() => setEditing(false)} onSaved={() => refreshed('Hedef gerekçesiyle kaydedildi. Finansal sonuçlar değiştirilmedi.')} />}
      {action && <ActionForm data={action.data} metric={action.metric} onClose={() => setAction(null)} onSaved={() => refreshed('Takip işi oluşturuldu. Markanın görevleri alanından takip edebilirsiniz.')} />}
      {data.target && <TargetHistory key={data.target.id} id={data.target.id} />}
    </>}
  </>;
}

function TargetForm({ path, target, onSaved, onClose }: { path: string; target: Target | null; onSaved: () => void; onClose: () => void }) {
  const [baseline] = useState(target); const [dirty, setDirty] = useState(false); const [error, setError] = useState('');
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  useUnsavedChanges(dirty);
  const save = useMutation({ mutationFn: (body: object) => api(path, { method: 'PUT', body: JSON.stringify(body) }), onSuccess: () => { setDirty(false); onSaved(); } });
  return <Card className="mt-4 p-5"><form onChange={() => setDirty(true)} onSubmit={e => { e.preventDefault(); setError(''); const form = new FormData(e.currentTarget); try {
    save.mutate({ netRevenueGoal: periodNumber(String(form.get('revenue'))), adBudget: periodNumber(String(form.get('budget'))), contributionMarginGoal: targetMargin(String(form.get('margin'))), ownerId: form.get('owner'), reason: form.get('reason'), revision: baseline?.revision ?? 0 });
  } catch (e) { setError(e instanceof Error ? e.message : 'Girişleri kontrol edin.'); } }}>
    <h2 className="mb-3 font-semibold">{baseline ? 'Hedef değişikliği' : 'Yeni hedef'}</h2><p className="mb-4 text-sm">Tutar: 1.234,56. Marj: 30 yazarsanız %30 olur. Bilmediğiniz alanı sıfırla doldurmayın. Sıfır bilinçli bir hedef/bütçe olarak kaydedilir.</p>
    <fieldset disabled={save.isPending} className="grid gap-4 sm:grid-cols-2">
      <label>Net ciro hedefi<input className="input mt-1" name="revenue" inputMode="decimal" required defaultValue={baseline ? localNumber(baseline.netRevenueGoal) : ''} /></label>
      <label>Reklam bütçesi<input className="input mt-1" name="budget" inputMode="decimal" required defaultValue={baseline ? localNumber(baseline.adBudget) : ''} /></label>
      <label>Katkı marjı hedefi (%)<input className="input mt-1" name="margin" inputMode="decimal" required defaultValue={baseline ? localNumber(baseline.contributionMarginGoal * 100) : ''} /></label>
      <label>Hedef sorumlusu<select className="input mt-1" name="owner" required defaultValue={baseline?.ownerId ?? ''}><option value="">Çalışan seçin</option>{team.data?.map(p => <option value={p.id} key={p.id} disabled={!p.isActive}>{p.name}{!p.isActive && ' (kapalı hesap)'}</option>)}</select></label>
      <label className="sm:col-span-2">Belirleme veya değişiklik nedeni<textarea className="input mt-1" name="reason" required minLength={5} maxLength={1000} rows={3} /></label>
      <label className="sm:col-span-2 flex items-start gap-2"><input type="checkbox" required className="mt-1" /> Hedefleri kontrol ettim. Değişiklik geçmişe kaydedilecek; hakediş ve gerçekleşen rakamlar değişmeyecek.</label>
      <div className="flex gap-3"><button className={button} disabled={!team.data || team.isError}>{save.isPending ? 'Kaydediliyor…' : 'Hedefi kaydet'}</button><button type="button" className={button} onClick={() => { if (!dirty || confirm('Değişiklikleri kaydetmeden kapatmak istiyor musunuz?')) onClose(); }}>Vazgeç</button></div>
    </fieldset>{(error || save.error || team.error) && <p className="mt-3 text-red-700" role="alert">{error || save.error?.message || team.error?.message}</p>}
  </form></Card>;
}

function ActionForm({ data, metric, onSaved, onClose }: { data: TargetData; metric: Metric; onSaved: () => void; onClose: () => void }) {
  const [dirty, setDirty] = useState(false); useUnsavedChanges(dirty);
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  const save = useMutation({ mutationFn: (body: object) => api(`/api/targets/${data.target!.id}/actions`, { method: 'POST', body: JSON.stringify(body) }), onSuccess: () => { setDirty(false); onSaved(); } });
  return <Card className="mt-4 p-5"><form onChange={() => setDirty(true)} onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); save.mutate({ metric, targetRevision: data.target!.revision, performanceUpdatedAt: data.comparison!.performanceUpdatedAt, assigneeId: form.get('assignee'), dueOn: form.get('due'), description: form.get('description') }); }}>
    <h2 className="mb-3 font-semibold">{labels[metric]} için takip işi</h2><p className="mb-4 text-sm">Hedef sürümü {data.target!.revision} ve şu anki {data.comparison?.isClosed ? 'kapanmış' : 'geçici'} sonuç göreve not edilir. İş zaten varsa yenisi açılmaz; tamamlanan işi gerekirse yeniden açın.</p>
    <fieldset disabled={save.isPending} className="grid gap-4 sm:grid-cols-2"><label>Takip sorumlusu<select className="input mt-1" name="assignee" required defaultValue={data.target!.ownerId}><option value="">Çalışan seçin</option>{team.data?.map(p => <option value={p.id} key={p.id} disabled={!p.isActive}>{p.name}{!p.isActive && ' (kapalı hesap)'}</option>)}</select></label><label>Son tarih<input className="input mt-1" type="date" name="due" required min="2020-01-01" max="2100-12-31" /></label><label className="sm:col-span-2">Ne yapılacak?<textarea className="input mt-1" name="description" required minLength={5} maxLength={2000} rows={3} /></label><div className="flex gap-3"><button className={button} disabled={!team.data || team.isError}>{save.isPending ? 'Oluşturuluyor…' : 'Takip işini kaydet'}</button><button className={button} type="button" onClick={() => { if (!dirty || confirm('Yazdıklarınızı kaydetmeden kapatmak istiyor musunuz?')) onClose(); }}>Vazgeç</button></div></fieldset>{(save.error || team.error) && <p className="mt-3 text-red-700" role="alert">{save.error?.message || team.error?.message}</p>}
  </form></Card>;
}

function TargetHistory({ id }: { id: string }) {
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ['target-history', id, page], queryFn: () => api<Paged<History>>(`/api/targets/${id}/history?page=${page}`) });
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  function snapshot(json: string | null) {
    if (!json) return 'İlk hedef; önceki kayıt yok.';
    try { const t = JSON.parse(json) as Target; return `Net ciro: ${moneyPrecise(t.netRevenueGoal, t.currency)} · Reklam bütçesi: ${moneyPrecise(t.adBudget, t.currency)} · Katkı marjı: ${percent(t.contributionMarginGoal)} · Sorumlu: ${team.data?.find(p => p.id === t.ownerId)?.name ?? 'Kayıtlı çalışan'} · Sürüm ${t.revision}`; }
    catch { return 'Bu eski kaydın ayrıntıları görüntülenemiyor.'; }
  }
  return <Card className="mt-4 p-5"><h2 className="mb-3 font-semibold">Hedef değişiklik geçmişi</h2>{query.isPending ? <p role="status">Geçmiş yükleniyor…</p> : query.isError ? <p role="alert">{query.error.message}</p> : <><div className="space-y-4">{query.data.items.map(h => <article key={h.id} className="rounded-lg border p-4 text-sm"><p className="font-semibold">{stamp(h.createdAt)} · {h.userId}</p><p className="mt-2 whitespace-pre-wrap break-words">Gerekçe: {h.reason}</p><p className="mt-2">Önce: {snapshot(h.oldValueJson)}</p><p className="mt-2">Sonra: {snapshot(h.newValueJson)}</p></article>)}</div><div className="mt-4 flex justify-between text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki</button><span>{query.data.total} değişiklik · Sayfa {page}</span><button disabled={page * query.data.pageSize >= query.data.total} onClick={() => setPage(page + 1)}>Sonraki</button></div></>}</Card>;
}
