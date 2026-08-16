import { Paragraphs } from './Paragraphs'
import type { SaintResponse } from '../api/types'

export function SaintCard({ saint }: { saint: SaintResponse }) {
  return (
    <section className="rounded-2xl border border-black/5 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-neutral-900">
      <p className="text-xs font-semibold tracking-wide text-amber-700 uppercase dark:text-amber-400">
        Santo do Dia
      </p>
      <h2 className="mt-1 font-serif-reading text-xl text-neutral-900 dark:text-neutral-50">{saint.name}</h2>

      {saint.imageUrl && (
        <img
          src={saint.imageUrl}
          alt={saint.name}
          loading="lazy"
          className="mt-4 max-h-72 w-full rounded-xl object-cover"
        />
      )}

      <Paragraphs
        text={saint.biography}
        className="mt-4 font-serif-reading text-[17px] leading-relaxed text-neutral-700 dark:text-neutral-300"
      />
    </section>
  )
}
