/** Matemática pura de calendário litúrgico (Páscoa móvel + tempos
 * derivados dela) — sem chamada de rede, sem estado. Fica no frontend de
 * propósito: é cálculo determinístico, não precisa de raspagem nem
 * persistência; um endpoint novo só pra isso seria complexidade
 * desnecessária. */

function toIso(date: Date): string {
  return date.toISOString().slice(0, 10)
}

function addDays(date: Date, days: number): Date {
  const result = new Date(date)
  result.setUTCDate(result.getUTCDate() + days)
  return result
}

/** Data da Páscoa (calendário gregoriano) via algoritmo de Meeus/Jones/Butcher. */
export function computeEasterDate(year: number): Date {
  const a = year % 19
  const b = Math.floor(year / 100)
  const c = year % 100
  const d = Math.floor(b / 4)
  const e = b % 4
  const f = Math.floor((b + 8) / 25)
  const g = Math.floor((b - f + 1) / 3)
  const h = (19 * a + b - d - g + 15) % 30
  const i = Math.floor(c / 4)
  const k = c % 4
  const l = (32 + 2 * e + 2 * i - h - k) % 7
  const m = Math.floor((a + 11 * h + 22 * l) / 451)
  const month = Math.floor((h + l - 7 * m + 114) / 31) // 3=março, 4=abril
  const day = ((h + l - 7 * m + 114) % 31) + 1

  return new Date(Date.UTC(year, month - 1, day))
}

/** 1º Domingo do Advento: o domingo imediatamente anterior a 25/dez, menos
 * 3 semanas (cai sempre entre 27/nov e 3/dez). */
function getAdventSunday1(year: number): Date {
  const christmas = new Date(Date.UTC(year, 11, 25))
  const daysSincePrecedingSunday = christmas.getUTCDay() === 0 ? 7 : christmas.getUTCDay()
  const fourthAdventSunday = addDays(christmas, -daysSincePrecedingSunday)
  return addDays(fourthAdventSunday, -21)
}

interface SeasonBoundary {
  seasonName: string
  date: Date
}

function getSeasonBoundaries(year: number): SeasonBoundary[] {
  const easter = computeEasterDate(year)

  return [
    { seasonName: 'Quaresma', date: addDays(easter, -46) }, // Quarta de Cinzas
    { seasonName: 'Tríduo Pascal', date: addDays(easter, -3) }, // Quinta-feira Santa
    { seasonName: 'Páscoa', date: easter },
    { seasonName: 'Pentecostes', date: addDays(easter, 49) },
    { seasonName: 'Advento', date: getAdventSunday1(year) },
    { seasonName: 'Natal', date: new Date(Date.UTC(year, 11, 25)) },
  ]
}

export interface SeasonCountdown {
  seasonName: string
  date: string // yyyy-MM-dd
  daysUntil: number
}

/** Próximo tempo/solenidade litúrgica maior a partir de hoje — olha o ano
 * atual e o seguinte, pra não "prender" a busca perto da virada do ano. */
export function getNextSeasonCountdown(todayIso: string): SeasonCountdown {
  const [year] = todayIso.split('-').map(Number)
  const today = new Date(`${todayIso}T00:00:00Z`)

  const boundaries = [...getSeasonBoundaries(year), ...getSeasonBoundaries(year + 1)]
    .filter((boundary) => boundary.date.getTime() > today.getTime())
    .sort((a, b) => a.date.getTime() - b.date.getTime())

  const next = boundaries[0]
  const daysUntil = Math.round((next.date.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))

  return { seasonName: next.seasonName, date: toIso(next.date), daysUntil }
}
