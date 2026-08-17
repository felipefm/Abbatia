import { useCallback, useEffect, useState } from 'react'
import { ApiError, getDevotionalByDate, getDevotionalToday } from '../api/client'
import type { DevotionalResponse } from '../api/types'

interface UseDevotionalResult {
  data: DevotionalResponse | null
  loading: boolean
  /** true quando o carregamento já passa de alguns segundos — sinal de que a
   * API provavelmente está buscando essa data ao vivo pela primeira vez
   * (cache-miss), não um carregamento normal do banco. */
  slow: boolean
  error: ApiError | Error | null
  retry: () => void
}

/** Depois desse tempo sem resposta, assumimos que pode ser um cache-miss
 * (busca sob demanda no Scriptorium.API) e trocamos a mensagem de
 * carregamento para não parecer que o app travou. */
const SLOW_LOADING_THRESHOLD_MS = 4000

/**
 * Busca o devocional de uma data específica, ou de hoje quando `date` é
 * `undefined`. Refaz a busca automaticamente sempre que `date` muda (ex:
 * usuário navega para o dia seguinte), e expõe `retry()` para o usuário
 * tentar de novo manualmente após uma falha de rede.
 */
export function useDevotional(date: string | undefined): UseDevotionalResult {
  const [data, setData] = useState<DevotionalResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [slow, setSlow] = useState(false)
  const [error, setError] = useState<ApiError | Error | null>(null)
  const [retryToken, setRetryToken] = useState(0)

  const retry = useCallback(() => setRetryToken((token) => token + 1), [])

  useEffect(() => {
    const controller = new AbortController()

    setLoading(true)
    setSlow(false)
    setError(null)

    const slowTimer = window.setTimeout(() => setSlow(true), SLOW_LOADING_THRESHOLD_MS)

    const fetchPromise = date
      ? getDevotionalByDate(date, controller.signal)
      : getDevotionalToday(controller.signal)

    fetchPromise
      .then((result) => {
        setData(result)
        setLoading(false)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setError(err instanceof Error ? err : new Error('Erro desconhecido ao buscar o devocional.'))
        setData(null)
        setLoading(false)
      })
      .finally(() => window.clearTimeout(slowTimer))

    return () => {
      controller.abort()
      window.clearTimeout(slowTimer)
    }
  }, [date, retryToken])

  return { data, loading, slow, error, retry }
}
