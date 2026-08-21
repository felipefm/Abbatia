import { useCallback, useEffect, useState } from 'react'
import { ApiError, getMonthCalendar } from '../api/client'
import type { MonthCalendarResponse } from '../api/types'

interface UseMonthCalendarResult {
  data: MonthCalendarResponse | null
  loading: boolean
  error: ApiError | Error | null
  retry: () => void
}

/** Busca a cor litúrgica de cada dia de um mês (year, month 1-12). Mesmo
 * formato de useDevotional.ts: AbortController + retry manual. */
export function useMonthCalendar(year: number, month: number): UseMonthCalendarResult {
  const [data, setData] = useState<MonthCalendarResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<ApiError | Error | null>(null)
  const [retryToken, setRetryToken] = useState(0)

  const retry = useCallback(() => setRetryToken((token) => token + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)

    getMonthCalendar(year, month, controller.signal)
      .then((result) => {
        setData(result)
        setLoading(false)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setError(err instanceof Error ? err : new Error('Erro desconhecido ao buscar o calendário.'))
        setLoading(false)
      })

    return () => controller.abort()
  }, [year, month, retryToken])

  return { data, loading, error, retry }
}
