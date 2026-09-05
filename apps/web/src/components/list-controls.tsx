"use client";
import {Search} from "lucide-react";
import {useEffect,useState} from "react";

export type Paged<T>={items:T[];total:number;page:number;pageSize:number};
export function useDebouncedValue<T>(value:T,delay=300){const[debounced,setDebounced]=useState(value);useEffect(()=>{const timer=window.setTimeout(()=>setDebounced(value),delay);return()=>window.clearTimeout(timer)},[value,delay]);return debounced}

export function ListControls({search,onSearch,status,onStatus,statuses,sort,onSort,placeholder="Ara…",children}:{search:string;onSearch:(value:string)=>void;status:string;onStatus:(value:string)=>void;statuses:[string,string][];sort?:string;onSort?:(value:string)=>void;placeholder?:string;children?:React.ReactNode}){
  return <div className="flex flex-wrap items-center gap-3 border-b p-4">
    <label className="flex min-w-[260px] flex-1 items-center gap-2 rounded-lg border border-[#c9cccf] bg-white px-3 py-2 text-sm"><Search size={16} className="text-[#6d7175]"/><input aria-label={placeholder} className="w-full bg-transparent outline-none" placeholder={placeholder} value={search} onChange={e=>onSearch(e.target.value)}/></label>
    <select aria-label="Duruma göre filtrele" className="input max-w-[220px]" value={status} onChange={e=>onStatus(e.target.value)}><option value="">Tüm durumlar</option>{statuses.map(([value,label])=><option key={value} value={value}>{label}</option>)}</select>{onSort&&<select aria-label="Sıralama" className="input max-w-[190px]" value={sort} onChange={e=>onSort(e.target.value)}><option value="recent">En yeni</option><option value="oldest">En eski</option><option value="name">Ada göre A–Z</option><option value="nameDesc">Ada göre Z–A</option></select>}{children}
  </div>;
}

export function Pagination({page,pageSize,total,onPage}:{page:number;pageSize:number;total:number;onPage:(page:number)=>void}){
  const pages=Math.max(1,Math.ceil(total/pageSize));if(total<=pageSize)return null;
  return <div className="flex items-center justify-between border-t px-4 py-3 text-sm text-[#6d7175]"><span>{total} kayıttan {(page-1)*pageSize+1}–{Math.min(page*pageSize,total)} arası</span><div className="flex gap-2"><button className="rounded-lg border px-3 py-1.5 disabled:opacity-40" disabled={page<=1} onClick={()=>onPage(page-1)}>Önceki</button><span className="px-2 py-1.5">{page} / {pages}</span><button className="rounded-lg border px-3 py-1.5 disabled:opacity-40" disabled={page>=pages} onClick={()=>onPage(page+1)}>Sonraki</button></div></div>;
}

export function EmptyState({children}:{children:React.ReactNode}){return <div className="p-10 text-center text-sm text-[#6d7175]">{children}</div>}
