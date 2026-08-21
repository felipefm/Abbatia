import { Card } from './Card'
import { Paragraphs } from './Paragraphs'
import type { HomilyResponse } from '../api/types'

export function HomilyCard({ homily }: { homily: HomilyResponse }) {
  return (
    <Card id="homily" eyebrow="Homilia do Papa" title={homily.title}>
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
    </Card>
  )
}
