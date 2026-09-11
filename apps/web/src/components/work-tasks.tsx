'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Badge, Card } from '@/components/ui/core';

export type TeamMember = { id: string; name: string; isActive: boolean };
export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };
type Work = { id: string; brandId: string; brandName: string; assigneeId: string; assigneeName: string; assigneeActive: boolean; title: string; description: string; priority: string; kind: string; dealId: string | null; year: number | null; month: number | null; dueOn: string; completedAt: string | null; completedBy: string; revision: number; overdue: boolean };
type Approval = { id: string; brandId: string; brandName: string; year: number; month: number; preparedBy: string };
const kinds = { General: 'Genel görev', MonthlyClose: 'Aylık kapanış', ContractRenewal: 'Anlaşma yenileme' };
const priorities = { Low: 'Düşük', Normal: 'Normal', High: 'Yüksek' };
export const dayText = (value: string | null) => value ? value.split('-').reverse().join('.') : 'Belirlenmedi';
export const todayText = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
export function WorkTasks({ brandId }: { brandId?: string }) {
  const [view, setView] = useState(brandId ? 'open' : 'mine');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Work | 'new' | null>(null);
  const [message, setMessage] = useState('');
  const cache = useQueryClient();
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const tasks = useQuery({ queryKey: ['work-tasks', brandId, view, page], queryFn: () => api<Paged<Work>>(`/api/work-tasks?view=${view}&page=${page}${brandId ? `&brandId=${brandId}` : ''}`), enabled: view !== 'approvals' });
  const approvals = useQuery({ queryKey: ['work-approvals', page], queryFn: () => api<Paged<Approval>>(`/api/work-approvals?page=${page}`), enabled: view === 'approvals' });
  const completion = useMutation({ mutationFn: (task: Work) => api(`/api/work-tasks/${task.id}/completion`, { method: 'PUT', body: JSON.stringify({ completed: !task.completedAt, revision: task.revision }) }),
    onSuccess: (_, task) => { setMessage(task.completedAt ? 'Görev yeniden açıldı.' : 'Görev tamamlandı. Finansal dönem veya anlaşma durumu değiştirilmedi.'); cache.invalidateQueries({ queryKey: ['work-tasks'] }); cache.invalidateQueries({ queryKey: ['tasks'] }); }, onError: (error) => setMessage(error.message) });
  const result = view === 'approvals' ? approvals : tasks;
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold">{brandId ? 'Markanın görevleri' : 'Ekip işleri'}</h2>{brandId && canManage && <button className="rounded-lg bg-[#303030] px-3 py-2 text-sm text-white" onClick={() => { setEditing('new'); setMessage(''); }}>Görev ekle</button>}</div>
    {!brandId && <p className="mt-2 text-sm">Yeni görev eklemek için <Link className="underline" href="/brands">ilgili markayı açın</Link>. Görevi tamamlamak, aylık sonucu onaylamaz veya anlaşmayı yenilemez.</p>}
    <div className="my-4 flex flex-wrap gap-2" role="group" aria-label="Görev görünümü">{[['mine', 'Benim işlerim'], ['open', 'Tüm açık işler'], ['overdue', 'Gecikenler'], ['completed', 'Tamamlananlar'], ...(!brandId ? [['approvals', 'Onay bekleyenler']] : [])].map(([key, label]) => <button key={key} aria-pressed={view === key} onClick={() => { setView(key); setPage(1); }} className={`rounded-lg border px-3 py-2 text-sm ${view === key ? 'bg-[#e3e5e7] font-semibold' : ''}`}>{label}</button>)}</div>
    <p className="mb-3 text-xs text-[#6d7175]">Son tarihler Türkiye saatine göre değerlendirilir. Bugün teslim edilecek iş henüz gecikmiş değildir.</p>
    {message && <p className="mb-3 text-sm" role="status">{message}</p>}
    {editing && brandId && <TaskForm key={editing === 'new' ? 'new' : `${editing.id}-${editing.revision}`} brandId={brandId} task={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); setMessage('Görev kaydedildi.'); cache.invalidateQueries({ queryKey: ['work-tasks'] }); cache.invalidateQueries({ queryKey: ['tasks'] }); }} />}
    {result.isPending ? <p role="status">İşler yükleniyor…</p> : result.isError ? <div role="alert">{result.error.message} <button className="underline" onClick={() => result.refetch()}>Yeniden dene</button></div> : view === 'approvals' ? <div className="space-y-3">{approvals.data?.items.map(item => <div className="rounded-lg border p-4" key={item.id}><Link className="font-semibold underline" href={`/performance/${item.id}`}>{item.brandName} · {item.month}/{item.year}</Link><p className="mt-1 text-sm">Hazırlayan: {item.preparedBy || 'Kayıtlı değil'}. Hazırlayan dışında bir yönetici veya iş ortağı onaylamalıdır.</p></div>)}</div> : <div className="space-y-3">{tasks.data?.items.map(task => <article className="rounded-lg border p-4" key={task.id}>
      <div className="flex flex-wrap items-start justify-between gap-2"><div><h3 className="font-semibold">{task.title}</h3><Link className="text-sm underline" href={`/brands/${task.brandId}#team-work`}>{task.brandName}</Link></div><Badge tone={task.completedAt ? 'green' : task.overdue ? 'red' : 'neutral'}>{task.completedAt ? 'Tamamlandı' : task.overdue ? 'Gecikti' : 'Açık'}</Badge></div>
      <p className="mt-2 whitespace-pre-wrap break-words text-sm">{task.description}</p><p className="mt-2 text-sm">Sorumlu: {task.assigneeName}{!task.assigneeActive && ' (hesabı kapalı; yeniden atayın)'} · Son tarih: {dayText(task.dueOn)} · Öncelik: {priorities[task.priority as keyof typeof priorities]}</p>
      <p className="mt-1 text-xs text-[#6d7175]">{kinds[task.kind as keyof typeof kinds]}{task.year && ` · ${task.month}/${task.year}`}{task.dealId && <> · <Link className="underline" href={`/deals/${task.dealId}`}>İlgili anlaşma</Link></>}{task.completedAt && ` · Tamamlayan: ${task.completedBy}`}</p>
      <div className="mt-3 flex flex-wrap gap-3 text-sm">{canManage && !task.completedAt && <button className="underline" onClick={() => { if (brandId) setEditing(task); }} disabled={!brandId}>{brandId ? 'Düzenle' : 'Düzenlemek için markayı açın'}</button>}{(canManage || task.assigneeId === me.data?.id) && <button className="rounded-lg border px-3 py-1.5 disabled:opacity-50" disabled={completion.isPending} onClick={() => completion.mutate(task)}>{task.completedAt ? 'Yeniden aç' : 'Tamamlandı olarak işaretle'}</button>}<Link className="underline" href={`/activity?entityType=WorkTask&entityId=${task.id}`}>İşlem geçmişi</Link></div>
    </article>)}</div>}
    {result.data?.total === 0 && <p className="py-5 text-sm">Bu görünümde iş bulunmuyor.</p>}
    {!!result.data?.total && <div className="mt-4 flex items-center justify-between text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki</button><span>{result.data.total} kayıt · Sayfa {page}</span><button disabled={page * result.data.pageSize >= result.data.total} onClick={() => setPage(page + 1)}>Sonraki</button></div>}
  </Card>;
}

function TaskForm({ brandId, task, onClose, onSaved }: { brandId: string; task?: Work; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState(() => ({ id: task?.id ?? crypto.randomUUID(), brandId, assigneeId: task?.assigneeId ?? '', title: task?.title ?? '', description: task?.description ?? '', priority: task?.priority ?? 'Normal', kind: task?.kind ?? 'General', dealId: task?.dealId ?? '', period: task?.year ? `${task.year}-${String(task.month).padStart(2, '0')}` : '', dueOn: task?.dueOn ?? '', revision: task?.revision ?? 0 }));
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  const brand = useQuery({ queryKey: ['work-brand', brandId], queryFn: () => api<{ deals: { id: string; name: string }[] }>(`/api/brands/${brandId}`) });
  const save = useMutation({ mutationFn: () => { const [year, month] = form.period.split('-').map(Number); return api(`/api/work-tasks${task ? `/${task.id}` : ''}`, { method: task ? 'PUT' : 'POST', body: JSON.stringify({ ...form, dealId: form.kind === 'General' ? null : form.dealId, year: form.kind === 'MonthlyClose' ? year : null, month: form.kind === 'MonthlyClose' ? month : null }) }); }, onSuccess: onSaved });
  return <form className="mb-5 rounded-lg border bg-[#f7f7f8] p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}>
    <h3 className="mb-3 font-semibold">{task ? 'Görevi düzenle' : 'Yeni görev'}</h3><fieldset disabled={save.isPending} className="grid gap-3 sm:grid-cols-2">
      <label>Görev başlığı<input className="input mt-1" required maxLength={200} value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
      <label>Sorumlu çalışan<select className="input mt-1" required value={form.assigneeId} onChange={e => setForm({ ...form, assigneeId: e.target.value })}><option value="">Çalışan seçin</option>{team.data?.map(person => <option key={person.id} value={person.id} disabled={!person.isActive}>{person.name}{!person.isActive ? ' (kapalı hesap)' : ''}</option>)}</select></label>
      <label>Son tarih<input className="input mt-1" type="date" required min="2020-01-01" max="2100-12-31" value={form.dueOn} onChange={e => setForm({ ...form, dueOn: e.target.value })} /></label>
      <label>Öncelik<select className="input mt-1" value={form.priority} onChange={e => setForm({ ...form, priority: e.target.value })}>{Object.entries(priorities).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      <label>Görev türü<select disabled={!!task} className="input mt-1" value={form.kind} onChange={e => setForm({ ...form, kind: e.target.value, dealId: '', period: '' })}>{Object.entries(kinds).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      {form.kind !== 'General' && <label>İlgili anlaşma<select disabled={!!task} className="input mt-1" required value={form.dealId} onChange={e => setForm({ ...form, dealId: e.target.value })}><option value="">Anlaşma seçin</option>{brand.data?.deals.map(deal => <option key={deal.id} value={deal.id}>{deal.name}</option>)}</select></label>}
      {form.kind === 'MonthlyClose' && <label>Kapatılacak dönem<input disabled={!!task} className="input mt-1" type="month" required min="2020-01" max="2100-12" value={form.period} onChange={e => setForm({ ...form, period: e.target.value })} /></label>}
      <label className="sm:col-span-2">Açıklama<textarea className="input mt-1" rows={3} maxLength={4000} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
      <div className="flex gap-3"><button className="rounded-lg bg-[#303030] px-3 py-2 text-white" disabled={team.isPending || team.isError || (form.kind !== 'General' && !brand.data)}> {save.isPending ? 'Kaydediliyor…' : 'Görevi kaydet'}</button><button type="button" className="underline" onClick={onClose}>Vazgeç</button></div>
    </fieldset>{(save.isError || team.isError || brand.isError) && <p className="mt-3 text-sm text-red-700" role="alert">{save.error?.message || team.error?.message || brand.error?.message}</p>}
  </form>;
}
