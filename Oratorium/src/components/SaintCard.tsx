import { Card } from './Card'
import { Paragraphs } from './Paragraphs'
import type { SaintResponse } from '../api/types'

export function SaintCard({ saint }: { saint: SaintResponse }) {
  return (
    <Card id="saint" eyebrow="Santo do Dia" title={saint.name}>
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
        className="mt-4 font-serif-reading text-[18px] leading-[1.8] text-neutral-700 dark:text-neutral-300"
      />
    </Card>
  )
}
