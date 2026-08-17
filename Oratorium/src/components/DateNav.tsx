import { useNavigate } from 'react-router-dom'
import { addDays, isValidIsoDate, todayISO } from '../lib/date'

interface DateNavProps {
  /** Data atualmente exibida (yyyy-MM-dd). */
  currentDate: string
}

export function DateNav({ currentDate }: DateNavProps) {
  const navigate = useNavigate()
  const isToday = currentDate === todayISO()

  return (
    <nav className="flex flex-col items-center gap-2">
      <div className="flex w-full items-center justify-between gap-2">
        <button
          type="button"
          onClick={() => navigate(`/dia/${addDays(currentDate, -1)}`)}
          className="rounded-full border border-black/10 px-4 py-2 text-sm font-medium text-neutral-700 transition hover:bg-black/5 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5"
          aria-label="Dia anterior"
        >
          ← Anterior
        </button>

        {/* Input nativo de data: dá um seletor de calendário completo (mês
         * inteiro, navegação por ano) de graça, sem nenhuma biblioteca extra
         * — o navegador já sabe desenhar isso, inclusive no celular (onde o
         * PWA roda). `[color-scheme:dark]` no modo escuro faz o POPUP do
         * calendário (não só o input) respeitar o tema escuro do app. */}
        <input
          type="date"
          value={currentDate}
          onChange={(event) => {
            if (isValidIsoDate(event.target.value)) {
              navigate(`/dia/${event.target.value}`)
            }
          }}
          aria-label="Escolher outra data"
          className="rounded-full border border-black/10 bg-transparent px-3 py-2 text-sm text-neutral-700 outline-none transition hover:bg-black/5 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5 dark:[color-scheme:dark]"
        />

        <button
          type="button"
          onClick={() => navigate(`/dia/${addDays(currentDate, 1)}`)}
          className="rounded-full border border-black/10 px-4 py-2 text-sm font-medium text-neutral-700 transition hover:bg-black/5 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5"
          aria-label="Próximo dia"
        >
          Próximo →
        </button>
      </div>

      {!isToday && (
        <button
          type="button"
          onClick={() => navigate('/hoje')}
          className="rounded-full px-4 py-2 text-sm font-semibold text-amber-700 underline decoration-dotted underline-offset-4 dark:text-amber-400"
        >
          Voltar para hoje
        </button>
      )}
    </nav>
  )
}
