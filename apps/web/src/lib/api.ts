export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
export type SessionUser = { id: string; email: string; name: string; role: string };

export function token() { return typeof window === "undefined" ? null : localStorage.getItem("ovo_token"); }
export function isSessionValid() {
  const value = token(); if (!value) return false;
  try { const payload = JSON.parse(atob(value.split(".")[1].replace(/-/g,"+").replace(/_/g,"/"))); return Number(payload.exp) * 1000 > Date.now(); }
  catch { return false; }
}
export function logout() { localStorage.removeItem("ovo_token"); localStorage.removeItem("ovo_user"); window.location.replace("/login"); }
let inFlight = 0;
function trackBusy(delta: number) {
  inFlight = Math.max(0, inFlight + delta);
  if (typeof window !== "undefined") window.dispatchEvent(new CustomEvent("ovo:busy", { detail: { busy: inFlight > 0 } }));
}
export async function api<T>(path:string, init:RequestInit = {}):Promise<T> {
  const headers = new Headers(init.headers);
  if (!(typeof FormData !== "undefined" && init.body instanceof FormData)) headers.set("content-type", "application/json");
  const accessToken = token();
  if (accessToken) headers.set("authorization", `Bearer ${accessToken}`);
  trackBusy(1);
  try {
    const response = await fetch(`${API_URL}${path}`, { ...init, headers });
    if (response.status === 401) { logout(); throw new Error("Oturumunuz sona erdi. Lütfen yeniden giriş yapın."); }
    if (!response.ok) { const body = await response.json().catch(()=>({})); const validation=body.errors&&Object.values(body.errors).flat()[0];const message=body.error ?? validation ?? body.title ?? `İşlem tamamlanamadı (${response.status})`;if(typeof window!=="undefined")window.dispatchEvent(new CustomEvent("ovo:notice",{detail:{message,tone:"error"}}));throw new Error(String(message)); }
    if (response.status === 204) return undefined as T;
    return response.json();
  } finally { trackBusy(-1); }
}
export const money = (v:number, currency = "TRY") => new Intl.NumberFormat("tr-TR",{style:"currency",currency,maximumFractionDigits:0}).format(v);
export const moneyPrecise = (v:number, currency = "TRY") => new Intl.NumberFormat("tr-TR",{style:"currency",currency,minimumFractionDigits:2,maximumFractionDigits:4}).format(v);
export const percent = (v:number) => new Intl.NumberFormat("tr-TR",{style:"percent",maximumFractionDigits:2}).format(v);
