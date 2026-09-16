import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  outputFileTracingIncludes: { '/api/guide': ['./OVO_GROWTH_OS_KULLANIM_REHBERI.md', '../../OVO_GROWTH_OS_KULLANIM_REHBERI.md'] },
};

export default nextConfig;
