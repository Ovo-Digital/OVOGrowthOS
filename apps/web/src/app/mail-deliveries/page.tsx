'use client';
import {useState} from 'react';
import {useQuery,useQueryClient} from '@tanstack/react-query';
import {api,type SessionUser} from '@/lib/api';
import {Card,PageHeader} from '@/components/ui/core';
import type {MailStatus} from '@/components/account-invitation';

type MailSource='account'|'notification'|'test';
type MailKind='invitation'|'password'|'report'|'daily'|'task'|'conversation'|'test';
type MailState='Pending'|'Sending'|'Sent'|'Uncertain'|'Cancelled';
type Message={id:string;source:MailSource;type:MailKind;recipient:string;name:string;userId:string;status:MailState;reason:string|null;createdAt:string;attemptedAt:string|null;finishedAt:string|null;resend:'invitation'|'notification'|null};
type Center={items:Message[];total:number;page:number;pageSize:number;summary:Record<string,number>;note:string};

const stateLabels:Record<MailState,string>={Pending:'Gönderim sırası bekliyor',Sending:'Gönderim deneniyor',Sent:'E-posta sunucusu kabul etti',Uncertain:'Gönderim doğrulanamadı',Cancelled:'Gönderilmeden kapatıldı'};
const typeLabels:Record<MailKind,string>={invitation:'Hesap daveti',password:'Şifre yenileme',report:'Rapor paylaşımı',daily:'Günlük görev özeti',task:'Görev son tarihi',conversation:'Müşteri konuşması',test:'Deneme e-postası'};
const typeOptions=['invitation','password','report','daily','task','conversation','test'] as const;
const stateOptions=['Pending','Sending','Sent','Uncertain','Cancelled'] as const;
const toDate=(v:string)=>v?`${v}T00:00:00.000Z`:'';

export default function Page(){
  const me=useQuery({queryKey:['session-user'],queryFn:()=>api<SessionUser>('/api/auth/me')});
  const admin=me.data?.role==='Admin';
  const status=useQuery({queryKey:['account-mail-status'],queryFn:()=>api<MailStatus>('/api/account-mail/status'),enabled:admin});
  const[filters,setFilters]=useState({type:'',status:'',from:'',to:'',q:''});
  const query=new URLSearchParams();
  if(filters.type)query.set('type',filters.type);
  if(filters.status)query.set('status',filters.status);
  if(filters.from)query.set('from',toDate(filters.from));
  if(filters.to)query.set('to',toDate(filters.to));
  if(filters.q.trim())query.set('q',filters.q.trim());
  const qs=query.toString();
  const list=useQuery({queryKey:['mail-center',qs],queryFn:()=>api<Center>(`/api/mail-center/messages${qs?`?${qs}`:''}`),enabled:admin,refetchInterval:30000});
  const qc=useQueryClient();const[busy,setBusy]=useState(false);const[notice,setNotice]=useState('');const[error,setError]=useState('');
  async function refresh(){setBusy(true);setError('');setNotice('');try{await Promise.all([list.refetch(),status.refetch()]);}catch(e){setError(e instanceof Error?e.message:'Durumlar yenilenemedi.');}finally{setBusy(false);}}
  async function resend(item:Message){
    if(busy)return;
    setError('');setNotice('');
    try{
      if(item.resend==='invitation'){
        if(!confirm(`${item.recipient} adresi için yeni davet oluşturulsun mu? Önceki bağlantılar geçersiz olacaktır.`))return;
        setBusy(true);
        await api(`/api/account-mail/invitations/${item.userId}/resend`,{method:'POST'});
        setNotice('Yeni davet sıraya alındı. Önceki bağlantılar geçersiz.');
      }else{
        const reason=window.prompt('Bu iletiyi neden yeniden gönderiyorsunuz? Gönderim günlüğüne gerekçe olarak yazılır.', '');
        if(!reason||!reason.trim())return;
        setBusy(true);
        await api(`/api/mail-center/notifications/${item.id}/resend`,{method:'POST',body:JSON.stringify({reason:reason.trim()})});
        setNotice('Yeniden gönderim sıraya alındı. Alıcının hesabı, marka erişimi ve tercihi gönderilmeden önce yeniden kontrol edilir.');
      }
      await qc.invalidateQueries({queryKey:['mail-center']});
      await qc.invalidateQueries({queryKey:['account-mail-deliveries']});
    }catch(e){setError(e instanceof Error?e.message:'İşlem yapılamadı.');}
    finally{setBusy(false);}
  }
  if(me.isPending)return <p role="status">Yetki kontrol ediliyor…</p>;
  if(me.isError)return <p role="alert">Hesap bilgisi alınamadı.</p>;
  if(!admin)return <p>Gönderim takibi yalnız yöneticilere açıktır.</p>;
  const summary=list.data?.summary??{};
  const chips=stateOptions.filter(k=>summary[k]).map(k=>`${stateLabels[k]}: ${summary[k]}`);
  return <><PageHeader title="Gönderim merkezi" description="Davet, şifre yenileme, bildirim, rapor ve deneme iletilerini tek ekranda izleyin ve gerekçeli yeniden gönderim yapın."/>
    <Card className="mb-4 space-y-3 p-5">
      <p>{status.isError?'E-posta hizmetinin durumu alınamadı.':status.data?.ready?'E-posta hizmeti açık.':status.isPending?'E-posta hizmeti kontrol ediliyor…':'E-posta gönderimi kapalı veya ayarları eksik.'}</p>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
        <label className="text-sm">Tür<select value={filters.type} onChange={e=>setFilters({...filters,type:e.target.value})} className="mt-1 w-full rounded-lg border px-2 py-2"><option value="">Tümü</option>{typeOptions.map(k=><option key={k} value={k}>{typeLabels[k]}</option>)}</select></label>
        <label className="text-sm">Durum<select value={filters.status} onChange={e=>setFilters({...filters,status:e.target.value})} className="mt-1 w-full rounded-lg border px-2 py-2"><option value="">Tümü</option>{stateOptions.map(k=><option key={k} value={k}>{stateLabels[k]}</option>)}</select></label>
        <label className="text-sm">Başlangıç<input type="date" value={filters.from} onChange={e=>setFilters({...filters,from:e.target.value})} className="mt-1 w-full rounded-lg border px-2 py-2"/></label>
        <label className="text-sm">Bitiş<input type="date" value={filters.to} onChange={e=>setFilters({...filters,to:e.target.value})} className="mt-1 w-full rounded-lg border px-2 py-2"/></label>
        <label className="text-sm">Alıcı ara<input value={filters.q} onChange={e=>setFilters({...filters,q:e.target.value})} placeholder="E-posta adresi" className="mt-1 w-full rounded-lg border px-2 py-2"/></label>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50" disabled={busy||list.isFetching} onClick={()=>void refresh()}>Listeyi yenile</button>
        <button className="rounded-lg border px-3 py-2 text-sm" onClick={()=>setFilters({type:'',status:'',from:'',to:'',q:''})}>Filtreleri temizle</button>
        <span className="text-sm text-[#6d7175]">{list.data?`${list.data.total} kayıt`:'—'}</span>
      </div>
      {chips.length>0&&<p className="text-sm text-[#4a4d50]">{chips.join(' · ')}</p>}
      <p className="text-sm">{list.data?.note}</p>
      <p className="text-sm">“E-posta sunucusu kabul etti” gelen kutusuna teslim veya okunma kanıtı değildir. Doğrulanamayan gönderimler otomatik tekrarlanmaz; önce alıcıdan kontrol etmesini isteyin. Şifre yenileme bağlantısı yeniden gönderilemez; kişi giriş ekranından yeni bağlantı istemelidir.</p>
    </Card>
    {error&&<p role="alert" className="mb-3">{error}</p>}{notice&&<p role="status" className="mb-3">{notice}</p>}
    {list.isPending?<p role="status">Gönderimler yükleniyor…</p>:list.isError?<p role="alert">Gönderimler alınamadı. Yeniden deneyin.</p>:!list.data.items.length?<p>Seçtiğiniz ölçütlere uyan gönderim bulunamadı.</p>:<div className="space-y-3">{list.data.items.map(item=><Card key={`${item.source}-${item.id}`} className="space-y-2 p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2"><h2 className="break-words font-semibold">{typeLabels[item.type]} · {item.name||item.recipient}</h2><span className="text-xs text-[#6d7175]">{item.source==='test'?'Deneme':item.source==='account'?'Hesap e-postası':'Bildirim'}</span></div>
      <p className="break-all text-sm">{item.recipient}</p>
      <p className="text-sm">{stateLabels[item.status]} · {new Date(item.createdAt).toLocaleString('tr-TR',{timeZone:'Europe/Istanbul'})} (Türkiye){item.finishedAt?` · bitiş ${new Date(item.finishedAt).toLocaleString('tr-TR',{timeZone:'Europe/Istanbul'})}`:''}</p>
      {item.reason&&<p className="text-sm text-[#4a4d50]">{item.reason}</p>}
      {item.type==='password'&&<p className="text-sm text-[#6d7175]">Şifre yenileme bağlantısı yeniden gönderilemez; kişi giriş ekranından yeni bağlantı istemelidir.</p>}
      {item.resend&&<button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50" disabled={busy||(item.resend==='invitation'&&!status.data?.ready)} onClick={()=>void resend(item)}>{item.resend==='invitation'?'Önceki daveti geçersiz kıl ve yenisini gönder':'Yeniden gönder'}</button>}
    </Card>)}</div>}
  </>;
}
