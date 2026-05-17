/** Convert a major-unit amount (e.g. 60) to minor units (6000). */
export function toMinorUnits(major: number): number {
  return Math.round(major * 100);
}

/** Convert a minor-unit amount (e.g. 6000) to major units (60). */
export function toMajorUnits(minor: number): number {
  return minor / 100;
}

/** Format a minor-unit amount as a locale-aware currency string. */
export function formatCurrency(minorUnits: number, currency: string, locale = 'en-US'): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(toMajorUnits(minorUnits));
}
