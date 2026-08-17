import { Paragraphs } from './Paragraphs'
import type { HomilyResponse } from '../api/types'

export function HomilyCard({ homily }: { homily: HomilyResponse }) {
  return (
    <section className="rounded-2xl border border-black/5 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-neutral-900">
      <p className="text-xs font-semibold tracking-wide text-amber-700 uppercase dark:text-amber-400">
        Homilia do Papa
      </p>
      <h2 className="mt-1 font-serif-reading text-xl text-neutral-900 dark:text-neutral-50">{homily.title}</h2>

      {homily.isAwaitingTranslation && (
        <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">
          Tradução para português ainda pendente — texto exibido no idioma original.
        </p>
      )}

      <Paragraphs
        text={homily.displayText}
        className="mt-4 font-serif-reading text-[18px] leading-[1.8] text-neutral-700 dark:text-neutral-300"
      />

      <a
        href={homily.sourceUrl}
        target="_blank"
        rel="noreferrer"
        className="mt-4 inline-block text-sm text-amber-700 underline decoration-dotted underline-offset-4 dark:text-amber-400"
      >
        Ver fonte original no vatican.va →
      </a>
    </section>
  )
}
