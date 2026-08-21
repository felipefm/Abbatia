import { Card } from '../Card'
import type { HomilyResponse } from '../../api/types'

export function HomilySourceCard({ homily }: { homily: HomilyResponse }) {
  return (
    <Card eyebrow="Fonte da homilia">
      <p className="mt-1 text-sm text-neutral-600 dark:text-neutral-400">
        Texto original publicado pelo Vaticano.
      </p>
      <a
        href={homily.sourceUrl}
        target="_blank"
        rel="noreferrer"
        className="mt-2 inline-block text-sm font-medium text-amber-700 underline decoration-dotted underline-offset-4 dark:text-amber-400"
      >
        Ver fonte original →
      </a>
    </Card>
  )
}
