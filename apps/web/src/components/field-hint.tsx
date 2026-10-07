'use client';

// Etiketin yanında duran küçük soru işareti; üzerine gelince veya odaklanınca
// kısa bir açıklama gösterir. Balon sayfada yer kaplamaz, düzeni kaydırmaz.
export function FieldHint({ text }: { text: string }) {
    return (
        <span className="group relative ml-1 inline-flex align-middle">
            <button
                type="button"
                tabIndex={0}
                aria-label={`Açıklama: ${text}`}
                className="grid h-4 w-4 place-items-center rounded-full border border-[#c4cdd5] text-[10px] font-bold leading-none text-[#6d7175] hover:border-[#303030] hover:text-[#303030] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1"
            >
                ?
            </button>
            <span
                role="tooltip"
                className="invisible absolute left-1/2 top-full z-30 mt-1 w-60 max-w-[70vw] -translate-x-1/2 rounded-lg border bg-white p-2.5 text-left text-xs font-normal normal-case leading-relaxed text-[#303030] shadow-lg group-hover:visible group-focus-within:visible group-active:visible"
            >
                {text}
            </span>
        </span>
    );
}
