import { Card } from './Card'
import { Paragraphs } from './Paragraphs'
import { READING_TYPE_LABEL } from '../lib/readingLabels'
import type { ReadingResponse } from '../api/types'

export function ReadingsList({ readings }: { readings: ReadingResponse[] }) {
  if (readings.length === 0) return null

  return (
    <section id="readings" className="space-y-4">
      {readings.map((reading) => (
        <Card key={reading.type}>
          <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
            <h3 className="font-serif-reading text-lg text-neutral-900 dark:text-neutral-50">
              {READING_TYPE_LABEL[reading.type]}
            </h3>
            <span className="text-sm text-neutral-500 dark:text-neutral-400">{reading.reference}</span>
          </div>

          <Paragraphs
            text={reading.text}
            highlightVerseNumbers
            className="mt-4 font-serif-reading text-[18px] leading-[1.8] text-neutral-700 dark:text-neutral-300"
          />
        </Card>
      ))}
    </section>
  )
}
