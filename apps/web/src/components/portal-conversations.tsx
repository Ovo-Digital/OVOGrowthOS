'use client';
import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card } from '@/components/ui/core';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Status = 'Open' | 'AwaitingCustomer' | 'Resolved';
type Conversation = { id: string; reportId: string; question: string; answer: string; createdAt: string; answeredAt: string | null; revision: number;
  status: Status; ownerId: string | null; ownerName: string | null; customerName: string | null; lastMessageAt: string; lastReplyAt: string | null;
  report: { year: number; month: number; version: number; revokedAt: string | null };
  messages: { id: string; text: string; fromStaff: boolean; createdAt: string }[] };
type Owner = { id: string; name: string };
const labels: Record<Status, string> = { Open: 'OVO yanıtı bekleniyor', AwaitingCustomer: 'Müşteri yanıtı bekleniyor', Resolved: 'Çözüldü' };
const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';
const date = (s: string) => new Date(s).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' });

export function PortalConversations({ brandId }: { brandId?: string }) {
  const root = brandId ? `/api/portal-management/brands/${brandId}` : '/api/portal';
  const query = useQuery({ queryKey: ['portal-conversations', root], queryFn: () => api<Conversation[]>(root + '/conversations'), refetchInterval: 30000 });
  const owners = useQuery({ queryKey: ['portal-conversation-owners', brandId], queryFn: () => api<Owner[]>(root + '/conversation-owners'), enabled: !!brandId });
  const [filter, setFilter] = useState('');
  return <Card className="space-y-3 p-5 print:hidden"><h2 className="font-semibold">{brandId ? '4. Müşteri konuşmaları' : 'Sorularım ve yanıtlar'}</h2>
    <p className="text-sm">{brandId ? 'Mesajlar yalnız soruyu açan müşteriye gösterilir. İç not veya iç maliyet yazmayın. Sorumlu, yanıt verebilen bir yönetici veya ortak olmalıdır.' : 'Yalnız bu hesapla açtığınız konuları görürsünüz. Yeni bir konu için üstten rapor seçip açıklama isteyin.'} Gönderilmiş mesajlar değiştirilmez; düzeltme için yeni mesaj ekleyin. E-posta gönderilmez. Tarihler Türkiye saatidir.</p>
    <div className="flex flex-wrap items-end gap-3"><label className="text-sm">Konu durumu<select className="input mt-1" value={filter} onChange={e => setFilter(e.target.value)}><option value="">Tüm konular</option>{Object.entries(labels).map(([key, label]) => <option value={key} key={key}>{label}</option>)}</select></label><button className={button} disabled={query.isFetching} onClick={() => void query.refetch()}>Konuşmaları yenile</button></div>
    {query.isPending ? <p role="status">Konuşmalar yükleniyor…</p> : query.isError ? <p role="alert">Konuşmalar alınamadı. {query.error.message}</p> : <>
      {!query.data.length && <p>Henüz konuşma yok.</p>}{query.data.length > 0 && !query.data.some(q => !filter || q.status === filter) && <p>Bu durumda konu yok.</p>}
      {query.data.map(q => <ConversationCard key={q.id} item={q} root={root} staff={!!brandId} owners={owners.data ?? []} ownersError={owners.isError} matches={!filter || q.status === filter} />)}
    </>}
  </Card>;
}

function ConversationCard({ item: q, root, staff, owners, ownersError, matches }: { item: Conversation; root: string; staff: boolean; owners: Owner[]; ownersError: boolean; matches: boolean }) {
  const [editing, setEditing] = useState(false); const [notice, setNotice] = useState('');
  if (!matches && !editing) return null;
  return <article className="space-y-3 rounded-lg border p-4 text-sm">
    <h3 className="font-semibold">{q.report.month}/{q.report.year} · Sürüm {q.report.version} · {labels[q.status]}</h3>
    <p>Son mesaj: {date(q.lastMessageAt)}{staff && ` · Sorumlu: ${q.ownerName || 'Henüz atanmadı'}`}</p>
    {staff && <p>Müşteri: {q.customerName} · Son OVO yanıtı: {q.lastReplyAt ? date(q.lastReplyAt) : 'Henüz yanıt yok'}</p>}
    {!matches && <p>Bu konu artık seçili filtreye uymuyor; açık formunuzun kaybolmaması için gösterilmeye devam ediyor.</p>}
    <ol className="space-y-3"><li><p className="font-semibold">Müşteri · {date(q.createdAt)}</p><p className="whitespace-pre-wrap break-words">{q.question}</p></li>
      {q.answer && <li><p className="font-semibold">OVO ekibi{q.answeredAt && ` · ${date(q.answeredAt)}`}</p><p className="whitespace-pre-wrap break-words">{q.answer}</p></li>}
      {q.messages.map(m => <li key={m.id}><p className="font-semibold">{m.fromStaff ? 'OVO ekibi' : 'Müşteri'} · {date(m.createdAt)}</p><p className="whitespace-pre-wrap break-words">{m.text}</p></li>)}
    </ol>
    {q.report.revokedAt && <p>Kaynak raporun paylaşımı geri çekildi. Geçmiş korunur; yeni mesaj için açık bir rapordan konu başlatın.</p>}
    {notice && <p role="status">{notice}</p>}
    {!editing ? <button className={button} disabled={!staff && !!q.report.revokedAt} onClick={() => { setEditing(true); setNotice(''); }}>{staff ? 'Mesaj veya takip bilgisi ekle' : 'Bu konuya mesaj yaz'}</button>
      : <ConversationEditor item={q} root={root} staff={staff} owners={owners} ownersError={ownersError} done={() => { setEditing(false); setNotice('Konuşma güncellendi.'); }} cancel={() => setEditing(false)} />}
  </article>;
}

function ConversationEditor({ item: q, root, staff, owners, ownersError, done, cancel }: { item: Conversation; root: string; staff: boolean; owners: Owner[]; ownersError: boolean; done: () => void; cancel: () => void }) {
  const qc = useQueryClient(); const [revision, setRevision] = useState(q.revision); const [text, setText] = useState('');
  const [status, setStatus] = useState(q.status); const [ownerId, setOwnerId] = useState(q.ownerId ?? ''); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const dirty = !!text || status !== q.status || ownerId !== (q.ownerId ?? ''); useUnsavedChanges(dirty);
  async function save(message: boolean) {
    if (busy) return;
    if (message && !confirm(staff ? 'Bu mesaj soruyu açan müşteriye gönderilsin mi? Sonradan değiştirilemez.' : 'Bu mesaj OVO ekibine gönderilsin mi? Sonradan değiştirilemez.')) return;
    setBusy(true); setError('');
    try { await api(root + `/questions/${q.id}/` + (message ? 'messages' : 'tracking'), { method: message ? 'POST' : 'PUT', body: JSON.stringify(message ? { text, revision } : { status, ownerId: ownerId || null, revision }) });
      await qc.invalidateQueries({ queryKey: ['portal-conversations', root] }); done();
    } catch (e) { setError(e instanceof Error ? e.message : 'İşlem kaydedilemedi.'); await qc.invalidateQueries({ queryKey: ['portal-conversations', root] }); } finally { setBusy(false); }
  }
  function submit(e: FormEvent) { e.preventDefault(); void save(true); }
  return <div className="space-y-3">{error && <p role="alert">{error}</p>}
    {revision !== q.revision && <p role="alert">Konuşmaya yeni bilgi geldi. Üstteki güncel mesajları okuyun. <button className="underline" disabled={busy} onClick={() => { setRevision(q.revision); setStatus(q.status); setOwnerId(q.ownerId ?? ''); }}>Metnimi koruyup güncel takip bilgilerini al</button></p>}
    {!q.report.revokedAt && <form onSubmit={submit} className="space-y-2"><label className="block">Yeni mesaj<textarea className="input mt-1" rows={3} maxLength={4000} required value={text} onChange={e => setText(e.target.value)} disabled={busy} /></label><p>Yeni müşteri mesajı konuyu yeniden açar; OVO mesajı müşteri yanıtını beklemeye alır.</p><button className={button} disabled={busy || revision !== q.revision}>Mesajı gönder</button></form>}
    {staff && <div className="space-y-2 border-t pt-3"><label className="block">Takip sorumlusu<select className="input mt-1" value={ownerId} onChange={e => setOwnerId(e.target.value)} disabled={busy || ownersError}><option value="">Atanmadı</option>{ownerId && !owners.some(o => o.id === ownerId) && <option value={ownerId}>{q.ownerName || 'Önceki sorumlu'} · yeniden seçin</option>}{owners.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>{ownersError && <p role="alert">Sorumlu listesi alınamadı; sayfayı yenileyin.</p>}<label className="block">Yeni konu durumu<select className="input mt-1" value={status} onChange={e => setStatus(e.target.value as Status)} disabled={busy}>{Object.entries(labels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label><button className={button} disabled={busy || ownersError || revision !== q.revision || !!text.trim()} onClick={() => void save(false)}>Takip bilgisini kaydet</button>{text.trim() && <p>Önce yazdığınız mesajı gönderin veya temizleyin; takip kaydı mesajı göndermez.</p>}</div>}
    <button className={button} disabled={busy} onClick={() => { if (!dirty || confirm('Yazdığınız değişiklikler kaydedilmeyecek. Vazgeçilsin mi?')) cancel(); }}>Vazgeç</button>
  </div>;
}
