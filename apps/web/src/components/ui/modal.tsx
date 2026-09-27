"use client";
import { createContext, useCallback, useContext, useEffect, useId, useRef, useState } from "react";

export type ConfirmRequest = {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: "default" | "danger";
};

export type PromptRequest = {
  title: string;
  message?: string;
  label: string;
  defaultValue?: string;
  placeholder?: string;
  inputType?: "text" | "number" | "url" | "date";
  inputMode?: "text" | "decimal" | "numeric";
  multiline?: boolean;
  required?: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
  validate?: (value: string) => string | null;
};

type DialogState =
  | { kind: "confirm"; request: ConfirmRequest; resolve: (value: boolean) => void }
  | { kind: "prompt"; request: PromptRequest; resolve: (value: string | null) => void };

type DialogApi = {
  confirm: (request: ConfirmRequest) => Promise<boolean>;
  prompt: (request: PromptRequest) => Promise<string | null>;
};

const DialogContext = createContext<DialogApi | null>(null);

export function useDialog(): DialogApi {
  const value = useContext(DialogContext);
  if (!value) throw new Error("Modal bileşeni DialogProvider içinde kullanılmalı.");
  return value;
}

const focusable = "button:not([disabled]), [href], input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex='-1'])";

export function Modal({ title, message, children, footer, onClose, initialFocus }: {
  title: string;
  message?: string;
  children?: React.ReactNode;
  footer: React.ReactNode;
  onClose: () => void;
  initialFocus?: React.RefObject<HTMLElement | null>;
}) {
  const titleId = useId();
  const messageId = useId();
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    const panel = panelRef.current;
    const target = initialFocus?.current ?? panel?.querySelector<HTMLElement>(focusable);
    target?.focus();
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const keydown = (event: KeyboardEvent) => {
      if (event.key === "Escape") { event.preventDefault(); onClose(); return; }
      if (event.key !== "Tab" || !panel) return;
      const items = Array.from(panel.querySelectorAll<HTMLElement>(focusable));
      if (!items.length) return;
      const first = items[0], last = items[items.length - 1];
      const active = document.activeElement;
      if (event.shiftKey && (active === first || !panel.contains(active))) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && active === last) { event.preventDefault(); first.focus(); }
    };
    document.addEventListener("keydown", keydown, true);
    return () => {
      document.removeEventListener("keydown", keydown, true);
      document.body.style.overflow = overflow;
      previous?.focus?.();
    };
  }, [initialFocus, onClose]);

  return (
    <div className="fixed inset-0 z-[120] flex items-end justify-center bg-black/40 p-3 sm:items-center sm:p-6" onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
      <div ref={panelRef} role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={message ? messageId : undefined}
        className="w-full max-w-md rounded-2xl bg-white p-5 shadow-xl">
        <h2 id={titleId} className="text-base font-semibold">{title}</h2>
        {message && <p id={messageId} className="mt-2 text-sm text-[#6d7175]">{message}</p>}
        {children}
        <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">{footer}</div>
      </div>
    </div>
  );
}

const button = "rounded-lg px-3.5 py-2 text-sm font-semibold";
const cancelClass = `${button} border bg-white`;

export function DialogProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<DialogState | null>(null);
  const inputRef = useRef<HTMLInputElement | HTMLTextAreaElement>(null);

  const confirm = useCallback((request: ConfirmRequest) => new Promise<boolean>(resolve => setState({ kind: "confirm", request, resolve })), []);
  const prompt = useCallback((request: PromptRequest) => new Promise<string | null>(resolve => setState({ kind: "prompt", request, resolve })), []);
  const close = useCallback(() => setState(null), []);

  const footer = (confirmLabel: string, cancelLabel: string, onConfirm: () => void, tone?: "default" | "danger") => (
    <>
      <button type="button" className={cancelClass} onClick={close}>{cancelLabel}</button>
      <button type="button" onClick={onConfirm}
        className={`${button} ${tone === "danger" ? "bg-[#d72c0d] text-white" : "bg-[#303030] text-white"}`}>{confirmLabel}</button>
    </>
  );

  let body: React.ReactNode = null;
  if (state?.kind === "confirm") {
    const { request, resolve } = state;
    body = (
      <Modal title={request.title} message={request.message} onClose={close}
        footer={footer(request.confirmLabel ?? "Devam et", request.cancelLabel ?? "Vazgeç", () => { resolve(true); close(); }, request.tone)} />
    );
  } else if (state?.kind === "prompt") {
    const { request, resolve } = state;
    body = <PromptDialog request={request} inputRef={inputRef} onClose={close} resolve={resolve} footerFactory={footer} />;
  }

  return <DialogContext.Provider value={{ confirm, prompt }}>{children}{body}</DialogContext.Provider>;
}

function PromptDialog({ request, inputRef, onClose, resolve, footerFactory }: {
  request: PromptRequest;
  inputRef: React.RefObject<HTMLInputElement | HTMLTextAreaElement | null>;
  onClose: () => void;
  resolve: (value: string | null) => void;
  footerFactory: (confirmLabel: string, cancelLabel: string, onConfirm: () => void, tone?: "default" | "danger") => React.ReactNode;
}) {
  const [value, setValue] = useState(request.defaultValue ?? "");
  const [error, setError] = useState<string | null>(null);
  const inputId = useId();

  const submit = () => {
    const trimmed = value.trim();
    if (request.required && !trimmed) { setError("Bu alan boş bırakılamaz."); return; }
    const problem = request.validate?.(trimmed) ?? null;
    if (problem) { setError(problem); return; }
    resolve(trimmed || null);
    onClose();
  };

  return (
    <Modal title={request.title} message={request.message} onClose={onClose} initialFocus={inputRef}
      footer={footerFactory(request.confirmLabel ?? "Kaydet", request.cancelLabel ?? "Vazgeç", submit)}>
      <div className="mt-4">
        <label htmlFor={inputId} className="label">{request.label}</label>
        {request.multiline
          ? <textarea id={inputId} ref={inputRef as React.RefObject<HTMLTextAreaElement>} className="input mt-1.5" rows={3}
              value={value} placeholder={request.placeholder} onChange={event => { setValue(event.target.value); setError(null); }} />
          : <input id={inputId} ref={inputRef as React.RefObject<HTMLInputElement>} className="input mt-1.5" type={request.inputType ?? "text"}
              inputMode={request.inputMode} value={value} placeholder={request.placeholder}
              onChange={event => { setValue(event.target.value); setError(null); }} />}
        {error && <p role="alert" className="mt-1.5 text-sm text-[#8e1f0b]">{error}</p>}
      </div>
    </Modal>
  );
}
