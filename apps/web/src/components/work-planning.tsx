'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, percent, type SessionUser } from '@/lib/api';
import { planningHours } from '@/lib/period-input';
import { planningWeek } from '@/lib/planning-week';
import { turkceTarih } from '@/lib/turkish';
import { Badge, Card, PageHeader } from '@/components/ui/core';
import { notify } from '@/components/feedback';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';
const number = (v: number) => new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2, useGrouping: false }).format(v);
const hours = (v: number | null) => v === null ? 'Bilgi yok' : `${number(v)} saat`;
type Capacity = { id: string; workingHours: number; unavailableHours: number; revision: number };
type Result = { availableHours: number | null; plannedHours: number; completedTaskPlannedHours: number; remainingHours: number | null; loadRatio: number | null; overloaded: boolean | null };
type Row = { user: { id: string; name: string; isActive: boolean }; capacity: Capacity | null; result: Result; preview: Result | null; dueWithoutPlan: number; plans: { id: string; taskId: string; title: string; hours: number; brandId: string; completedAt: string | null }[] };
type TaskPlan = { task: { id: string; title: string; assigneeName: string; assigneeActive: boolean; revision: number; completedAt: string | null }; plan: { id: string; hours: number; revision: number } | null };

export function WeeklyPlanning() {
  const [week, setWeek] = useState(() => planningWeek()); const [editing, setEditing] = useState<Row | null>(null);
  const [preview, setPreview] = useState<{ userId: string; hours: number } | null>(null); const [message, setMessage] = useState('');
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner'; const cache = useQueryClient();
  const query = useQuery({ queryKey: ['work-planning', week, preview], queryFn: () => api<{ rows: Row[] }>(`/api/work-planning?weekStart=${week}${preview ? `&previewUserId=${preview.userId}&additionalHours=${preview.hours}` : ''}`), enabled: !!week, refetchOnWindowFocus: false });
  return <><PageHeader title="Haftalık ekip kapasitesi" description="Kayıtlı çalışma saatleriyle görev planlarını karşılaştırın. Bu ekran bordro veya çalışan performans puanı değildir." />
    <div className="mb-4 flex flex-wrap gap-4 text-sm"><Link href="/work" className="underline">İşlerime dön</Link><Link href="/guide#gorev-sablonlari-ve-ekip-kapasitesi" className="underline">Nasıl kullanılır?</Link></div>
    <Card className="mb-4 p-5"><label className="block max-w-xs">Haftanın pazartesi günü<input className="input mt-1" type="date" min="2020-01-06" max="2100-12-27" step={7} required disabled={!!editing} value={week} onChange={e => { setWeek(e.target.value); setPreview(null); setMessage(''); }} /></label><button className={`${button} mt-3`} disabled={!week || query.isFetching || !!editing} onClick={() => query.refetch()}>Planı yenile</button>
      <p className="mt-3 text-sm">Kullanılabilir saat = çalışma saati − başka işlere ayrılmış/kullanılamayan saat. Tamamlanan görevin planı haftalık toplamda kalır; harcanan gerçek süre kabul edilmez. Eski toplam hizmet saatleri çalışanlara tahminen dağıtılmaz.</p><p className="mt-2 text-sm">Göreve saat eklemek için markanın görev alanında <strong>Haftalık saat planı</strong> düğmesini kullanın. Görev sorumlusu değişirse kayıtlı plan yeni sorumluda görünür; eski haftaları değiştirirken bu etkiyi de kontrol edin.</p></Card>
    {message && <p role="status" className="mb-3">{message}</p>}
    {!week ? <p>Bir hafta seçin.</p> : query.isPending ? <p role="status">Ekip planı yükleniyor…</p> : query.isError ? <p role="alert">{query.error.message}</p> : <>
      <Card className="mb-4 p-5"><h2 className="font-semibold">Yeni marka veya ek iş ön izlemesi</h2><p className="my-3 text-sm">Bir çalışana ek saat gelirse ne olur? Bu deneme kaydedilmez ve görev oluşturmaz. Birden fazla kişi için ayrı ayrı inceleyin.</p><form className="flex flex-wrap items-end gap-3" onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); try { setPreview({ userId: String(f.get('person')), hours: planningHours(String(f.get('extra'))) }); setMessage('Yalnız ön izleme; hiçbir saat planı kaydedilmedi.'); } catch (e) { setMessage(e instanceof Error ? e.message : 'Saati kontrol edin.'); } }}>
        <label>Ön izlenecek çalışan<select name="person" className="input mt-1" required defaultValue={preview?.userId ?? ''}><option value="">Çalışan seçin</option>{query.data.rows.filter(r => r.user.isActive).map(r => <option key={r.user.id} value={r.user.id}>{r.user.name}</option>)}</select></label><label>Ek iş saati<input name="extra" className="input mt-1" inputMode="decimal" required placeholder="Örneğin 8,5" defaultValue={preview ? number(preview.hours) : ''} /></label><button className={button} disabled={!!editing}>Etkisini göster</button>{preview && <button className={button} type="button" disabled={!!editing} onClick={() => setPreview(null)}>Ön izlemeyi temizle</button>}</form></Card>
      {editing && <CapacityForm row={editing} week={week} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); setMessage('Haftalık kapasite kaydedildi. Mali kayıtlar değiştirilmedi.'); cache.invalidateQueries({ queryKey: ['work-planning'] }); }} />}
      <div className="grid gap-4 xl:grid-cols-2">{query.data.rows.map(row => <Card key={row.user.id} className="p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{row.user.name}{!row.user.isActive && ' (kapalı hesap)'}</h2><Badge tone={row.result.overloaded === null ? 'neutral' : row.result.overloaded ? 'red' : 'green'}>{row.result.overloaded === null ? 'Kapasite girilmedi' : row.result.overloaded ? 'Plan kapasiteyi aşıyor' : 'Kayıtlı plan kapasite içinde'}</Badge></div>
        <dl className="my-4 grid grid-cols-2 gap-3 text-sm">{[['Çalışma saati', hours(row.capacity?.workingHours ?? null)], ['Kullanılamayan saat', hours(row.capacity?.unavailableHours ?? null)], ['Kullanılabilir', hours(row.result.availableHours)], ['Planlanan toplam', hours(row.result.plannedHours)], ['Kalan kapasite', hours(row.result.remainingHours)], ['Doluluk', row.result.loadRatio === null ? 'Hesaplanamıyor' : percent(row.result.loadRatio)], ['Tamamlanan işlerin planı', hours(row.result.completedTaskPlannedHours)]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd className="font-semibold">{value}</dd></div>)}</dl>
        <p className="text-sm">{row.dueWithoutPlan} açık görevin son tarihi bu hafta; saat planı girilmemiş. Boş kapasite yalnız kayıtlı planları kapsar.</p>
        {row.preview && <div className="mt-3 rounded-lg border border-amber-400 p-3 text-sm"><strong>Kaydedilmeyen ek iş denemesi</strong><p>Toplam: {hours(row.preview.plannedHours)} · Kalan: {hours(row.preview.remainingHours)} · {row.preview.overloaded === null ? 'Kapasite bilinmiyor' : row.preview.overloaded ? 'Kapasite aşılır' : 'Kayıtlı kapasiteye sığıyor'}</p></div>}
        <div className="mt-3 flex flex-wrap gap-3">{canManage && row.user.isActive && <button className={button} disabled={!!editing} onClick={() => setEditing(row)}>Kapasiteyi düzenle: {row.user.name}</button>}{row.capacity && <Link className="text-sm underline" href={`/activity?entityType=WeeklyCapacity&entityId=${row.capacity.id}`}>Değişiklik geçmişi</Link>}</div>
        <ul className="mt-4 space-y-2 text-sm">{row.plans.map(p => <li key={p.id}><Link className="underline" href={`/brands/${p.brandId}#team-work`}>{p.title}</Link> · {hours(p.hours)}{p.completedAt && ' · Görev tamamlandı'} · <Link className="underline" href={`/activity?entityType=TaskHourPlan&entityId=${p.id}`}>Plan geçmişi</Link></li>)}</ul>
      </Card>)}</div>{query.data.rows.length === 0 && <p>Henüz iç ekip hesabı yok.</p>}
    </>}</>;
}

function CapacityForm({ row, week, onSaved, onClose }: { row: Row; week: string; onSaved: () => void; onClose: () => void }) {
  const [dirty, setDirty] = useState(false); const [error, setError] = useState(''); useUnsavedChanges(dirty);
  const save = useMutation({ mutationFn: (body: object) => api(`/api/team/${row.user.id}/capacity`, { method: 'PUT', body: JSON.stringify(body) }), onSuccess: () => { setDirty(false); onSaved(); } });
  return <Card className="mb-4 p-5"><h2 className="font-semibold">{row.user.name} · {week} haftası</h2><p className="my-3 text-sm">İzin, başka projeler ve toplantılar için kullanılamayan toplam saati ayırın; özel izin nedeni veya sağlık bilgisi yazmayın. Bilgi yoksa sıfırla doldurmayın.</p>
    <form onChange={() => setDirty(true)} onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); setError(''); try { save.mutate({ weekStart: week, workingHours: planningHours(String(f.get('working'))), unavailableHours: planningHours(String(f.get('unavailable'))), reason: f.get('reason'), revision: row.capacity?.revision ?? 0 }); } catch (e) { setError(e instanceof Error ? e.message : 'Saatleri kontrol edin.'); } }}>
      <fieldset disabled={save.isPending} className="grid gap-3 sm:grid-cols-2"><label>Haftalık çalışma saati<input className="input mt-1" name="working" inputMode="decimal" required defaultValue={row.capacity ? number(row.capacity.workingHours) : ''} /></label><label>Kullanılamayan saat<input className="input mt-1" name="unavailable" inputMode="decimal" required defaultValue={row.capacity ? number(row.capacity.unavailableHours) : ''} /></label><label className="sm:col-span-2">Planlama açıklaması<textarea className="input mt-1" name="reason" required minLength={5} maxLength={1000} /></label><div className="flex gap-3"><button className={button}>Kapasiteyi kaydet</button><button className={button} type="button" onClick={() => { if (!dirty || confirm('Kaydetmeden kapatmak istiyor musunuz?')) onClose(); }}>Vazgeç</button></div></fieldset>
      {(error || save.error) && <p role="alert" className="mt-3 text-red-700">{error || save.error?.message}</p>}
    </form></Card>;
}

export function TaskHourPanel({ taskId, canManage, onClose }: { taskId: string; canManage: boolean; onClose: () => void }) {
  const [week, setWeek] = useState(() => planningWeek()); const [editing, setEditing] = useState(false); const [message, setMessage] = useState(''); const cache = useQueryClient();
  const query = useQuery({ queryKey: ['task-hour-plan', taskId, week], queryFn: () => api<TaskPlan>(`/api/work-tasks/${taskId}/hour-plan?weekStart=${week}`), enabled: !!week, refetchOnWindowFocus: false });
  return <Card className="my-4 p-5"><div className="flex items-center justify-between gap-3"><h3 className="font-semibold">Görevin haftalık saat planı</h3>{!editing && <button className="underline" onClick={onClose}>Kapat</button>}</div><label className="mt-3 block max-w-xs">Plan haftasının pazartesisi<input className="input mt-1" type="date" min="2020-01-06" max="2100-12-27" step={7} value={week} disabled={editing} onChange={e => { setWeek(e.target.value); setMessage(''); }} /></label>
    {message && <p className="mt-3" role="status">{message}</p>}{!week ? <p>Hafta seçin.</p> : query.isPending ? <p role="status">Saat planı yükleniyor…</p> : query.isError ? <p role="alert">{query.error.message} <button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></p> : <><p className="mt-3 font-semibold">{query.data.task.title}</p><p className="my-2 text-sm">Sorumlu: {query.data.task.assigneeName}{!query.data.task.assigneeActive && ' (kapalı hesap)'} · {query.data.plan ? `Plan: ${hours(query.data.plan.hours)}` : 'Bu hafta için saat planı yok; sıfır sayılmadı.'}</p><p className="mb-3 text-sm">Bu süre gerçekleşen çalışma değildir; maliyete yazılmaz. Planı sıfırlamak için gerekçeyle 0 saat kaydedin; geçmiş silinmez. Tamamlanan göreve yeni pozitif saat eklemek için önce görevi yeniden açın.</p>
      {!editing && canManage && <button className={button} onClick={() => setEditing(true)}>Saat planını düzenle</button>}{query.data.plan && <Link className="ml-3 text-sm underline" href={`/activity?entityType=TaskHourPlan&entityId=${query.data.plan.id}`}>Değişiklik geçmişi</Link>}
      {editing && <TaskHourEditor key={`${taskId}-${week}`} baseline={query.data} week={week} onClose={() => setEditing(false)} onSaved={() => { setEditing(false); setMessage('Saat planı kaydedildi.'); cache.invalidateQueries({ queryKey: ['task-hour-plan'] }); cache.invalidateQueries({ queryKey: ['work-planning'] }); }} />}
    </>}{week && <TimeEntryPanel taskId={taskId} week={week} />}</Card>;
}

function TaskHourEditor({ baseline: loaded, week, onSaved, onClose }: { baseline: TaskPlan; week: string; onSaved: () => void; onClose: () => void }) {
  const [baseline] = useState(loaded); const [dirty, setDirty] = useState(false); const [error, setError] = useState(''); useUnsavedChanges(dirty);
  const save = useMutation({ mutationFn: (body: object) => api(`/api/work-tasks/${baseline.task.id}/hour-plan`, { method: 'PUT', body: JSON.stringify(body) }), onSuccess: () => { setDirty(false); onSaved(); } });
  return <form className="mt-4" onChange={() => setDirty(true)} onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); setError(''); try { save.mutate({ weekStart: week, hours: planningHours(String(f.get('hours'))), reason: f.get('reason'), revision: baseline.plan?.revision ?? 0, taskRevision: baseline.task.revision }); } catch (e) { setError(e instanceof Error ? e.message : 'Saati kontrol edin.'); } }}>
    <fieldset disabled={save.isPending} className="space-y-3"><label className="block">Bu haftaya planlanan saat<input className="input mt-1" name="hours" inputMode="decimal" required defaultValue={baseline.plan ? number(baseline.plan.hours) : ''} /></label><label className="block">Planlama nedeni<textarea className="input mt-1" name="reason" required minLength={5} maxLength={1000} /></label><div className="flex gap-3"><button className={button}>Saat planını kaydet</button><button className={button} type="button" onClick={() => { if (!dirty || confirm('Kaydetmeden kapatmak istiyor musunuz?')) onClose(); }}>Vazgeç</button></div></fieldset>{(error || save.error) && <p role="alert" className="mt-3 text-red-700">{error || save.error?.message}</p>}
  </form>;
}

type TimeEntry = { id: string; userId: string; userName: string; weekStart: string; hours: number; note: string; createdBy: string; createdAt: string; voidedAt: string | null; voidedBy: string; voidReason: string; converted: boolean; costLabel: string };
type TimePeriod = { id: string; year: number; month: number; label: string; revision: number };
type TimeState = {
  taskId: string; brandId: string; brandName?: string; title: string; assigneeId: string; assigneeName?: string;
  dueOn: string; completedAt: string | null; canLog: boolean; isManager: boolean; self: string;
  summary: { plannedHours: number; actualHours: number; voidedHours: number; remainingHours: number; complete: boolean };
  entries: TimeEntry[]; periods: TimePeriod[];
};
const dateText = (v: string) => v ? v.split('-').reverse().join('.') : '—';
const monthDays = (year: number, month: number) => new Date(year, month, 0).getDate();
const monthRange = (year: number, month: number) => { const m = String(month).padStart(2, '0'); return { first: `${year}-${m}-01`, last: `${year}-${m}-${monthDays(year, month)}` }; };

export function TimeEntryPanel({ taskId, week }: { taskId: string; week: string }) {
  const cache = useQueryClient();
  const [voiding, setVoiding] = useState<string | null>(null);
  const [converting, setConverting] = useState<string | null>(null);
  const [addKey, setAddKey] = useState(0);
  const query = useQuery({ queryKey: ['time-entries', taskId], queryFn: () => api<TimeState>(`/api/work-tasks/${taskId}/time-entries`), refetchOnWindowFocus: false });
  const refresh = () => {
    void cache.invalidateQueries({ queryKey: ['time-entries', taskId] });
    void cache.invalidateQueries({ queryKey: ['task-hour-plan'] });
    void cache.invalidateQueries({ queryKey: ['work-planning'] });
    void cache.invalidateQueries({ queryKey: ['service-costs'] });
  };
  const add = useMutation({ mutationFn: (body: object) => api(`/api/work-tasks/${taskId}/time-entries`, { method: 'POST', body: JSON.stringify(body) }), onSuccess: () => { notify('Gerçekleşen saat kaydedildi.'); setAddKey(x => x + 1); refresh(); } });
  const remove = useMutation({ mutationFn: (input: { entryId: string; reason: string }) => api(`/api/work-tasks/${taskId}/time-entries/${input.entryId}/void`, { method: 'POST', body: JSON.stringify({ reason: input.reason }) }), onSuccess: () => { setVoiding(null); notify('Saat girişi iptal edildi. Kayıt silinmez, iptalli hâlde kalır.'); refresh(); } });
  const convert = useMutation({ mutationFn: (input: { entryId: string; periodId: string; revision: number; incurredOn: string; hourlyCost: number; reference: string; description: string }) => api(`/api/performance/${input.periodId}/costs/entries/from-time`, { method: 'POST', body: JSON.stringify({ id: crypto.randomUUID(), timeEntryId: input.entryId, hourlyCost: input.hourlyCost, incurredOn: input.incurredOn, reference: input.reference, description: input.description, confirmed: true, revision: input.revision }) }), onSuccess: () => { setConverting(null); notify('Saat girişi hizmet maliyetine aktarıldı. Aynı çalışma ikinci kez maliyet olmaz.'); refresh(); } });

  if (query.isPending) return <p className="mt-4 text-sm" role="status">Gerçekleşen saatler yükleniyor…</p>;
  if (query.isError) return <p className="mt-4 text-sm" role="alert">{query.error.message} <button className="underline" onClick={() => query.refetch()}>Yeniden dene</button></p>;
  const s = query.data; const sum = s.summary;
  return <div className="mt-4 border-t pt-4">
    <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="font-semibold">Gerçekleşen saatler</h3><Badge tone={sum.complete ? 'green' : 'neutral'}>{sum.complete ? 'Plan ve gerçekleşen var' : 'Henüz gerçekleşen saat yok'}</Badge></div>
    <dl className="my-3 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
      <div><dt>Planlanan</dt><dd className="font-semibold">{number(sum.plannedHours)} saat</dd></div>
      <div><dt>Gerçekleşen</dt><dd className="font-semibold">{number(sum.actualHours)} saat</dd></div>
      <div><dt>İptal edilen</dt><dd className="font-semibold">{number(sum.voidedHours)} saat</dd></div>
      <div><dt>Kalan</dt><dd className="font-semibold">{number(sum.remainingHours)} saat</dd></div>
    </dl>
    <p className="mb-3 text-sm text-[#6d7175]">Planlanan ve gerçekleşen saat yan yana gösterilir; biri diğerinden hesaplanmaz. Bu alan bordro değildir ve çalışan performans puanı üretmez.</p>

    {s.canLog && <form key={addKey} className="rounded-lg border p-3" onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); add.mutate({ id: crypto.randomUUID(), weekStart: String(f.get('weekStart')), hours: Number(String(f.get('hours')).replace(',', '.')), note: f.get('note') }); }}>
      <h4 className="text-sm font-semibold">Gerçekleşen saat ekle</h4>
      <div className="mt-2 grid gap-3 sm:grid-cols-3"><label className="text-sm">Haftanın pazartesisi<input className="input mt-1" type="date" name="weekStart" min="2020-01-06" max="2100-12-27" step={7} defaultValue={week} required /></label>
        <label className="text-sm">Çalışılan saat<input className="input mt-1" type="number" name="hours" inputMode="decimal" min="0.01" max="168" step="0.01" required placeholder="Örneğin 3,5" /></label>
        <label className="text-sm">Not<input className="input mt-1" name="note" maxLength={1000} placeholder="Örneğin: içerik planı hazırlandı" /></label></div>
      <button className={`${button} mt-3`} disabled={add.isPending}>{add.isPending ? 'Kaydediliyor…' : 'Saati kaydet'}</button>
      {add.error && <p className="mt-2 text-sm text-[#d72c0d]" role="alert">{add.error.message}</p>}
    </form>}
    {!s.canLog && <p className="mb-3 text-sm text-[#6d7175]">Bu görevde saat kaydetme yetkiniz yok; kayıtları yalnız görüntüleyebilirsiniz.</p>}

    <ul className="mt-3 space-y-3">{s.entries.map(entry => <li key={entry.id} className="rounded-lg border p-3">
      <div className="flex flex-wrap items-center justify-between gap-2"><strong>{number(entry.hours)} saat · {dateText(entry.weekStart)} haftası</strong>
        {entry.voidedAt ? <Badge tone="red">İptal</Badge> : entry.converted ? <Badge tone="blue">Maliyete aktarıldı ({entry.costLabel})</Badge> : <Badge tone="neutral">Aktarılmadı</Badge>}</div>
      {entry.note && <p className="mt-1 text-sm text-[#6d7175]">{entry.note}</p>}
      <p className="mt-1 text-xs text-[#6d7175]">Giren: {entry.userName || entry.createdBy} · {turkceTarih(entry.createdAt)}</p>
      {entry.voidedAt && <p className="mt-1 text-sm">İptal nedeni: {entry.voidReason} · {entry.voidedBy}</p>}
      {!entry.voidedAt && (s.isManager || entry.userId === s.self) && voiding !== entry.id && <button className="mt-2 underline" onClick={() => { setVoiding(entry.id); setConverting(null); }}>Saati iptal et</button>}
      {voiding === entry.id && <form className="mt-2" onSubmit={e => { e.preventDefault(); const reason = String(new FormData(e.currentTarget).get('reason') ?? '').trim(); if (reason) remove.mutate({ entryId: entry.id, reason }); }}>
        <label className="block text-sm">İptal nedeni<input className="input mt-1" name="reason" required maxLength={1000} placeholder="Örneğin: yanlış haftaya girildi." /></label>
        <div className="mt-2 flex gap-3"><button className={button} disabled={remove.isPending}>Saati iptal et</button><button className={button} type="button" onClick={() => setVoiding(null)}>Vazgeç</button></div>
      </form>}
      {!entry.voidedAt && !entry.converted && s.isManager && s.periods.length > 0 && converting !== entry.id && <button className="mt-2 underline" onClick={() => { setConverting(entry.id); setVoiding(null); }}>Hizmet maliyetine aktar</button>}
      {!entry.voidedAt && !entry.converted && s.isManager && converting === entry.id && <ConvertForm periods={s.periods} pending={convert.isPending} submit={input => convert.mutate({ entryId: entry.id, ...input })} close={() => setConverting(null)} />}
    </li>)}</ul>
    {s.entries.length === 0 && <p className="text-sm">Bu görevde henüz gerçekleşen saat girilmedi.</p>}
  </div>;
}

function ConvertForm({ periods, submit, close, pending }: { periods: TimePeriod[]; submit: (input: { periodId: string; revision: number; incurredOn: string; hourlyCost: number; reference: string; description: string }) => void; close: () => void; pending: boolean }) {
  const [periodId, setPeriodId] = useState(periods[0].id);
  const period = periods.find(x => x.id === periodId) ?? periods[0];
  const range = monthRange(period.year, period.month);
  return <form className="mt-3 space-y-3 rounded-lg border p-3" onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); submit({ periodId, revision: period.revision, incurredOn: String(f.get('incurredOn')), hourlyCost: Number(String(f.get('hourlyCost')).replace(',', '.')), reference: String(f.get('reference')).trim(), description: String(f.get('description')).trim() }); }}>
    <p className="text-sm text-[#6d7175]">Bu saat tek seferlik olarak seçili kapanış dönemine hizmet maliyeti olarak yazılır. Aynı saat ikinci kez maliyet olmaz.</p>
    <div className="grid gap-3 sm:grid-cols-2"><label className="text-sm">Kapanış dönemi<select className="input mt-1" value={periodId} onChange={e => setPeriodId(e.target.value)}>{periods.map(p => <option key={p.id} value={p.id}>{p.label}</option>)}</select></label>
      <label className="text-sm">Gider tarihi<input className="input mt-1" type="date" name="incurredOn" min={range.first} max={range.last} defaultValue={range.first} required /></label>
      <label className="text-sm">Saat ücreti<input className="input mt-1" type="number" name="hourlyCost" inputMode="decimal" min="0.01" step="0.01" required placeholder="Örneğin 1500" /></label>
      <label className="text-sm">Gider referansı<input className="input mt-1" name="reference" required maxLength={160} placeholder="Örneğin SAAT-2026-014" /></label></div>
    <label className="block text-sm">Giderin açıklaması<textarea className="input mt-1" name="description" required maxLength={1000} placeholder="Bu saat hangi iş için harcandı?" /></label>
    <div className="flex gap-3"><button className={button} disabled={pending}>{pending ? 'Aktarılıyor…' : 'Maliyete aktar'}</button><button className={button} type="button" onClick={close}>Vazgeç</button></div>
  </form>;
}
