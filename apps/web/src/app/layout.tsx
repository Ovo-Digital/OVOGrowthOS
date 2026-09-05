import type { Metadata } from "next";
import "./globals.css";
import { AppShell } from "@/components/app-shell";
import { Providers } from "@/components/providers";

export const metadata: Metadata = {
  title: "OVO Growth OS",
  description: "OVO Digital büyüme ve iş ortaklığı yönetim sistemi",
  icons: { icon: "/favicon.svg" },
};
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) { return <html lang="tr"><body><Providers><AppShell>{children}</AppShell></Providers></body></html>; }
