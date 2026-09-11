'use client';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, logout, type SessionUser } from '@/lib/api';
import { turkce } from '@/lib/turkish';
import { notify } from '@/components/feedback';
import { Badge, Card, PageHeader } from '@/components/ui/core';

type User = SessionUser & { isActive: boolean; createdAt: string };
const initial = { email: '', name: '', role: 'Analyst', password: '', isActive: true };

export default function Page() {
  const qc = useQueryClient();
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const users = useQuery({ queryKey: ['users'], queryFn: () => api<User[]>('/api/users'), enabled: me.data?.role === 'Admin' });
  const [form, setForm] = useState(initial);
  const [editing, setEditing] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  function reset() { setForm(initial); setEditing(null); setError(''); }
  function edit(user: User) {
    setEditing(user.id);
    setForm({ email: user.email, name: user.name, role: user.role, password: '', isActive: user.isActive });
    setError('');
    document.getElementById('user-name')?.focus();
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (saving) return;
    if (editing && !window.confirm(`${form.name} adlı kullanıcının bilgileri güncellenecek ve açık oturumları kapatılacak.${!form.isActive ? ' Bu kişi yeniden giriş yapamayacak.' : ''} Devam edilsin mi?`)) return;
    setSaving(true); setError('');
    try {
      await api(editing ? `/api/users/${editing}` : '/api/users', { method: editing ? 'PUT' : 'POST', body: JSON.stringify(form) });
      if (editing === me.data?.id) { qc.clear(); logout(); return; }
      notify(editing ? 'Kullanıcı güncellendi. Önceki oturumları kapatıldı.' : 'Kullanıcı oluşturuldu.');
      reset();
      await qc.invalidateQueries({ queryKey: ['users'] });
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'İşlem tamamlanamadı. Yeniden deneyin.'); }
    finally { setSaving(false); }
  }

  if (me.isPending) return <p role="status">Hesap yetkileriniz kontrol ediliyor…</p>;
  if (me.isError) return <p role="alert">Hesap bilgileriniz alınamadı. Sayfayı yenileyip yeniden deneyin.</p>;
  if (me.data.role !== 'Admin') return <Card className="p-5"><h1 className="font-semibold">Bu alan yöneticiler içindir</h1><p className="mt-2 text-sm">Hesap değişiklikleri için yöneticinizle görüşün.</p></Card>;

  return <>
    <PageHeader title="Kullanıcılar" description="Çalışan hesaplarını oluşturun, yetkilerini düzenleyin ve kullanılmayan hesapları kapatın." />
    <div className="grid gap-4 xl:grid-cols-[1fr_380px]">
      <div className="space-y-3">
        {users.isPending && <p role="status">Kullanıcılar yükleniyor…</p>}
        {users.isError && <p role="alert">Kullanıcılar yüklenemedi. <button className="underline" onClick={() => users.refetch()}>Yeniden dene</button></p>}
        {users.data?.length === 0 && <Card className="p-5">Henüz kullanıcı bulunmuyor.</Card>}
        {users.data?.map(user => <Card key={user.id} className="flex flex-wrap items-center justify-between gap-3 p-5">
          <div className="min-w-0"><h2 className="font-semibold">{user.name}{user.id === me.data.id && ' (Siz)'}</h2><p className="break-all text-sm text-[#6d7175]">{user.email}</p></div>
          <div className="flex items-center gap-3"><div className="text-right"><Badge tone={user.isActive ? 'green' : 'neutral'}>{user.isActive ? 'Etkin' : 'Kapalı'}</Badge><div className="mt-1 text-xs text-[#6d7175]">{turkce(user.role)}</div></div><button disabled={saving} onClick={() => edit(user)} className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50" aria-label={`${user.name} hesabını düzenle`}>Düzenle</button></div>
        </Card>)}
      </div>
      <Card className="h-fit p-5">
        <h2 className="font-semibold">{editing ? 'Kullanıcıyı düzenle' : 'Yeni kullanıcı'}</h2>
        <form onSubmit={submit} className="mt-4 space-y-3">
          <fieldset disabled={saving} className="space-y-3 disabled:opacity-60">
            <label className="block"><span className="label">Ad soyad</span><input id="user-name" className="input mt-1.5" required maxLength={160} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
            <label className="block"><span className="label">E-posta</span><input className="input mt-1.5" type="email" required maxLength={320} autoComplete="off" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /></label>
            <label className="block"><span className="label">{editing ? 'Yeni şifre (isteğe bağlı)' : 'Şifre'}</span><input className="input mt-1.5" type="password" required={!editing} minLength={10} maxLength={256} autoComplete="new-password" aria-describedby="password-help" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} /></label>
            <p id="password-help" className="text-xs text-[#6d7175]">{editing ? 'Boş bırakırsanız mevcut şifre korunur. ' : ''}Yeni şifre en az 10 karakter olmalı; yalnızca ilgili kişiye güvenli bir kanaldan iletin.</p>
            <label className="block"><span className="label">Rol</span><select className="input mt-1.5" value={form.role} onChange={e => setForm({ ...form, role: e.target.value })}>{['Analyst', 'Partner', 'Admin'].map(role => <option key={role} value={role}>{turkce(role)}</option>)}</select></label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />Hesap etkin; giriş yapabilir</label>
            {editing && <p className="text-xs text-[#6d7175]">Kaydettiğinizde bu kişinin açık oturumları kapanır. Kendi hesabınızı düzenliyorsanız yeniden giriş yapmanız gerekir. Son etkin yönetici kapatılamaz.</p>}
            {error && <p role="alert" className="text-sm text-[#8e1f0b]">{error}</p>}
            <button className="w-full rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white">{saving ? 'Kaydediliyor…' : editing ? 'Değişiklikleri kaydet' : 'Kullanıcı oluştur'}</button>
            {editing && <button type="button" className="w-full rounded-lg border px-4 py-2 text-sm" onClick={reset}>Düzenlemeden vazgeç</button>}
          </fieldset>
        </form>
      </Card>
    </div>
  </>;
}
