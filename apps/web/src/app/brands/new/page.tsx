"use client";
import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { Card, PageHeader } from "@/components/ui/core";
import { FieldHint } from "@/components/field-hint";

const hints: Record<string, string> = {
    name: "Markanın müşterilerin bildiği kısa adı. Listelerde ve raporlarda böyle görünür.",
    legalName: "Fatura ve sözleşmelerde geçen resmî şirket unvanı.",
    website: "Markanın satış yaptığı internet adresi.",
    industry: "Markanın faaliyet gösterdiği ana sektör (ör. giyim, kozmetik).",
    contactName: "Marka tarafında görüştüğümüz kişinin adı.",
    contactEmail: "Yetkiliyle iletişim için kullanılacak e-posta adresi.",
    platform: "Markanın mağazasının çalıştığı teknik altyapı. Sipariş ve veri bağlantılarını etkiler.",
    channels: "Cironun nereden geldiğini görmek için kanalları yazın (ör. Web sitesi, Pazaryeri). Değerlendirmede ciro bu kanallara ayrı ayrı girilir.",
};
const maxChannels = 30;

export default function NewBrand() {
    const router = useRouter();
    const [channels, setChannels] = useState<string[]>(["Web sitesi", "Pazaryeri"]);
    const [error, setError] = useState("");
    const [savedBrandId, setSavedBrandId] = useState("");
    const [busy, setBusy] = useState(false);

    function channelError(list: string[]) {
        const names = list.map((x) => x.trim()).filter((x) => x.length > 0);
        if (names.some((x) => x.length < 2)) return "Kanal adı en az 2 karakter olmalıdır.";
        if (names.some((x) => x.length > 80)) return "Kanal adı en fazla 80 karakter olabilir.";
        if (new Set(names.map((x) => x.toLocaleLowerCase("tr-TR"))).size !== names.length)
            return "Her kanal adı bir kez yazılabilir.";
        return "";
    }

    async function submit(e: FormEvent<HTMLFormElement>) {
        e.preventDefault();
        const message = channelError(channels);
        if (message) { setError(message); return; }
        const f = new FormData(e.currentTarget);
        setBusy(true); setError("");
        try {
            const b = await api<{ id: string }>("/api/brands", {
                method: "POST",
                body: JSON.stringify({
                    name: f.get("name"), legalName: f.get("legalName"), website: f.get("website"),
                    country: "TR", currency: "TRY", industry: f.get("industry"), businessModel: "Dtc",
                    platform: f.get("platform"), status: "Lead",
                    contactName: f.get("contactName"), contactEmail: f.get("contactEmail"),
                }),
            });
            const names = [...new Set(channels.map((x) => x.trim()).filter((x) => x.length > 0))];
            for (const name of names) {
                try {
                    await api(`/api/brands/${b.id}/sales-channels`, { method: "POST", body: JSON.stringify({ name }) });
                } catch (x) {
                    setSavedBrandId(b.id);
                    setError(`Marka kaydedildi ama "${name}" kanalı eklenemedi: ${x instanceof Error ? x.message : "bilinmeyen hata"}. Kanalları sonra marka sayfasından ekleyebilirsiniz.`);
                    setBusy(false);
                    return;
                }
            }
            router.push(`/evaluations/new?brandId=${b.id}`);
        } catch (x) { setError(x instanceof Error ? x.message : "Marka kaydedilemedi"); setBusy(false); }
    }

    return <>
        <PageHeader title="Marka ekle" description="Değerlendirmeye başlamadan önce marka bilgilerini kaydedin." />
        <Card className="max-w-3xl p-6">
            <form onSubmit={submit} className="grid gap-5 md:grid-cols-2">
                <Field n="name" l="Marka adı" />
                <Field n="legalName" l="Ticari unvan" />
                <Field n="website" l="İnternet sitesi" />
                <Field n="industry" l="Sektör" />
                <Field n="contactName" l="Yetkili adı" />
                <Field n="contactEmail" l="Yetkili e-posta adresi" t="email" />
                <label>
                    <span className="mb-1.5 block text-xs font-semibold">E-ticaret altyapısı{hints.platform && <FieldHint text={hints.platform} />}</span>
                    <select name="platform" className="input">
                        <option>Shopify</option>
                        <option>GrandNode</option>
                        <option value="Custom">Özel yazılım</option>
                        <option value="Other">Diğer</option>
                    </select>
                </label>
                <div className="md:col-span-2 rounded-lg border p-4">
                    <span className="mb-1.5 block text-xs font-semibold">SATIŞ KANALLARI (İSTEĞE BAĞLI)<FieldHint text={hints.channels} /></span>
                    <p className="mb-3 text-sm text-[#6d7175]">Değerlendirmede aylık ciro bu kanallara ayrı ayrı yazılır.</p>
                    <div className="space-y-2">
                        {channels.map((c, i) => <div key={i} className="flex gap-2">
                            <input className="input" value={c} maxLength={80} disabled={busy}
                                onChange={(e) => setChannels(channels.map((x, j) => j === i ? e.target.value : x))}
                                placeholder="Örn. Web sitesi" aria-label={`Satış kanalı ${i + 1}`} />
                            <button type="button" className="shrink-0 text-sm underline disabled:opacity-40" disabled={busy}
                                onClick={() => setChannels(channels.filter((_, j) => j !== i))}>Kaldır</button>
                        </div>)}
                    </div>
                    {channels.length < maxChannels && <button type="button" className="mt-3 rounded-lg border px-4 py-2 text-sm font-semibold" disabled={busy}
                        onClick={() => setChannels([...channels, ""])}>Kanal ekle</button>}
                </div>
                {error && <p className="md:col-span-2 text-sm text-[#d72c0d]">{error}</p>}
                {savedBrandId && <p className="md:col-span-2 text-sm"><Link className="underline" href={`/evaluations/new?brandId=${savedBrandId}`}>Yine de değerlendirmeye başla</Link></p>}
                <div className="md:col-span-2 flex justify-end">
                    <button className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={busy}>
                        {busy ? "Kaydediliyor…" : "Kaydet ve değerlendirmeye başla"}
                    </button>
                </div>
            </form>
        </Card>
    </>;
}
function Field({ n, l, t = "text" }: { n: string; l: string; t?: string }) {
    return <label><span className="mb-1.5 block text-xs font-semibold">{l}{hints[n] && <FieldHint text={hints[n]} />}</span><input required={n === "name"} name={n} type={t} className="input" /></label>;
}
