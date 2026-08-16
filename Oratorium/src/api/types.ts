/**
 * Espelha exatamente os DTOs expostos por Scriptorium.API
 * (Scriptorium/src/Scriptorium.API/DTOs/DevotionalResponse.cs).
 * Mudanças no formato da resposta da API devem ser refletidas aqui.
 */

export type ReadingType = 'PrimeiraLeitura' | 'SalmoResponsorial' | 'SegundaLeitura' | 'Evangelho'

export type LiturgicalColor = 'Verde' | 'Branco' | 'Vermelho' | 'Roxo' | 'Rosa' | 'Preto'

export interface SaintResponse {
  name: string
  biography: string
  imageUrl: string | null
}

export interface ReadingResponse {
  type: ReadingType
  reference: string
  text: string
}

export interface HomilyResponse {
  title: string
  displayText: string
  isAwaitingTranslation: boolean
  sourceUrl: string
}

export interface DevotionalResponse {
  date: string // yyyy-MM-dd
  liturgicalTitle: string
  liturgicalColor: LiturgicalColor
  saint: SaintResponse | null
  readings: ReadingResponse[]
  homily: HomilyResponse | null
}

export interface ApiErrorBody {
  erro: string
}
