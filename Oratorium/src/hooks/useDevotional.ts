import { useCallback, useEffect, useState } from 'react'
import { ApiError, getDevotionalByDate, getDevotionalToday } from '../api/client'
import type { DevotionalResponse } from '../api/types'

interface UseDevotionalResult {
  data: DevotionalResponse | null
  loading: boolean
  error: ApiError | Error | null
  retry: () => void
}

/**
 * Busca o devocional de uma data específica, ou de hoje quando `date` é
 * `undefined`. Refaz a busca automaticamente sempre que `date` muda (ex:
 * usuário navega para o dia seguinte), e expõe `retry()` para o usuário
 * tentar de novo manualmente após uma falha de rede.
 */
export function useDevotional(date: string | undefined): UseDevotionalResult {
  const [data, setData] = useState<DevotionalResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<ApiError | Error | null>(null)
  const [retryToken, setRetryToken] = useState(0)

  const retry = useCallback(() => setRetryToken((token) => token + 1), [])

  useEffect(() => {
    const controller = new AbortController()

    setLoading(true)
    setError(null)

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

    return () => controller.abort()
  }, [date, retryToken])

  return { data, loading, error, retry }
}
