export const brandTabs = [
  { id: 'ozet', label: 'Genel bakış' },
  { id: 'gorusme', label: 'Görüşme ve ekip' },
  { id: 'performans', label: 'Performans' },
  { id: 'baglantilar', label: 'Veri bağlantıları' },
] as const;

export type BrandTabId = (typeof brandTabs)[number]['id'];

const anchorTabs: Record<string, BrandTabId> = { 'team-work': 'gorusme' };

export const isBrandTab = (value: string | null): value is BrandTabId =>
  brandTabs.some(tab => tab.id === value);

const cleanHash = (hash: string) => {
  const raw = hash.replace(/^#/, '');
  try { return decodeURIComponent(raw); } catch { return raw; }
};

export function resolveBrandTab(hash: string, search: string | null): { tab: BrandTabId; anchor: string | null } {
  const anchor = cleanHash(hash);
  const anchorTab = anchorTabs[anchor] ?? null;
  const fromHash = isBrandTab(anchor) ? anchor : null;
  const fromSearch = isBrandTab(search) ? search : null;
  const tab = fromHash ?? anchorTab ?? fromSearch ?? 'ozet';
  return { tab, anchor: anchorTab === tab ? anchor : null };
}
