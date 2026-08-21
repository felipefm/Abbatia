import { Card } from '../Card'
import { getExcerpt } from '../../lib/excerpt'
import type { HomilyResponse, ReadingResponse } from '../../api/types'

interface PullQuoteCardProps {
  readings: ReadingResponse[]
  homily: HomilyResponse | null
}

export function PullQuoteCard({ readings, homily }: PullQuoteCardProps) {
  const gospel = readings.find((reading) => reading.type === 'Evangelho')
  const source = gospel ?? homily
  if (!source) return null

  const text = 'text' in source ? source.text : source.displayText
  const label = gospel ? 'Do Evangelho de hoje' : 'Da homilia de hoje'

  return (
    <Card eyebrow="Em destaque">
      <blockquote className="mt-2 font-serif-reading text-lg leading-snug text-neutral-800 italic dark:text-neutral-200">
        “{getExcerpt(text)}”
      </blockquote>
      <p className="mt-3 text-sm text-neutral-500 dark:text-neutral-400">{label}</p>
    </Card>
  )
}
