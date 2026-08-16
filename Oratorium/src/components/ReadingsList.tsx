import { Paragraphs } from './Paragraphs'
import { READING_TYPE_LABEL } from '../lib/readingLabels'
import type { ReadingResponse } from '../api/types'

export function ReadingsList({ readings }: { readings: ReadingResponse[] }) {
  if (readings.length === 0) return null

  return (
    <section className="space-y-4">
      {readings.map((reading) => (
        <article
          key={reading.type}
          className="rounded-2xl border border-black/5 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-neutral-900"
        >
          <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
            <h3 className="font-serif-reading text-lg text-neutral-900 dark:text-neutral-50">
              {READING_TYPE_LABEL[reading.type]}
            </h3>
            <span className="text-sm text-neutral-500 dark:text-neutral-400">{reading.reference}</span>
          </div>

          <Paragraphs
            text={reading.text}
            className="mt-3 font-serif-reading text-[17px] leading-relaxed text-neutral-700 dark:text-neutral-300"
          />
        </article>
      ))}
    </section>
  )
}
