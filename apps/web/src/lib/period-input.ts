export const periodGroups = [
  { title: 'Satış ve kesintiler', fields: ['grossSales', 'vat', 'refunds', 'cancellations', 'chargebacks', 'customerPaidShipping', 'giftCardTopups'] },
  { title: 'Sipariş ve müşteriler', fields: ['orders', 'sessions', 'newCustomers', 'returningCustomers'] },
  { title: 'Ürün ve operasyon giderleri', fields: ['cogs', 'paymentFees', 'fulfillmentCosts', 'shippingSubsidy', 'otherVariableCosts'] },
  { title: 'Reklam harcamaları', fields: ['metaSpend', 'googleSpend', 'tikTokSpend', 'influencerSpend', 'otherAdSpend'] },
] as const;
export type PeriodField = typeof periodGroups[number]['fields'][number];
export type PeriodInput = Record<PeriodField, number> & { brandId: string; dealId: string; year: number; month: number };
export type PeriodSnapshot = PeriodInput & { id: string; updatedAt: string; brand: { name: string }; deal: { name: string; currency: string } };
export const periodLabels: Record<PeriodField, string> = {
  grossSales: 'Brüt satış', vat: 'KDV tutarı', refunds: 'İadeler', cancellations: 'İptaller', chargebacks: 'Ters ibrazlar',
  customerPaidShipping: 'Müşterinin ödediği kargo', giftCardTopups: 'Hediye kartı yüklemeleri', orders: 'Sipariş sayısı',
  sessions: 'Site ziyareti', newCustomers: 'Yeni müşteri sayısı', returningCustomers: 'Tekrar alışveriş yapan müşteri sayısı',
  cogs: 'Ürün maliyeti', paymentFees: 'Ödeme sistemi giderleri', fulfillmentCosts: 'Sipariş hazırlama giderleri',
  shippingSubsidy: 'Kargo desteği', otherVariableCosts: 'Diğer değişken giderler', metaSpend: 'Meta harcaması',
  googleSpend: 'Google harcaması', tikTokSpend: 'TikTok harcaması', influencerSpend: 'Influencer harcaması', otherAdSpend: 'Diğer reklam harcamaları',
};
export function periodNumber(raw: string, count = false, signed = false): number {
  const value = raw.trim();
  const pattern = signed ? /^-?(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d{1,4})?$/ : /^(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d{1,4})?$/;
  if (!pattern.test(value)) throw new Error('Sayıyı 1234,56 biçiminde yazın. Bilinmeyen tutarı boşken sıfır kabul etmeyin.');
  const n = Number(value.replaceAll('.', '').replace(',', '.'));
  if (!Number.isFinite(n) || Math.abs(n) >= 100_000_000_000_000 || (count && (!Number.isInteger(n) || n > 2_147_483_647)))
    throw new Error(count ? 'Adet alanına geçerli bir tam sayı yazın.' : 'Tutar izin verilen sınırı aşıyor.');
  const canonical = value.replaceAll('.', '').replace(',', '.').replace(/^(-?)0+(?=\d)/, '$1').replace(/(\.\d*?)0+$/, '$1').replace(/\.$/, '');
  if (n !== 0 && String(n) !== canonical) throw new Error('Tutar bu ekranda hassasiyeti korunarak işlenemiyor. Daha küçük bir tutar girin.');
  return n;
}

export function targetMargin(raw: string): number {
  const value = periodNumber(raw);
  if (value > 100 || (raw.trim().split(',')[1]?.length ?? 0) > 2)
    throw new Error('Marjı %0–%100 arasında, en fazla iki ondalıkla yazın.');
  return Number((value / 100).toFixed(4));
}

export function planningHours(raw: string): number {
  const value = periodNumber(raw);
  if (value > 168 || (raw.trim().split(',')[1]?.length ?? 0) > 2)
    throw new Error('Saati 0–168 arasında, en fazla iki ondalıkla yazın.');
  return value;
}
