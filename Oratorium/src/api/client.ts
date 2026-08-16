import type { ApiErrorBody, DevotionalResponse } from './types'

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

async function request<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, { signal })

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as ApiErrorBody | null
    throw new ApiError(response.status, body?.erro ?? `Falha ao consultar a API (status ${response.status}).`)
  }

  return response.json() as Promise<T>
}

export function getDevotionalToday(signal?: AbortSignal): Promise<DevotionalResponse> {
  return request<DevotionalResponse>('/api/devotional/today', signal)
}

export function getDevotionalByDate(date: string, signal?: AbortSignal): Promise<DevotionalResponse> {
  return request<DevotionalResponse>(`/api/devotional/${date}`, signal)
}
