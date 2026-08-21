import { Card } from '../Card'
import { useDiaryEntry } from '../../hooks/useDiaryEntry'

const TEXTAREA_CLASS =
  'mt-2 w-full resize-y rounded-lg border border-black/10 bg-transparent p-2 font-serif-reading text-[15px] leading-relaxed text-neutral-800 outline-none placeholder:text-neutral-400 focus:border-amber-500 dark:border-white/10 dark:text-neutral-200 dark:placeholder:text-neutral-600'

export function DiaryCard({ date }: { date: string }) {
  const { text, setText, loading, saving, saved, error, save } = useDiaryEntry(date)

  return (
    <Card eyebrow="Diário espiritual">
      <textarea
        rows={5}
        value={text}
        disabled={loading}
        onChange={(event) => setText(event.target.value)}
        placeholder="Uma reflexão rápida sobre o dia..."
        className={TEXTAREA_CLASS}
      />

      <div className="mt-2 flex items-center justify-between">
        <button
          type="button"
          onClick={save}
          disabled={saving || loading}
          className="rounded-full border border-black/10 px-4 py-1.5 text-sm font-medium text-neutral-700 transition hover:bg-black/5 disabled:opacity-50 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5"
        >
          {saving ? 'Salvando…' : 'Salvar'}
        </button>
        {saved && <span className="text-xs text-neutral-500 dark:text-neutral-400">Salvo.</span>}
        {error && <span className="text-xs text-red-600 dark:text-red-400">Erro ao salvar.</span>}
      </div>
    </Card>
  )
}
