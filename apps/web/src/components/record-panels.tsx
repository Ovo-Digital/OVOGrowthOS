'use client';
import { FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, API_URL, token } from '@/lib/api';
import { notify } from '@/components/feedback';
import { Badge, Card } from '@/components/ui/core';
import { turkce, turkceTarih } from '@/lib/turkish';
export type Condition = {
    id: string;
    title: string;
    description: string;
    required: boolean;
    status: string;
    resolutionReason: string;
    evidenceUrl: string;
    resolvedBy: string;
    resolvedAt?: string;
};
export function ConditionsPanel({ conditions }: { conditions: Condition[] }) {
    const qc = useQueryClient();
    async function resolve(id: string, status: 'Satisfied' | 'Waived') {
        const reason =
            prompt(
                status === 'Waived'
                    ? 'Feragat nedenini yazın:'
                    : 'Tamamlama notunu yazabilirsiniz:',
            ) ?? '';
        if (status === 'Waived' && !reason.trim()) return;
        const evidenceUrl =
            prompt('Varsa kanıt veya belge bağlantısını yapıştırın:') ?? '';
        await api('/api/conditions/' + id, {
            method: 'PUT',
            body: JSON.stringify({ status, reason, evidenceUrl }),
        });
        notify(
            status === 'Waived'
                ? 'Koşul gerekçesiyle feragat edildi.'
                : 'Koşul tamamlandı.',
        );
        await qc.invalidateQueries();
    }
    return (
        <Card className="p-5">
            <h2 className="font-semibold">Anlaşma koşulları</h2>
            <div className="mt-4 space-y-3">
                {conditions.length ? (
                    conditions.map((x) => (
                        <div className="rounded-lg border p-3" key={x.id}>
                            <div className="flex flex-wrap items-start justify-between gap-2">
                                <div>
                                    <div className="font-semibold">
                                        {x.title}
                                        {x.required ? ' *' : ''}
                                    </div>
                                    <p className="mt-1 text-sm text-[#6d7175]">
                                        {x.description}
                                    </p>
                                </div>
                                <Badge
                                    tone={
                                        x.status === 'Satisfied'
                                            ? 'green'
                                            : x.status === 'Waived'
                                              ? 'yellow'
                                              : 'blue'
                                    }
                                >
                                    {turkce(x.status)}
                                </Badge>
                            </div>
                            {x.status === 'Pending' ? (
                                <div className="mt-3 flex gap-2">
                                    <button
                                        onClick={() =>
                                            resolve(x.id, 'Satisfied')
                                        }
                                        className="rounded-lg bg-[#008060] px-3 py-1.5 text-sm font-semibold text-white"
                                    >
                                        Tamamlandı
                                    </button>
                                    <button
                                        onClick={() => resolve(x.id, 'Waived')}
                                        className="rounded-lg border px-3 py-1.5 text-sm font-semibold"
                                    >
                                        Gerekçeyle feragat et
                                    </button>
                                </div>
                            ) : (
                                <div className="mt-3 text-xs text-[#6d7175]">
                                    {x.resolutionReason || 'Not girilmedi'} ·{' '}
                                    {x.resolvedBy}{' '}
                                    {x.resolvedAt &&
                                        '· ' + turkceTarih(x.resolvedAt)}
                                    {x.evidenceUrl && (
                                        <a
                                            className="ml-2 font-semibold text-[#005bd3]"
                                            href={x.evidenceUrl}
                                            target="_blank"
                                            rel="noreferrer"
                                        >
                                            Kanıtı aç
                                        </a>
                                    )}
                                </div>
                            )}
                        </div>
                    ))
                ) : (
                    <p className="text-sm text-[#6d7175]">
                        Bu kayıt için koşul bulunmuyor.
                    </p>
                )}
            </div>
        </Card>
    );
}
type Document = {
    id: string;
    fileName: string;
    contentType: string;
    size: number;
    note: string;
    uploadedBy: string;
    createdAt: string;
};
export function DocumentsPanel({
    entityType,
    entityId,
}: {
    entityType: string;
    entityId: string;
}) {
    const qc = useQueryClient();
    const { data = [] } = useQuery({
        queryKey: ['documents', entityType, entityId],
        queryFn: () =>
            api<Document[]>('/api/documents/' + entityType + '/' + entityId),
    });
    async function upload(e: FormEvent<HTMLFormElement>) {
        e.preventDefault();
        const target = e.currentTarget;
        const form = new FormData(target);
        await api('/api/documents/' + entityType + '/' + entityId, {
            method: 'POST',
            body: form,
        });
        notify('Belge kaydedildi.');
        target.reset();
        await qc.invalidateQueries({
            queryKey: ['documents', entityType, entityId],
        });
    }
    async function download(item: Document) {
        const response = await fetch(
            API_URL + '/api/documents/' + item.id + '/download',
            { headers: { authorization: 'Bearer ' + token() } },
        );
        if (!response.ok) {
            notify('Belge indirilemedi.', 'error');
            return;
        }
        const url = URL.createObjectURL(await response.blob());
        const link = document.createElement('a');
        link.href = url;
        link.download = item.fileName;
        link.click();
        URL.revokeObjectURL(url);
    }
    return (
        <Card className="p-5">
            <h2 className="font-semibold">Belgeler ve notlar</h2>
            <p className="mt-1 text-sm text-[#6d7175]">
                PDF, görsel, CSV veya Excel dosyası ekleyebilirsiniz. En fazla
                10 MB.
            </p>
            <form
                onSubmit={upload}
                className="mt-4 grid gap-3 sm:grid-cols-[1fr_1fr_auto]"
            >
                <input
                    required
                    name="file"
                    type="file"
                    accept=".pdf,.png,.jpg,.jpeg,.csv,.xlsx"
                    className="input"
                />
                <input
                    name="note"
                    className="input"
                    placeholder="Belgenin kısa açıklaması"
                />
                <button className="rounded-lg bg-[#303030] px-4 py-2 text-sm font-semibold text-white">
                    Belge ekle
                </button>
            </form>
            <div className="mt-4 space-y-2">
                {data.map((x) => (
                    <div
                        key={x.id}
                        className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm"
                    >
                        <div>
                            <button
                                className="font-semibold text-[#005bd3]"
                                onClick={() => download(x)}
                            >
                                {x.fileName}
                            </button>
                            <div className="text-xs text-[#6d7175]">
                                {x.note || 'Açıklama yok'} ·{' '}
                                {(x.size / 1024).toFixed(0)} KB · {x.uploadedBy}
                            </div>
                        </div>
                        <span className="text-xs text-[#6d7175]">
                            {turkceTarih(x.createdAt)}
                        </span>
                    </div>
                ))}
                {!data.length && (
                    <p className="text-sm text-[#6d7175]">
                        Henüz belge eklenmedi.
                    </p>
                )}
            </div>
        </Card>
    );
}
