"use client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import {FeedbackHost} from "@/components/feedback";
export function Providers({children}:{children:React.ReactNode}){const [client]=useState(()=>new QueryClient({defaultOptions:{queries:{staleTime:15_000,retry:1}}}));return <QueryClientProvider client={client}>{children}<FeedbackHost/></QueryClientProvider>}
