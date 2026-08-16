const ISO_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/

export function isValidIsoDate(value: string): boolean {
  return ISO_DATE_PATTERN.test(value)
}

export function todayISO(): string {
  return new Date().toISOString().slice(0, 10)
}

/** Soma (ou subtrai, se negativo) dias a uma data "yyyy-MM-dd", em UTC — evita
 * bugs de fuso horário que `new Date(iso)` + `setDate` teria perto da meia-noite. */
export function addDays(iso: string, days: number): string {
  const [year, month, day] = iso.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day))
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

export function formatLongDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day))
  const formatted = new Intl.DateTimeFormat('pt-BR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(date)
  return formatted.charAt(0).toUpperCase() + formatted.slice(1)
}
