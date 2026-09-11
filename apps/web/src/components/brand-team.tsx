'use client';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Card } from '@/components/ui/core';
import { DocumentsPanel } from '@/components/record-panels';
import { WorkTasks, dayText, todayText, type TeamMember, type Paged } from '@/components/work-tasks';

export const leadStages: Record<string, string> = { New: 'Yeni aday', Contacted: 'İlk görüşme yapıldı', WaitingForInformation: 'Bilgi bekleniyor', MeetingPlanned: 'Görüşme planlandı', ProposalFollowUp: 'Teklif takibi', OnHold: 'Beklemeye alındı' };
type FollowUp = { ownerId: string | null; stage: string; waitingReason: string; nextContactOn: string | null; nextStep: string; revision: number };
type Note = { id: string; text: string; contactOn: string; createdBy: string; createdAt: string };

export function BrandTeam({ brandId }: { brandId: string }) {
  const [editing, setEditing] = useState(false);
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState('');
  const [note, setNote] = useState<{ id: string; contactOn: string; text: string } | null>(null);
  const cache = useQueryClient();
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  const follow = useQuery({ queryKey: ['follow-up', brandId], queryFn: () => api<{ followUp: FollowUp; lastContactOn: string | null }>(`/api/brands/${brandId}/follow-up`) });
  const notes = useQuery({ queryKey: ['contact-notes', brandId, page], queryFn: () => api<Paged<Note>>(`/api/brands/${brandId}/contact-notes?page=${page}`) });
  const saveNote = useMutation({ mutationFn: () => api(`/api/brands/${brandId}/contact-notes`, { method: 'POST', body: JSON.stringify(note) }), onSuccess: () => { setNote(null); setPage(1); setMessage('Görüşme notu eklendi.'); cache.invalidateQueries({ queryKey: ['contact-notes', brandId] }); cache.invalidateQueries({ queryKey: ['follow-up', brandId] }); cache.invalidateQueries({ queryKey: ['lead-follow-ups'] }); } });
  const owner = team.data?.find(person => person.id === follow.data?.followUp.ownerId);
  return <div id="team-work" className="mt-6 scroll-mt-20">
    <Card className="p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold">Sorumlu ve görüşme takibi</h2>{canManage && follow.data && <button className="underline" onClick={() => setEditing(!editing)}>{editing ? 'Vazgeç' : 'Takip bilgilerini düzenle'}</button>}</div>
      <p className="mt-2 text-xs text-[#6d7175]">Marka sorumluluğu finansal onay yetkisi vermez. Takip aşaması, markanın değerlendirme veya anlaşma durumundan ayrıdır.</p>
      {(follow.isPending || team.isPending) ? <p className="mt-3" role="status">Takip bilgileri yükleniyor…</p> : (follow.isError || team.isError) ? <p className="mt-3" role="alert">{follow.error?.message || team.error?.message} <button className="underline" onClick={() => { follow.refetch(); team.refetch(); }}>Yeniden dene</button></p> : follow.data && <>
        <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">{[
          ['Marka sorumlusu', owner ? `${owner.name}${!owner.isActive ? ' (kapalı hesap; yeni sorumlu seçin)' : ''}` : 'Atanmadı'],
          ['Takip aşaması', leadStages[follow.data.followUp.stage]], ['Son görüşme', dayText(follow.data.lastContactOn)],
          ['Sonraki görüşme', dayText(follow.data.followUp.nextContactOn)], ['Bekleme nedeni', follow.data.followUp.waitingReason || 'Yok'], ['Sonraki adım', follow.data.followUp.nextStep || 'Belirlenmedi']
        ].map(([label, value]) => <div key={label}><dt className="text-[#6d7175]">{label}</dt><dd className="whitespace-pre-wrap break-words font-medium">{value}</dd></div>)}</dl>
        {editing && <FollowUpForm key={follow.data.followUp.revision} brandId={brandId} initial={follow.data.followUp} team={team.data ?? []} onSaved={() => { setEditing(false); setMessage('Takip bilgileri kaydedildi.'); cache.invalidateQueries({ queryKey: ['follow-up', brandId] }); cache.invalidateQueries({ queryKey: ['lead-follow-ups'] }); cache.invalidateQueries({ queryKey: ['tasks'] }); }} />}
      </>}
      {message && <p className="mt-3 text-sm" role="status">{message}</p>}
    </Card>
    <WorkTasks brandId={brandId} />
    <div className="mt-5"><DocumentsPanel entityType="Brand" entityId={brandId} canUpload={canManage} /></div>
    <Card className="mt-5 p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold">Görüşme notları</h2>{canManage && <button className="underline" onClick={() => { setNote({ id: crypto.randomUUID(), text: '', contactOn: todayText() }); saveNote.reset(); }}>Görüşme notu ekle</button>}</div>
      <p className="mt-2 text-xs text-[#6d7175]">Notlar geçmişi korumak için silinmez veya değiştirilmez. Düzeltme gerekiyorsa yeni bir açıklama ekleyin. Şifre ve hassas kişisel bilgi yazmayın.</p>
      {note && <form className="my-4 space-y-3 rounded-lg border p-4" onSubmit={e => { e.preventDefault(); saveNote.mutate(); }}><label className="block">Görüşme tarihi<input className="input mt-1" type="date" required min="2020-01-01" max={todayText()} value={note.contactOn} onChange={e => setNote({ ...note, contactOn: e.target.value })} /></label><label className="block">Görüşmede ne konuşuldu?<textarea className="input mt-1" rows={4} required maxLength={4000} value={note.text} onChange={e => setNote({ ...note, text: e.target.value })} /></label><div className="flex gap-3"><button disabled={saveNote.isPending} className="rounded-lg bg-[#303030] px-3 py-2 text-white">{saveNote.isPending ? 'Kaydediliyor…' : 'Notu kaydet'}</button><button disabled={saveNote.isPending} type="button" className="underline" onClick={() => setNote(null)}>Vazgeç</button></div>{saveNote.isError && <p role="alert">{saveNote.error.message}</p>}</form>}
      {notes.isPending ? <p className="mt-4" role="status">Notlar yükleniyor…</p> : notes.isError ? <p role="alert">{notes.error.message} <button className="underline" onClick={() => notes.refetch()}>Yeniden dene</button></p> : !notes.data?.items.length ? <p className="mt-4 text-sm">Henüz görüşme notu yok.</p> : <div className="mt-4 space-y-3">{notes.data.items.map(item => <article className="rounded-lg border p-3" key={item.id}><p className="text-xs text-[#6d7175]">Görüşme: {dayText(item.contactOn)} · Kaydeden: {item.createdBy}</p><p className="mt-2 whitespace-pre-wrap break-words text-sm">{item.text}</p></article>)}<div className="flex items-center justify-between text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki</button><span>{notes.data.total} not · Sayfa {page}</span><button disabled={page * notes.data.pageSize >= notes.data.total} onClick={() => setPage(page + 1)}>Sonraki</button></div></div>}
    </Card>
  </div>;
}

function FollowUpForm({ brandId, initial, team, onSaved }: { brandId: string; initial: FollowUp; team: TeamMember[]; onSaved: () => void }) {
  const [form, setForm] = useState({ ...initial, ownerId: initial.ownerId ?? '', nextContactOn: initial.nextContactOn ?? '' });
  const save = useMutation({ mutationFn: () => api(`/api/brands/${brandId}/follow-up`, { method: 'PUT', body: JSON.stringify({ ...form, ownerId: form.ownerId || null, nextContactOn: form.nextContactOn || null }) }), onSuccess: onSaved });
  return <form className="mt-4 rounded-lg border bg-[#f7f7f8] p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><fieldset disabled={save.isPending} className="grid gap-3 sm:grid-cols-2">
    <label>Marka sorumlusu<select className="input mt-1" value={form.ownerId} onChange={e => setForm({ ...form, ownerId: e.target.value })}><option value="">Atanmamış</option>{team.map(person => <option disabled={!person.isActive} key={person.id} value={person.id}>{person.name}{!person.isActive ? ' (kapalı hesap)' : ''}</option>)}</select></label>
    <label>Takip aşaması<select className="input mt-1" value={form.stage} onChange={e => setForm({ ...form, stage: e.target.value })}>{Object.entries(leadStages).map(([key, label]) => <option value={key} key={key}>{label}</option>)}</select></label>
    <label>Sonraki görüşme<input className="input mt-1" type="date" required={form.stage === 'MeetingPlanned'} min="2020-01-01" max="2100-12-31" value={form.nextContactOn} onChange={e => setForm({ ...form, nextContactOn: e.target.value })} /></label>
    <label>Bekleme nedeni<textarea className="input mt-1" maxLength={1000} required={['OnHold', 'WaitingForInformation'].includes(form.stage)} value={form.waitingReason} onChange={e => setForm({ ...form, waitingReason: e.target.value })} /></label>
    <label className="sm:col-span-2">Sonraki adım<textarea className="input mt-1" required={!!form.nextContactOn} maxLength={1000} value={form.nextStep} onChange={e => setForm({ ...form, nextStep: e.target.value })} /></label>
    <button className="rounded-lg bg-[#303030] px-3 py-2 text-white">{save.isPending ? 'Kaydediliyor…' : 'Takip bilgilerini kaydet'}</button>
  </fieldset>{save.isError && <p className="mt-3" role="alert">{save.error.message}</p>}</form>;
}
