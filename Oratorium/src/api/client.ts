import type {
  ApiErrorBody,
  DevotionalResponse,
  DiaryEntryResponse,
  MonthCalendarResponse,
} from './types'

// Ordem de prioridade: configuração de RUNTIME (window.__ORATORIUM_CONFIG__,
// injetada pelo container Docker via public/env-config.js) > variável de
// BUILD-TIME do Vite (import.meta.env.VITE_API_BASE_URL, usada em dev local)
// > valor padrão. Ver env.d.ts para o porquê dessa camada de runtime existir.
const API_BASE_URL = (
  window.__ORATORIUM_CONFIG__?.apiBaseUrl ||
  import.meta.env.VITE_API_BASE_URL ||
  'http://localhost:8110'
).replace(/\/$/, '')

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface RequestOptions {
  method?: 'GET' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? 'GET',
    headers: options.body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  })

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as ApiErrorBody | null
    throw new ApiError(response.status, body?.erro ?? `Falha ao consultar a API (status ${response.status}).`)
  }

  // DELETE bem-sucedido devolve 204 sem corpo — não dá pra chamar
  // response.json() nesse caso (o body está vazio, o parse falharia).
  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export function getDevotionalToday(signal?: AbortSignal): Promise<DevotionalResponse> {
  return request<DevotionalResponse>('/api/devotional/today', { signal })
}

export function getDevotionalByDate(date: string, signal?: AbortSignal): Promise<DevotionalResponse> {
  return request<DevotionalResponse>(`/api/devotional/${date}`, { signal })
}

export function getMonthCalendar(year: number, month: number, signal?: AbortSignal): Promise<MonthCalendarResponse> {
  return request<MonthCalendarResponse>(`/api/devotional/calendar/${year}/${month}`, { signal })
}

export function getDiaryEntry(date: string, signal?: AbortSignal): Promise<DiaryEntryResponse> {
  return request<DiaryEntryResponse>(`/api/diary/${date}`, { signal })
}

export function saveDiaryEntry(date: string, text: string, signal?: AbortSignal): Promise<DiaryEntryResponse> {
  return request<DiaryEntryResponse>(`/api/diary/${date}`, { method: 'PUT', body: { text }, signal })
}
