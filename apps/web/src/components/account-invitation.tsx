'use client';
import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

export type MailStatus = { enabled: boolean; configured: boolean; ready: boolean };
export function AccountInvitation({brandId}: {brandId?: string}) {
  const status = useQuery({queryKey:['account-mail-status'],queryFn:()=>api<MailStatus>('/api/account-mail/status')});
  const qc=useQueryClient(); const [busy,setBusy]=useState(false); const [error,setError]=useState(''); const [notice,setNotice]=useState(''); const [dirty,setDirty]=useState(false); useUnsavedChanges(dirty);
  async function submit(e:FormEvent<HTMLFormElement>) {
    e.preventDefault(); if(busy)return; const form=e.currentTarget; const fields=new FormData(form);
    if(!confirm(`${fields.get('email')} adresine ${brandId ? 'bu markanın müşteri hesabı' : 'seçtiğiniz ekip rolü'} için davet gönderilsin mi? Adresi ve yetkiyi kontrol edin.`))return;
    setBusy(true);setError('');setNotice('');
    try {await api('/api/account-mail/invitations',{method:'POST',body:JSON.stringify({email:fields.get('email'),name:fields.get('name'),role:brandId?'BrandClient':fields.get('role'),brandId:brandId||null})});form.reset();setDirty(false);setNotice('Davet sıraya alındı. Kişi kendi şifresini belirleyince giriş yapabilir.');await Promise.all([qc.invalidateQueries({queryKey:['users']}),qc.invalidateQueries({queryKey:['portal-admin',brandId]}),qc.invalidateQueries({queryKey:['account-mail-deliveries']})]);}
    catch(e){setError(e instanceof Error?e.message:'Davet oluşturulamadı.');}finally{setBusy(false);}
  }
  return <section className="space-y-3 rounded-xl border bg-white p-5"><h2 className="font-semibold">{brandId?'Marka yetkilisini e-postayla davet et':'Çalışanı e-postayla davet et'}</h2><p className="text-sm">Şifreyi siz oluşturmazsınız. Davet 24 saat geçerlidir. Bu bölüm yalnız yeni hesap içindir; mevcut hesabın şifresi giriş ekranından yenilenebilir.</p>
    {status.isPending?<p role="status">E-posta hizmeti kontrol ediliyor…</p>:status.isError?<p role="alert">E-posta durumu alınamadı. Sayfayı yenileyin.</p>:!status.data.ready&&<p role="status">E-posta gönderimi kapalı veya ayarları eksik. Bilgiler sunucuda tamamlanana kadar davet gönderilemez; mevcut hesap açma yöntemi kullanılabilir.</p>}
    <form onSubmit={submit} onChange={()=>setDirty(true)}><fieldset disabled={busy||!status.data?.ready} className="grid gap-3 sm:grid-cols-2"><label className="text-sm">Davet edilecek kişinin adı<input className="input mt-1" name="name" required maxLength={160}/></label><label className="text-sm">Davet e-posta adresi<input className="input mt-1" name="email" type="email" required maxLength={320}/></label>{!brandId&&<label className="text-sm">Davet yetkisi<select name="role" className="input mt-1"><option value="Analyst">Analist</option><option value="Partner">Ortak</option><option value="Admin">Yönetici</option></select></label>}<button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">{busy?'Hazırlanıyor…':'Hesabı oluştur ve davet gönder'}</button></fieldset></form>
    {error&&<p role="alert" className="text-sm text-red-700">{error}</p>}{notice&&<p role="status" className="text-sm">{notice}</p>}<Link href="/mail-deliveries" className="text-sm underline">Gönderim durumları ve bekleyen davetler</Link>
  </section>;
}
