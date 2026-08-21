import { useCallback, useEffect, useState } from 'react'
import { ApiError, getDiaryEntry, saveDiaryEntry } from '../api/client'

interface UseDiaryEntryResult {
  text: string
  setText: (text: string) => void
  loading: boolean
  saving: boolean
  saved: boolean
  error: Error | null
  save: () => Promise<void>
}

/** Busca/salva a entrada do diário espiritual de uma data. Um 404 na busca
 * significa "ainda não existe entrada" — não é tratado como erro. */
export function useDiaryEntry(date: string): UseDiaryEntryResult {
  const [text, setTextState] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    setSaved(false)

    getDiaryEntry(date, controller.signal)
      .then((entry) => setTextState(entry.text))
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        if (err instanceof ApiError && err.status === 404) {
          setTextState('')
          return
        }
        setError(err instanceof Error ? err : new Error('Erro ao carregar o diário.'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [date])

  const setText = useCallback((value: string) => {
    setTextState(value)
    setSaved(false)
  }, [])

  const save = useCallback(async () => {
    setSaving(true)
    setError(null)
    try {
      await saveDiaryEntry(date, text)
      setSaved(true)
    } catch (err: unknown) {
      setError(err instanceof Error ? err : new Error('Erro ao salvar o diário.'))
    } finally {
      setSaving(false)
    }
  }, [date, text])

  return { text, setText, loading, saving, saved, error, save }
}
