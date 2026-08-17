import { ApiError } from '../api/client'

export function LoadingState({ slow = false }: { slow?: boolean }) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-black/5 bg-white p-10 text-center dark:border-white/10 dark:bg-neutral-900">
      <div className="h-8 w-8 animate-spin rounded-full border-2 border-amber-600 border-t-transparent" />
      <p className="text-sm text-neutral-500 dark:text-neutral-400">
        {slow
          ? 'Ainda buscando… se for a primeira vez que alguém abre este dia, a API está raspando as fontes ao vivo agora — pode levar até um minuto.'
          : 'Carregando o devocional…'}
      </p>
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: Error; onRetry: () => void }) {
  const isNotFound = error instanceof ApiError && error.status === 404

  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-black/5 bg-white p-10 text-center dark:border-white/10 dark:bg-neutral-900">
      <p className="text-3xl">{isNotFound ? '📖' : '⚠️'}</p>
      <p className="max-w-sm text-sm text-neutral-600 dark:text-neutral-300">
        {isNotFound
          ? 'Não encontramos informações para esta data — mesmo tentando buscar ao vivo nas fontes agora. Tente uma data mais próxima de hoje.'
          : error.message || 'Não foi possível conectar à API do Scriptorium. Verifique se o backend está no ar.'}
      </p>
      {!isNotFound && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-1 rounded-full border border-black/10 px-4 py-2 text-sm font-medium text-neutral-700 transition hover:bg-black/5 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5"
        >
          Tentar novamente
        </button>
      )}
    </div>
  )
}
